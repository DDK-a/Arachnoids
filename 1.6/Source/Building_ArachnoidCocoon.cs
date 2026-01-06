using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Arachnoids
{
    public class Building_ArachnoidCocoon : Building, IThingHolder
    {
        private ThingOwner<Pawn> innerContainer;

        // TickRare runs every 250 ticks.
        private const int SustainIntervalTicks = 250;
        private const float TendQuality = 1.0f;

        // --- NEW: Cached graphics for early/late cocoon states ---
        private Graphic cachedGraphicEarly;
        private Graphic cachedGraphicLate;
        private string cachedTexPathEarly;
        private string cachedTexPathLate;

        public Building_ArachnoidCocoon()
        {
            innerContainer = new ThingOwner<Pawn>(this, oneStackOnly: true);
        }

        public ThingOwner GetDirectlyHeldThings() => innerContainer;

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public Pawn ContainedPawn => (innerContainer != null && innerContainer.Count > 0) ? innerContainer[0] : null;
        public bool HasPawn => ContainedPawn != null;

        // --- NEW: Dynamic graphic swap based on contained pawn cocoon severity ---
        public override Graphic Graphic
        {
            get
            {
                // If no def graphic data, fallback
                if (def?.graphicData == null)
                    return base.Graphic;

                // Resolve texture paths (early/late)
                ResolveCocoonTexPaths(out string early, out string late);

                // Ensure early cached graphic exists
                if (cachedGraphicEarly == null || cachedTexPathEarly != early)
                {
                    cachedTexPathEarly = early;
                    cachedGraphicEarly = GraphicDatabase.Get<Graphic_Single>(
                        early,
                        def.graphicData.shaderType.Shader,
                        def.graphicData.drawSize,
                        DrawColor,
                        DrawColorTwo
                    );
                }

                // Ensure late cached graphic exists (if provided)
                if (!late.NullOrEmpty() && (cachedGraphicLate == null || cachedTexPathLate != late))
                {
                    cachedTexPathLate = late;
                    cachedGraphicLate = GraphicDatabase.Get<Graphic_Single>(
                        late,
                        def.graphicData.shaderType.Shader,
                        def.graphicData.drawSize,
                        DrawColor,
                        DrawColorTwo
                    );
                }

                // Decide which to show
                if (ShouldUseLateGraphic() && cachedGraphicLate != null)
                    return cachedGraphicLate;

                return cachedGraphicEarly ?? base.Graphic;
            }
        }

        private void ResolveCocoonTexPaths(out string early, out string late)
        {
            early = def.graphicData.texPath;
            late = null;

            var ext = def.GetModExtension<DefModExt_CocoonGraphics>();
            if (ext != null)
            {
                if (!ext.texPathEarly.NullOrEmpty()) early = ext.texPathEarly;
                if (!ext.texPathLate.NullOrEmpty()) late = ext.texPathLate;
            }
        }

        private bool ShouldUseLateGraphic()
        {
            Pawn p = ContainedPawn;
            if (p == null || p.Dead) return false;

            HediffDef cocoonedDef = DefDatabase<HediffDef>.GetNamedSilentFail("Arachnoid_Cocooned");
            if (cocoonedDef == null) return false;

            Hediff h = p.health?.hediffSet?.GetFirstHediffOfDef(cocoonedDef);
            if (h == null) return false;

            return h.Severity >= 1f;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
        }

        public override string GetInspectString()
        {
            string baseStr = base.GetInspectString();
            Pawn p = ContainedPawn;

            if (p == null) return baseStr;

            string extra = $"Contains: {p.LabelShortCap}";
            return baseStr.NullOrEmpty() ? extra : baseStr + "\n" + extra;
        }

        public override void TickRare()
        {
            base.TickRare();

            Pawn p = ContainedPawn;
            if (p == null || p.Dead) return;

            SustainPawn(p);
        }

        private void SustainPawn(Pawn p)
        {
            // Ensure Cocooned hediff exists while contained
            Hediff cocooned = EnsureCocoonedHediff(p);

            // Keep hunger topped up
            try
            {
                if (p.needs?.food != null)
                    p.needs.food.CurLevel = p.needs.food.MaxLevel;
            }
            catch { }

            // Stop bleeding
            TryAutoTendBleeding(p);

            // Drive cocoon progression
            TryAdvanceCocoonProgress(p, cocooned);
        }

        private Hediff EnsureCocoonedHediff(Pawn p)
        {
            HediffDef cocooned = DefDatabase<HediffDef>.GetNamedSilentFail("Arachnoid_Cocooned");
            if (cocooned == null) return null;

            if (p.health?.hediffSet == null) return null;

            Hediff existing = p.health.hediffSet.GetFirstHediffOfDef(cocooned);
            if (existing == null)
            {
                existing = HediffMaker.MakeHediff(cocooned, p);
                p.health.AddHediff(existing);
            }

            return existing;
        }

        private void RemoveCocoonedHediff(Pawn p)
        {
            HediffDef cocooned = DefDatabase<HediffDef>.GetNamedSilentFail("Arachnoid_Cocooned");
            if (cocooned == null) return;

            Hediff h = p.health?.hediffSet?.GetFirstHediffOfDef(cocooned);
            if (h != null) p.health.RemoveHediff(h);
        }

        private void TryAutoTendBleeding(Pawn p)
        {
            if (p.health?.hediffSet == null) return;

            List<Hediff> hediffs = p.health.hediffSet.hediffs;
            if (hediffs == null) return;

            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_Injury inj)
                {
                    if (!inj.Bleeding) continue;

                    try
                    {
                        inj.Tended(TendQuality, TendQuality);
                    }
                    catch { }
                }
            }
        }

        private void TryAdvanceCocoonProgress(Pawn p, Hediff cocooned)
        {
            if (p == null || cocooned == null) return;

            if (cocooned is HediffWithComps hwc)
            {
                var comp = hwc.TryGetComp<HediffComp_ArachnoidCocoonedProgress>();
                if (comp != null)
                {
                    // NOTE: If you’ve already implemented the “lookTarget = this” version,
                    // keep that call here. If not, this still works.
                    comp.NotifyInterval(SustainIntervalTicks, Map, this);
                }
            }
        }

        public bool TryAcceptPawn(Pawn p)
        {
            if (p == null || p.Dead) return false;
            if (HasPawn) return false;

            if (p.Spawned) p.DeSpawn();
            innerContainer.TryAdd(p, canMergeWithExistingStacks: false);

            SustainPawn(p);
            return true;
        }

        public Pawn ReleasePawn()
        {
            Pawn p = ContainedPawn;
            if (p == null) return null;

            innerContainer.Remove(p);
            RemoveCocoonedHediff(p);

            IntVec3 drop = InteractionCell;
            if (!drop.InBounds(Map) || !drop.Standable(Map))
                drop = Position;

            GenSpawn.Spawn(p, drop, Map, WipeMode.Vanish);

            if (!p.Downed)
                p.stances?.stunner?.StunFor(180, this);

            return p;
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (HasPawn && Map != null)
            {
                try { ReleasePawn(); } catch { }
            }
            base.Destroy(mode);
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (var opt in base.GetFloatMenuOptions(selPawn))
                yield return opt;

            if (selPawn == null || selPawn.Map != Map) yield break;
            if (!selPawn.IsColonistPlayerControlled) yield break;
            if (!HasPawn) yield break;

            JobDef rescueDef = DefDatabase<JobDef>.GetNamedSilentFail("Arachnoid_RescueFromCocoon");
            if (rescueDef == null) yield break;

            yield return new FloatMenuOption("Rescue from cocoon", () =>
            {
                Job job = JobMaker.MakeJob(rescueDef, this);
                selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            });
        }
    }
}