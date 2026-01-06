using System;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;

namespace Arachnoids
{
    public class HediffCompProperties_ArachnoidCocoonedProgress : HediffCompProperties
    {
        public float daysToArachnophobe = 3f;
        public float daysToStartEgging = 3f;
        public float eggIntervalDays = 2f;
        public int maxEggs = 6;

        public string eggHediffDef = "RJW_ArachnoidEgg";
        public string arachnophobeTraitDef = "Arachnoid_Arachnophobe";

        public HediffCompProperties_ArachnoidCocoonedProgress()
        {
            compClass = typeof(HediffComp_ArachnoidCocoonedProgress);
        }
    }

    public class HediffComp_ArachnoidCocoonedProgress : HediffComp
    {
        private const int IntervalTicks = 250; // building TickRare cadence and spawned fallback cadence

        private int ticksCocooned;
        private int ticksSinceLastEgg;
        private bool arachnophobeApplied;

        // New: to ensure the alert fires exactly once
        private bool eggingBeginsNotified;

        public HediffCompProperties_ArachnoidCocoonedProgress Props => (HediffCompProperties_ArachnoidCocoonedProgress)props;

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksCocooned, "ticksCocooned", 0);
            Scribe_Values.Look(ref ticksSinceLastEgg, "ticksSinceLastEgg", 0);
            Scribe_Values.Look(ref arachnophobeApplied, "arachnophobeApplied", false);
            Scribe_Values.Look(ref eggingBeginsNotified, "eggingBeginsNotified", false);
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            Pawn pawn = parent?.pawn;
            if (pawn == null || pawn.Dead) return;

            // Only advance here if pawn is actually spawned.
            // If contained in the cocoon building, the building calls NotifyInterval() directly.
            if (!pawn.Spawned) return;

            if (pawn.IsHashIntervalTick(IntervalTicks))
                NotifyInterval(IntervalTicks, pawn.Map, null);
        }

        /// <summary>
        /// Backward-compatible overload (used by some callers).
        /// </summary>
        public void NotifyInterval(int ticks, Map mapForImplanter)
        {
            NotifyInterval(ticks, mapForImplanter, null);
        }

        /// <summary>
        /// Called by the cocoon building every 250 ticks to ensure progression while pawn is contained.
        /// If messageLookTarget is provided, alerts can jump the camera to the cocoon.
        /// </summary>
        public void NotifyInterval(int ticks, Map mapForImplanter, Thing messageLookTarget)
        {
            Pawn pawn = parent?.pawn;
            if (pawn == null || pawn.Dead) return;

            ticksCocooned += ticks;

            TryUpdateSeverityVisual();

            float negDays = ArachnoidsMod.Settings?.cocoonNegativeEffectsDays ?? Props.daysToArachnophobe;

            // Fixed: 4 per day = every 0.25 days = 15,000 ticks
            const float FixedEggIntervalDays = 0.25f;

            int ticksToArachnophobe = (int)(negDays * 60000f);
            int ticksToEgging = (int)(negDays * 60000f);
            int eggIntervalTicks = (int)(FixedEggIntervalDays * 60000f);

            if (!arachnophobeApplied && ticksCocooned >= ticksToArachnophobe)
            {
                TryApplyArachnophobeTrait(pawn);
                arachnophobeApplied = true;
            }

            if (ticksCocooned >= ticksToEgging)
            {
                // Alert once when egging begins
                if (!eggingBeginsNotified)
                {
                    TrySendEggingBeginsMessage(pawn, messageLookTarget);
                    eggingBeginsNotified = true;
                }

                ticksSinceLastEgg += ticks;

                if (ticksSinceLastEgg >= eggIntervalTicks)
                {
                    ticksSinceLastEgg = 0;
                    TryImplantEgg(pawn, mapForImplanter);
                }
            }
        }

        private void TrySendEggingBeginsMessage(Pawn pawn, Thing messageLookTarget)
        {
            if (pawn == null || pawn.Dead) return;
            if (!pawn.RaceProps.Humanlike) return;
            if (!(pawn.IsColonist || pawn.IsPrisonerOfColony)) return;

            // NOTE: This requires the keyed string to exist. (File provided below.)
            try
            {
                string text = "Arachnoids_EggingBeginsMessage".Translate(pawn.LabelShortCap);

                // Prefer jumping to the cocoon building, if provided.
                if (messageLookTarget != null)
                {
                    Messages.Message(text, messageLookTarget, MessageTypeDefOf.NegativeEvent);
                    return;
                }

                // If pawn is spawned (rare for this hediff), target the pawn.
                if (pawn.Spawned)
                {
                    Messages.Message(text, pawn, MessageTypeDefOf.NegativeEvent);
                    return;
                }

                // Fallback: no look target.
                Messages.Message(text, MessageTypeDefOf.NegativeEvent);
            }
            catch
            {
                // If Translate fails (missing keyed file), silently do nothing.
                // (If you'd rather have a red error to catch missing keys, tell me and I'll change this.)
            }
        }

        private void TryUpdateSeverityVisual()
        {
            // Visual only: scale severity to 1.0 by daysToArachnophobe
            float negDays = ArachnoidsMod.Settings?.cocoonNegativeEffectsDays ?? Props.daysToArachnophobe;
            float denom = Math.Max(0.1f, negDays * 60000f);
            parent.Severity = Math.Min(1f, ticksCocooned / denom);
        }

        private void TryApplyArachnophobeTrait(Pawn pawn)
        {
            if (!pawn.RaceProps.Humanlike) return;
            if (pawn.story?.traits == null) return;

            TraitDef traitDef = DefDatabase<TraitDef>.GetNamedSilentFail(Props.arachnophobeTraitDef);
            if (traitDef == null) return;

            if (pawn.story.traits.HasTrait(traitDef)) return;

            pawn.story.traits.GainTrait(new Trait(traitDef, 0, forced: true));
        }

        private void TryImplantEgg(Pawn pawn, Map mapForImplanter)
        {
            if (pawn.health?.hediffSet == null) return;

            HediffDef eggDef = DefDatabase<HediffDef>.GetNamedSilentFail(Props.eggHediffDef);
            if (eggDef == null) return;

            // Cap egg count
            int existing = pawn.health.hediffSet.hediffs.Count(h => h?.def == eggDef);
            if (existing >= Props.maxEggs) return;

            // Prefer a sensible body part (Stomach). Fall back to whole body if not found.
            BodyPartRecord part = TryGetStomachPart(pawn);

            // Add the egg hediff on that part (if available)
            Hediff egg = HediffMaker.MakeHediff(eggDef, pawn, part);
            pawn.health.AddHediff(egg, part);

            // Initialize with an implanter (spider) and force fertilization.
            Pawn implanter = FindImplanterSpider(mapForImplanter ?? pawn.Map);
            TryInitImplanter(egg, implanter);
            TryForceFertilized(egg, implanter);
        }

        private BodyPartRecord TryGetStomachPart(Pawn pawn)
        {
            if (pawn?.RaceProps?.body == null) return null;

            BodyPartDef stomachDef = DefDatabase<BodyPartDef>.GetNamedSilentFail("Stomach");
            if (stomachDef == null) return null;

            var parts = pawn.RaceProps.body.AllParts;
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i]?.def == stomachDef)
                    return parts[i];
            }

            return null;
        }

        private Pawn FindImplanterSpider(Map map)
        {
            if (map?.mapPawns == null) return null;

            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p == null || p.Dead) continue;
                if (!p.Spawned) continue;

                if (p.kindDef != null && p.kindDef.defName == "Arachnoid_Spider")
                    return p;
            }

            return null;
        }

        private void TryInitImplanter(Hediff egg, Pawn implanter)
        {
            if (egg == null) return;

            try
            {
                MethodInfo mi = egg.GetType().GetMethod("InitImplanter",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (mi == null) return;

                ParameterInfo[] pars = mi.GetParameters();
                if (pars.Length == 1)
                    mi.Invoke(egg, new object[] { implanter });
                else if (pars.Length == 2)
                    mi.Invoke(egg, new object[] { implanter, true });
            }
            catch
            {
                // Silent: avoid log spam if RJW changes signatures
            }
        }

        private void TryForceFertilized(Hediff egg, Pawn implanter)
        {
            if (egg == null) return;

            Type t = egg.GetType();

            string[] methodNames =
            {
                "Fertilize",
                "MakeFertilized",
                "SetFertilized",
                "ForceFertilized"
            };

            foreach (string name in methodNames)
            {
                try
                {
                    MethodInfo mi = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (mi == null) continue;

                    ParameterInfo[] pars = mi.GetParameters();
                    if (pars.Length == 0)
                    {
                        mi.Invoke(egg, null);
                        return;
                    }
                    if (pars.Length == 1)
                    {
                        if (pars[0].ParameterType == typeof(Pawn))
                        {
                            mi.Invoke(egg, new object[] { implanter });
                            return;
                        }
                        if (pars[0].ParameterType == typeof(bool))
                        {
                            mi.Invoke(egg, new object[] { true });
                            return;
                        }
                    }
                }
                catch { }
            }

            try
            {
                FieldInfo f =
                    t.GetField("fertilized", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
                    t.GetField("isFertilized", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (f != null && f.FieldType == typeof(bool))
                {
                    f.SetValue(egg, true);
                    return;
                }

                PropertyInfo p =
                    t.GetProperty("Fertilized", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
                    t.GetProperty("IsFertilized", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (p != null && p.PropertyType == typeof(bool) && p.CanWrite)
                {
                    p.SetValue(egg, true);
                    return;
                }
            }
            catch { }
        }
    }
}