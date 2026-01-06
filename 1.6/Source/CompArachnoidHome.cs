using RimWorld;
using Verse;

namespace Arachnoids
{
    public enum ArachnoidMode : byte
    {
        Defend = 0,
        Hunt = 1,
        Drag = 2
    }

    public class CompProperties_ArachnoidHome : CompProperties
    {
        public int defaultRadius = 20;

        public CompProperties_ArachnoidHome()
        {
            compClass = typeof(CompArachnoidHome);
        }
    }

    /// <summary>
    /// Stores home + hunt/drag state.
    /// Jobs are still chosen by ThinkTree / JobGivers.
    /// </summary>
    public partial class CompArachnoidHome : ThingComp
    {
        // ---- Hunt tuning ----
        public const int NormalHuntCooldownTicks = 120000; // 2 days
        public const int FailedHuntCooldownDivisor = 4;    // 1/4 cooldown on failure

        // Abort conditions
        public const int HuntTimeoutTicks = 15000;               // ~1/4 day
        public const int BaseVictimAbortDistanceFromHome = 130;  // tiles (scaled by settings)
        public const int AbortMinDistanceFromPlayerDoor = 20;
        public const int AbortMinDistanceFromPlayerTurret = 30;

        // Pause on failed hunt (listening / reassessing)
        public const int AbortPauseMinTicks = 60;   // ~1 second
        public const int AbortPauseMaxTicks = 120;  // ~2 seconds

        // ---- Home ----
        private int homeX = int.MinValue;
        private int homeZ = int.MinValue;
        private int radius = 20;

        // ---- Mode / cooldown ----
        private ArachnoidMode mode = ArachnoidMode.Defend;
        private int nextHuntTick = 0;

        // ---- Hunt tracking ----
        private int huntStartTick = -1;
        private int victimThingId = -1;

        public Pawn Pawn => parent as Pawn;

        // Must be settable (other code assigns these)
        public ArachnoidMode Mode
        {
            get => mode;
            set => mode = value;
        }

        public int NextHuntTick
        {
            get => nextHuntTick;
            set => nextHuntTick = value;
        }

        public bool HasHome => homeX != int.MinValue && homeZ != int.MinValue;
        public IntVec3 HomeCell => new IntVec3(homeX, 0, homeZ);
        public int Radius => radius;

        public void SetHome(IntVec3 cell, int newRadius)
        {
            homeX = cell.x;
            homeZ = cell.z;
            if (newRadius > 0) radius = newRadius;
        }

        public void ClearVictim()
        {
            victimThingId = -1;
        }

        public void BeginHunt(Pawn victim, int now)
        {
            mode = ArachnoidMode.Hunt;
            victimThingId = victim?.thingIDNumber ?? -1;
            huntStartTick = now;
        }

        public void BeginDragCapture(Pawn victim, int now)
        {
            mode = ArachnoidMode.Drag;
            victimThingId = victim?.thingIDNumber ?? -1;
            huntStartTick = now;
        }

        // Keep the old signature used throughout the mod
        public void EndHunt(bool success, int now)
        {
            EndHunt(success, now, pauseOnFail: false);
        }

        /// <summary>
        /// Ends the hunt and applies cooldown. If pauseOnFail is true, failed hunts will briefly "pause".
        /// </summary>
        public void EndHunt(bool success, int now, bool pauseOnFail)
        {
            // Optional "listening pause" on failed hunts
            if (!success && pauseOnFail && Pawn != null && Pawn.Spawned)
            {
                try
                {
                    int pauseTicks = Rand.RangeInclusive(AbortPauseMinTicks, AbortPauseMaxTicks);
                    Pawn.stances?.stunner?.StunFor(pauseTicks, Pawn);
                }
                catch { }
            }

            mode = ArachnoidMode.Defend;
            victimThingId = -1;
            huntStartTick = -1;

            int cooldown = success
                ? NormalHuntCooldownTicks
                : (NormalHuntCooldownTicks / FailedHuntCooldownDivisor);

            nextHuntTick = now + cooldown;
        }

        // Must be public (JobGivers call it)
        public Pawn GetVictimPawn(Map map)
        {
            if (victimThingId < 0 || map == null) return null;
            var pawns = map.mapPawns?.AllPawnsSpawned;
            if (pawns == null) return null;

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p != null && p.thingIDNumber == victimThingId)
                    return p;
            }
            return null;
        }

        public override void CompTickRare()
        {
            base.CompTickRare();

            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead || pawn.Map == null) return;

            int now = Find.TickManager.TicksGame;

            if (mode == ArachnoidMode.Hunt)
            {
                Pawn victim = GetVictimPawn(pawn.Map);
                if (victim == null || victim.Dead)
                {
                    EndHunt(false, now, pauseOnFail: true);
                    return;
                }

                // Hard timeout: don't hunt forever
                if (huntStartTick > 0 && now - huntStartTick > HuntTimeoutTicks)
                {
                    EndHunt(false, now, pauseOnFail: true);
                    return;
                }

                // Victim fled too far outside spider territory (scaled)
                float mult = ArachnoidsMod.Settings?.huntRangeMultiplier ?? 1f;
                int abortDist = (int)(BaseVictimAbortDistanceFromHome * mult);
                if (HasHome && HomeCell.DistanceTo(victim.Position) > abortDist)
                {
                    EndHunt(false, now, pauseOnFail: true);
                    return;
                }

                // Victim reached colony defenses (door/turret proximity)
                if (VictimTooCloseToColonyDefenses(pawn.Map, victim.Position))
                {
                    EndHunt(false, now, pauseOnFail: true);
                    return;
                }

                // Abort hunt if seriously damaged BEFORE kidnap triggers (<60% health)
                if (pawn.health?.summaryHealth.SummaryHealthPercent < 0.60f)
                {
                    EndHunt(false, now, pauseOnFail: true);
                    return;
                }

                if (victim.Downed)
                {
                    mode = ArachnoidMode.Drag;
                }
            }
            else if (mode == ArachnoidMode.Drag)
            {
                Pawn victim = GetVictimPawn(pawn.Map);
                if (victim == null || victim.Dead)
                {
                    // No pause here: usually "lost the body" rather than "stalking failed"
                    EndHunt(false, now, pauseOnFail: false);
                }
            }
        }

        private bool VictimTooCloseToColonyDefenses(Map map, IntVec3 pos)
        {
            var doors = map.listerThings?.ThingsInGroup(ThingRequestGroup.Door);
            if (doors != null)
            {
                for (int i = 0; i < doors.Count; i++)
                {
                    Thing d = doors[i];
                    if (d?.Faction == Faction.OfPlayer && pos.DistanceTo(d.Position) <= AbortMinDistanceFromPlayerDoor)
                        return true;
                }
            }

            var buildings = map.listerBuildings?.allBuildingsColonist;
            if (buildings != null)
            {
                for (int i = 0; i < buildings.Count; i++)
                {
                    if (buildings[i] is Building_Turret t &&
                        pos.DistanceTo(t.Position) <= AbortMinDistanceFromPlayerTurret)
                        return true;
                }
            }

            return false;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Values.Look(ref homeX, "arachHomeX", int.MinValue);
            Scribe_Values.Look(ref homeZ, "arachHomeZ", int.MinValue);
            Scribe_Values.Look(ref radius, "arachHomeRadius", radius);

            Scribe_Values.Look(ref mode, "arachMode", ArachnoidMode.Defend);
            Scribe_Values.Look(ref nextHuntTick, "arachNextHuntTick", 0);
            Scribe_Values.Look(ref victimThingId, "arachVictimThingId", -1);

            Scribe_Values.Look(ref huntStartTick, "arachHuntStartTick", -1);
        }
    }
}