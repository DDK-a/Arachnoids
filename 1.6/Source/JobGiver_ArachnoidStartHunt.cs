using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Arachnoids
{
    public class JobGiver_ArachnoidStartHunt : ThinkNode_JobGiver
    {
        private const float MinHealthToHunt = 0.75f;

        private const int BaseVictimMaxDistanceFromHome = 90;
        private const int MaxOtherHumanlikesNearVictim = 2;
        private const int OtherHumanlikesRadius = 15;

        private const int MinDistanceFromPlayerDoor = 20;
        private const int MinDistanceFromPlayerTurret = 30;

        // Throttle scanning so we don't do heavy work every think tick
        private const int ScanIntervalTicks = 500;
        private int nextScanTick = 0;

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn?.Map == null || pawn.Dead || pawn.Downed) return null;

            var comp = pawn.TryGetComp<CompArachnoidHome>();
            if (comp == null || !comp.HasHome) return null;

            // Only start hunts from Defend mode
            if (comp.Mode != ArachnoidMode.Defend) return null;

            int now = Find.TickManager.TicksGame;
            if (now < comp.NextHuntTick) return null;

            // Throttle
            if (now < nextScanTick) return null;
            nextScanTick = now + ScanIntervalTicks;

            // Health gate
            if (pawn.health != null && pawn.health.summaryHealth.SummaryHealthPercent < MinHealthToHunt)
                return null;

            // Not in combat currently (simple definition)
            if (IsInCombat(pawn)) return null;

            Pawn victim = TryFindVictim(pawn, comp);
            if (victim == null) return null;

            // Commit: start hunt tracking (cooldown is applied when the hunt resolves)
            comp.BeginHunt(victim, now);

            // Force "combat engaged" state so ability/fight jobgivers run even if victim hasn't attacked yet.
            if (pawn.mindState != null)
            {
                pawn.mindState.enemyTarget = victim;
                pawn.mindState.lastEngageTargetTick = now;
            }

            // Yield control immediately so the Hunt subtree can choose WebShot / stand-off movement.
            Job brief = JobMaker.MakeJob(JobDefOf.Wait_Combat);
            brief.expiryInterval = 1;
            brief.checkOverrideOnExpire = true;
            return brief;
        }

        private bool IsInCombat(Pawn pawn)
        {
            if (pawn.CurJob != null)
            {
                if (pawn.CurJob.def == JobDefOf.AttackMelee) return true;
                if (pawn.CurJob.def != null && pawn.CurJob.def.defName != null && pawn.CurJob.def.defName.Contains("Combat"))
                    return true;
            }

            if (pawn.mindState != null && pawn.mindState.lastHarmTick >= 0)
            {
                int sinceHarm = Find.TickManager.TicksGame - pawn.mindState.lastHarmTick;
                if (sinceHarm >= 0 && sinceHarm < 600) return true;
            }

            if (pawn.mindState != null && pawn.mindState.enemyTarget != null) return true;

            var pawns = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (other == null || other.Dead) continue;
                if (!other.RaceProps.Humanlike) continue;

                if (pawn.Position.DistanceTo(other.Position) <= 25f && pawn.HostileTo(other))
                    return true;
            }

            return false;
        }

        private Pawn TryFindVictim(Pawn spider, CompArachnoidHome comp)
        {
            Map map = spider.Map;
            IntVec3 home = comp.HomeCell;

            float mult = ArachnoidsMod.Settings?.huntRangeMultiplier ?? 1f;
            int victimMaxDistFromHome = Mathf.RoundToInt(BaseVictimMaxDistanceFromHome * mult);

            List<Thing> doors = map.listerThings.ThingsInGroup(ThingRequestGroup.Door);
            List<Building> colonistBuildings = map.listerBuildings.allBuildingsColonist;

            Pawn best = null;
            float bestDistToSpider = float.MaxValue;

            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p == null || p.Dead || p.Downed) continue;
                if (p == spider) continue;
                if (!p.RaceProps.Humanlike) continue;

                if (home.DistanceTo(p.Position) > victimMaxDistFromHome) continue;
                if (!spider.CanReach(p, PathEndMode.Touch, Danger.Some)) continue;

                if (!VictimIsIsolated(map, p, MaxOtherHumanlikesNearVictim, OtherHumanlikesRadius)) continue;
                if (TooCloseToPlayerDoor(p.Position, doors, MinDistanceFromPlayerDoor)) continue;
                if (TooCloseToPlayerTurret(p.Position, colonistBuildings, MinDistanceFromPlayerTurret)) continue;

                float distToSpider = spider.Position.DistanceTo(p.Position);
                if (distToSpider < bestDistToSpider)
                {
                    bestDistToSpider = distToSpider;
                    best = p;
                }
            }

            return best;
        }

        private bool VictimIsIsolated(Map map, Pawn victim, int maxOthers, int radius)
        {
            int count = 0;
            var pawns = map.mapPawns.AllPawnsSpawned;

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p == null || p.Dead || p == victim) continue;
                if (!p.RaceProps.Humanlike) continue;

                if (p.Position.DistanceTo(victim.Position) <= radius)
                {
                    count++;
                    if (count > maxOthers) return false;
                }
            }

            return true;
        }

        private bool TooCloseToPlayerDoor(IntVec3 pos, List<Thing> doors, int minDist)
        {
            if (doors == null) return false;
            for (int i = 0; i < doors.Count; i++)
            {
                Thing d = doors[i];
                if (d == null) continue;
                if (d.Faction != Faction.OfPlayer) continue;

                if (pos.DistanceTo(d.Position) <= minDist) return true;
            }
            return false;
        }

        private bool TooCloseToPlayerTurret(IntVec3 pos, List<Building> colonistBuildings, int minDist)
        {
            if (colonistBuildings == null) return false;

            for (int i = 0; i < colonistBuildings.Count; i++)
            {
                Building b = colonistBuildings[i];
                if (b == null) continue;
                if (b.Faction != Faction.OfPlayer) continue;

                if (b is Building_Turret)
                {
                    if (pos.DistanceTo(b.Position) <= minDist) return true;
                }
            }

            return false;
        }
    }
}