using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Arachnoids
{
    public class IncidentWorker_ArachnoidHomeSpider : IncidentWorker
    {
        private const int DefaultHomeRadius = 20;

        // Candidate sampling
        private const int CandidateSamples = 500;
        private const int TopPickCount = 20;

        // Edge distance constraints
        private const int MinDistFromEdge = 10;
        private const int MaxDistFromEdge = 75;

        // Player proximity constraints
        private const int MinDistFromAnyPlayerThing = 10;
        private const int MinDistFromPlayerDoorOrTurret = 50;

        // Anti-cluster
        private const int MinDistFromOtherLair = 100;

        // Scoring radius
        private const int DensityRadius = 4;

        // Spawn offset (spider spawns near home, not on it)
        private const int SpawnNearHomeRadius = 5;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (map == null) return false;

            return DefDatabase<PawnKindDef>.GetNamedSilentFail("Arachnoid_Spider") != null;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (map == null) return false;

            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Arachnoid_Spider");
            if (kind == null) return false;

            // Get (or generate) the Arachnoid faction instance.
            FactionDef factionDef = DefDatabase<FactionDef>.GetNamedSilentFail("Arachnoid");
            Faction faction = null;

            if (factionDef != null)
            {
                faction = Find.FactionManager.FirstFactionOfDef(factionDef);
                if (faction == null)
                {
                    var fgParms = new FactionGeneratorParms(factionDef);
                    faction = FactionGenerator.NewGeneratedFaction(fgParms);
                    Find.FactionManager.Add(faction);
                }
            }

            // Pick a scored home cell (lair)
            IntVec3 home;
            if (!TryFindHomeCell_Scored(map, out home))
                home = map.Center;

            // Find a separate spawn cell near home (avoid spawning on the lair tile)
            IntVec3 spawnCell;
            if (!TryFindSpawnCellNearHome(map, home, out spawnCell))
                spawnCell = home;

            // Generate + spawn the pawn
            PawnGenerationRequest req = new PawnGenerationRequest(
                kind,
                faction,
                PawnGenerationContext.NonPlayer,
                map.Tile,
                forceGenerateNewPawn: true,
                allowDead: false,
                allowDowned: false
            );

            Pawn spider = PawnGenerator.GeneratePawn(req);
            if (spider == null) return false;

            GenSpawn.Spawn(spider, spawnCell, map, WipeMode.Vanish);

            // Initialize home area
            CompArachnoidHome comp = spider.TryGetComp<CompArachnoidHome>();
            if (comp != null)
            {
                comp.SetHome(home, DefaultHomeRadius);
                comp.Mode = ArachnoidMode.Defend;
            }

            SendStandardLetter(parms, spider);
            return true;
        }

        private bool TryFindSpawnCellNearHome(Map map, IntVec3 home, out IntVec3 spawnCell)
        {
            spawnCell = IntVec3.Invalid;

            // Small sanity: home must be in bounds
            if (!home.InBounds(map)) return false;

            TraverseParms tp = TraverseParms.For(TraverseMode.PassDoors, Danger.Some);

            // Try a few random close walk cells near home with a strict validator
            for (int i = 0; i < 25; i++)
            {
                IntVec3 c = CellFinder.RandomClosewalkCellNear(home, map, SpawnNearHomeRadius);
                if (!c.InBounds(map)) continue;
                if (c == home) continue;
                if (map.fogGrid != null && map.fogGrid.IsFogged(c)) continue;
                if (!c.Standable(map)) continue;

                TerrainDef terr = c.GetTerrain(map);
                if (terr != null && terr.IsWater) continue;

                // Make sure it can reach the lair tile (helps avoid weird micro-pockets)
                if (map.reachability != null && !map.reachability.CanReach(c, home, PathEndMode.OnCell, tp))
                    continue;

                spawnCell = c;
                return true;
            }

            // Fallback: scan the radial ring for the first valid cell
            for (int r = 1; r <= SpawnNearHomeRadius; r++)
            {
                foreach (IntVec3 c in GenRadial.RadialCellsAround(home, r, true))
                {
                    if (!c.InBounds(map)) continue;
                    if (c == home) continue;
                    if (map.fogGrid != null && map.fogGrid.IsFogged(c)) continue;
                    if (!c.Standable(map)) continue;

                    TerrainDef terr = c.GetTerrain(map);
                    if (terr != null && terr.IsWater) continue;

                    if (map.reachability != null && !map.reachability.CanReach(c, home, PathEndMode.OnCell, tp))
                        continue;

                    spawnCell = c;
                    return true;
                }
            }

            return false;
        }

        private bool TryFindHomeCell_Scored(Map map, out IntVec3 result)
        {
            // Colony anchor for reachability sanity checks
            IntVec3 colonyAnchor = map.Center;
            if (map.mapPawns != null && map.mapPawns.FreeColonistsSpawned != null && map.mapPawns.FreeColonistsSpawned.Count > 0)
                colonyAnchor = map.mapPawns.FreeColonistsSpawned[0].Position;

            TraverseParms tp = TraverseParms.For(TraverseMode.PassDoors, Danger.Some);

            // Gather player buildings once
            List<Building> colonistBuildings = map.listerBuildings?.allBuildingsColonist;
            if (colonistBuildings == null) colonistBuildings = new List<Building>();

            // Build quick subsets (doors + turrets)
            List<Building> playerDoors = new List<Building>();
            List<Building> playerTurrets = new List<Building>();

            for (int i = 0; i < colonistBuildings.Count; i++)
            {
                Building b = colonistBuildings[i];
                if (b == null) continue;
                if (b.Faction != Faction.OfPlayer) continue;

                if (b.def != null && b.def.IsDoor) playerDoors.Add(b);
                if (b is Building_Turret) playerTurrets.Add(b);
            }

            // Collect existing arachnoid lairs (from other spiders)
            List<IntVec3> existingLairs = new List<IntVec3>();
            if (map.mapPawns != null)
            {
                IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn p = pawns[i];
                    if (p == null || p.Dead) continue;

                    CompArachnoidHome h = p.TryGetComp<CompArachnoidHome>();
                    if (h != null && h.HasHome)
                        existingLairs.Add(h.HomeCell);
                }
            }

            List<(IntVec3 cell, int score)> candidates = new List<(IntVec3, int)>(CandidateSamples);

            for (int i = 0; i < CandidateSamples; i++)
            {
                IntVec3 c = CellFinder.RandomCell(map);
                if (!c.InBounds(map)) continue;

                // Must be revealed
                if (map.fogGrid != null && map.fogGrid.IsFogged(c)) continue;

                // Standable + not water
                if (!c.Standable(map)) continue;
                TerrainDef terr = c.GetTerrain(map);
                if (terr != null && terr.IsWater) continue;

                // Edge band
                int edgeDist = c.DistanceToEdge(map);
                if (edgeDist < MinDistFromEdge) continue;
                if (edgeDist > MaxDistFromEdge) continue;

                // Must be reachable from the colony anchor (and vice versa)
                if (map.reachability != null)
                {
                    if (!map.reachability.CanReach(colonyAnchor, c, PathEndMode.OnCell, tp)) continue;
                    if (!map.reachability.CanReach(c, colonyAnchor, PathEndMode.OnCell, tp)) continue;
                }

                // >10 tiles from anything player-claimed (buildings)
                if (TooCloseToPlayerBuilding(c, colonistBuildings, MinDistFromAnyPlayerThing)) continue;

                // >50 tiles from any player door or turret
                if (TooCloseToAny(c, playerDoors, MinDistFromPlayerDoorOrTurret)) continue;
                if (TooCloseToAny(c, playerTurrets, MinDistFromPlayerDoorOrTurret)) continue;

                int score = ScoreCell(map, c, existingLairs);
                candidates.Add((c, score));
            }

            if (candidates.Count == 0)
            {
                result = IntVec3.Invalid;
                return false;
            }

            // Sort best-first
            candidates.Sort((a, b) => b.score.CompareTo(a.score));

            int pickN = Math.Min(TopPickCount, candidates.Count);

            // Weighted pick among top N by rank (best has highest weight)
            // rank 1 weight = N, rank N weight = 1
            int totalWeight = (pickN * (pickN + 1)) / 2; // N + (N-1) + ... + 1
            int roll = Rand.RangeInclusive(1, totalWeight);

            int running = 0;
            for (int r = 0; r < pickN; r++)
            {
                int weight = pickN - r;
                running += weight;
                if (roll <= running)
                {
                    result = candidates[r].cell;
                    return true;
                }
            }

            // Fallback (should never happen)
            result = candidates[0].cell;
            return true;
        }

        private int ScoreCell(Map map, IntVec3 c, List<IntVec3> existingLairs)
        {
            int score = 0;

            // Roof scoring
            RoofDef roof = map.roofGrid?.RoofAt(c);
            if (roof == RoofDefOf.RoofRockThin) score += 30;
            else if (roof == RoofDefOf.RoofRockThick) score += 10;

            // Density around the cell
            int mineables = 0;
            int trees = 0;

            foreach (IntVec3 x in GenRadial.RadialCellsAround(c, DensityRadius, true))
            {
                if (!x.InBounds(map)) continue;

                List<Thing> things = x.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing t = things[i];
                    if (t == null) continue;

                    // Mineables (ores/rock) — counts Mineable things (not chunks)
                    if (t.def != null && t.def.mineable)
                        mineables++;

                    // Trees
                    if (t is Plant p && p.def?.plant != null && p.def.plant.IsTree)
                        trees++;
                }
            }

            score += mineables; // +1 each
            score += trees;     // +1 each

            // Anti-cluster: -100 if another lair within 100 tiles
            for (int i = 0; i < existingLairs.Count; i++)
            {
                if (existingLairs[i].IsValid && existingLairs[i].DistanceTo(c) <= MinDistFromOtherLair)
                {
                    score -= 100;
                    break;
                }
            }

            return score;
        }

        private bool TooCloseToPlayerBuilding(IntVec3 c, List<Building> buildings, int minDist)
        {
            for (int i = 0; i < buildings.Count; i++)
            {
                Building b = buildings[i];
                if (b == null) continue;
                if (b.Faction != Faction.OfPlayer) continue;

                if (c.DistanceTo(b.Position) <= minDist)
                    return true;
            }
            return false;
        }

        private bool TooCloseToAny(IntVec3 c, List<Building> things, int minDist)
        {
            for (int i = 0; i < things.Count; i++)
            {
                Building b = things[i];
                if (b == null) continue;
                if (b.Faction != Faction.OfPlayer) continue;

                if (c.DistanceTo(b.Position) <= minDist)
                    return true;
            }
            return false;
        }
    }
}