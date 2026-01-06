using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Arachnoids
{
    public static class DownedJobUtility_Arachnoids
    {
        public static bool IsArachnoidPawn(Pawn pawn)
        {
            if (pawn == null) return false;
            string defName = pawn.def?.defName;
            return defName != null && defName.StartsWith("Arachnoid_");
        }

        public static void EnsureDownedWaitJob(Pawn pawn)
        {
            if (pawn == null) return;
            if (!pawn.Spawned) return;
            if (pawn.Dead) return;
            if (!pawn.Downed) return;

            // Already correctly downed-waiting
            if (pawn.jobs?.curJob != null && pawn.jobs.curJob.def == JobDefOf.Wait_Downed)
                return;

            if (pawn.jobs == null) return;

            // Interrupt anything (including wander goto)
            pawn.jobs.StopAll();

            // Force the standard downed wait job
            Job wait = JobMaker.MakeJob(JobDefOf.Wait_Downed);
            pawn.jobs.StartJob(wait, JobCondition.InterruptForced, null, resumeCurJobAfterwards: false);
        }
    }

    [HarmonyPatch]
    public static class Patch_Arachnoids_Downed_EnforceWait
    {
        // Private field accessors (RimWorld internals)
        private static readonly AccessTools.FieldRef<Pawn_HealthTracker, Pawn> HealthPawnRef =
            AccessTools.FieldRefAccess<Pawn_HealthTracker, Pawn>("pawn");

        private static readonly AccessTools.FieldRef<Pawn_JobTracker, Pawn> JobPawnRef =
            AccessTools.FieldRefAccess<Pawn_JobTracker, Pawn>("pawn");

        // 1) When pawn becomes downed, immediately force Wait_Downed
        [HarmonyPatch(typeof(Pawn_HealthTracker), "MakeDowned")]
        [HarmonyPostfix]
        public static void MakeDowned_Postfix(Pawn_HealthTracker __instance)
        {
            Pawn pawn = HealthPawnRef(__instance);
            if (!DownedJobUtility_Arachnoids.IsArachnoidPawn(pawn)) return;

            DownedJobUtility_Arachnoids.EnsureDownedWaitJob(pawn);
        }

        // 2) While downed, block job-finding and re-force Wait_Downed
        [HarmonyPatch(typeof(Pawn_JobTracker), "TryFindAndStartJob")]
        [HarmonyPrefix]
        public static bool TryFindAndStartJob_Prefix(Pawn_JobTracker __instance)
        {
            Pawn pawn = JobPawnRef(__instance);
            if (!DownedJobUtility_Arachnoids.IsArachnoidPawn(pawn)) return true;

            if (pawn != null && pawn.Downed && !pawn.Dead)
            {
                DownedJobUtility_Arachnoids.EnsureDownedWaitJob(pawn);
                return false; // skip vanilla job-finding
            }

            return true;
        }
    }
}