using System.Collections.Generic;
using System.Linq;
using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
    public class LordToilData_ChildrenOuting : LordToilData
    {
        public Dictionary<Pawn, int> presentForTicks = new Dictionary<Pawn, int>();
        
        public override void ExposeData()
        {
            Scribe_Collections.Look(ref presentForTicks, "presentForTicks", LookMode.Reference, LookMode.Value);
            if (presentForTicks == null)
            {
                presentForTicks = new Dictionary<Pawn, int>();
            }
        }
    }
    
    public class LordToil_ChildrenOuting : LordToil
    {
        protected IntVec3 spot;
        protected GatheringDef gatheringDef;
        
        private const float JoyPerTick = 3.5E-05f;
        
        private const float HungerExitThreshold = 0.1f;
        
        private const float RestExitThreshold = 0.1f;
        
        public LordToilData_ChildrenOuting Data => (LordToilData_ChildrenOuting)data;
        
        public LordToil_ChildrenOuting(IntVec3 spot, GatheringDef gatheringDef)
        {
            this.spot = spot;
            this.gatheringDef = gatheringDef;
            data = new LordToilData_ChildrenOuting();
        }
        
        public override ThinkTreeDutyHook VoluntaryJoinDutyHookFor(Pawn p)
        {
            if (gatheringDef?.duty != null)
            {
                return gatheringDef.duty.hook;
            }
            return ThinkTreeDutyHook.MediumPriority;
        }
        
        public override void UpdateAllDuties()
        {
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn pawn = lord.ownedPawns[i];
                if (gatheringDef?.duty != null)
                {
                    pawn.mindState.duty = new PawnDuty(gatheringDef.duty, spot);
                }
                else
                {
                    // Vanilla defines the Party duty without a DutyDefOf entry.
                	pawn.mindState.duty = new PawnDuty(ToddlersExpansionDutyDefOf.Party, spot);
                }
            }
        }
        
        public override void LordToilTick()
        {
            List<Pawn> ownedPawns = lord.ownedPawns;
            
            for (int i = ownedPawns.Count - 1; i >= 0; i--)
            {
                Pawn pawn = ownedPawns[i];
                
                if (GatheringsUtility.InGatheringArea(pawn.Position, spot, base.Map))
                {
                    if (!Data.presentForTicks.ContainsKey(pawn))
                    {
                        Data.presentForTicks.Add(pawn, 0);
                    }
                    Data.presentForTicks[pawn]++;
                    
                    pawn.needs?.joy?.GainJoy(JoyPerTick, JoyKindDefOf.Social);
                }
                
                if (Find.TickManager.TicksGame % 60 == 0)
                {
                    if (ShouldPawnExit(pawn))
                    {
                        lord.Notify_PawnLost(pawn, PawnLostCondition.LeftVoluntarily);
                    }
                }
            }
        }
        
        private bool ShouldPawnExit(Pawn pawn)
        {
            if (pawn.needs?.food != null && pawn.needs.food.CurLevelPercentage < HungerExitThreshold)
            {
                return true;
            }
            
            if (pawn.needs?.rest != null && pawn.needs.rest.CurLevelPercentage < RestExitThreshold)
            {
                return true;
            }
            
            if (pawn.Downed)
            {
                return true;
            }
            
            if (ToddlerMentalStateUtility.HasBlockingMentalState(pawn))
            {
                return true;
            }
            
            return false;
        }
    }
}
