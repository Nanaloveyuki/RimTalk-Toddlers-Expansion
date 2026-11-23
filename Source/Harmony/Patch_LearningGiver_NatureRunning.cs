using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Harmony
{
    /// <summary>
    /// Patches for the NatureRunning learning activity.
    /// When a Child starts nature running, nearby children and toddlers will follow them.
    /// </summary>
    public static class Patch_LearningGiver_NatureRunning
    {
        /// <summary>
        /// Recruitment radius for finding nearby children to follow
        /// </summary>
        private const float RecruitmentRadius = 10f;
        
        /// <summary>
        /// Cached NatureRunning JobDef (looked up at runtime)
        /// </summary>
        private static JobDef _natureRunningJobDef;
        
        /// <summary>
        /// Get the NatureRunning JobDef
        /// </summary>
        private static JobDef NatureRunningJobDef
        {
            get
            {
                if (_natureRunningJobDef == null)
                {
                    _natureRunningJobDef = DefDatabase<JobDef>.GetNamedSilentFail("NatureRunning");
                }
                return _natureRunningJobDef;
            }
        }
        
        public static void Init(HarmonyLib.Harmony harmony)
        {
            var tryGiveJobTarget = AccessTools.Method(typeof(LearningGiver_NatureRunning), nameof(LearningGiver_NatureRunning.TryGiveJob));
            if (tryGiveJobTarget != null)
            {
                harmony.Patch(tryGiveJobTarget,
                    postfix: new HarmonyMethod(typeof(Patch_LearningGiver_NatureRunning), nameof(TryGiveJob_Postfix)));
            }
            else
            {
                Log.Warning("[RimTalk_ToddlersExpansion] Could not find LearningGiver_NatureRunning.TryGiveJob.");
            }
            
            // Also patch Pawn_JobTracker.StartJob to catch when the job actually starts
            var startJobTarget = AccessTools.Method(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob));
            if (startJobTarget != null)
            {
                harmony.Patch(startJobTarget,
                    postfix: new HarmonyMethod(typeof(Patch_LearningGiver_NatureRunning), nameof(StartJob_Postfix)));
            }
            
            var tryFindTarget = AccessTools.Method(typeof(NatureRunningUtility), nameof(NatureRunningUtility.TryFindNatureInterestTarget));
            if (tryFindTarget != null)
            {
                harmony.Patch(tryFindTarget,
                    prefix: new HarmonyMethod(typeof(Patch_LearningGiver_NatureRunning), nameof(TryFindNatureInterestTarget_Prefix)),
                    postfix: new HarmonyMethod(typeof(Patch_LearningGiver_NatureRunning), nameof(TryFindNatureInterestTarget_Postfix)));
            }
            else
            {
                Log.Warning("[RimTalk_ToddlersExpansion] Could not find NatureRunningUtility.TryFindNatureInterestTarget.");
            }

            var findInterestingThingTarget = AccessTools.Method(typeof(JobDriver_NatureRunning), "FindInterestingThing");
            if (findInterestingThingTarget != null)
            {
                harmony.Patch(findInterestingThingTarget,
                    postfix: new HarmonyMethod(typeof(Patch_LearningGiver_NatureRunning), nameof(FindInterestingThing_Postfix)));
            }
            else
            {
                Log.Warning("[RimTalk_ToddlersExpansion] Could not find JobDriver_NatureRunning.FindInterestingThing.");
            }

            var makeNewToilsTarget = AccessTools.Method(typeof(JobDriver_NatureRunning), "MakeNewToils");
            if (makeNewToilsTarget != null)
            {
                harmony.Patch(makeNewToilsTarget,
                    postfix: new HarmonyMethod(typeof(Patch_LearningGiver_NatureRunning), nameof(MakeNewToils_Postfix)));
            }
        }
        
        private static void TryGiveJob_Postfix(Pawn pawn, ref Job __result)
        {
            if (__result == null || NatureRunningJobDef == null || __result.def != NatureRunningJobDef)
            {
                return;
            }

            if (!NatureRunningSafetyUtility.IsSafeToStartOrContinue(pawn)
                || !NatureRunningSafetyUtility.IsSafeNatureRunningTarget(pawn, __result.targetA.Cell))
            {
                __result = null;
                return;
            }

            if (pawn == null || pawn.Map == null)
            {
                return;
            }

            // Keep vanilla scope: only player children initiate this behavior.
            if (!pawn.DevelopmentalStage.Child() || pawn.Faction != Faction.OfPlayer)
            {
                return;
            }

            NatureRunningDestinationUtility.TryAssignDestinationAndText(pawn, __result);
        }
        
        /// <summary>
        /// Called after a job is started - recruit nearby children if this is a NatureRunning job
        /// </summary>
        private static void StartJob_Postfix(Job newJob, Pawn ___pawn)
        {
            Pawn leader = ___pawn;
            if (newJob == null || NatureRunningJobDef == null || newJob.def != NatureRunningJobDef
                || leader?.Map == null || leader.CurJob != newJob)
            {
                return;
            }

            if (!NatureRunningSafetyUtility.IsSafeToStartOrContinue(leader)
                || !NatureRunningSafetyUtility.IsSafeNatureRunningTarget(leader, newJob.targetA.Cell))
            {
                leader.jobs.EndCurrentJob(JobCondition.Incompletable, startNewJob: true, canReturnToPool: true);
                return;
            }
            
            // Only children (not babies/toddlers) can be the leader of nature running
            if (!leader.DevelopmentalStage.Child())
            {
                return;
            }
            
            // Only player faction
            if (leader.Faction != Faction.OfPlayer)
            {
                return;
            }

            NatureRunningDestinationUtility.EnsureActivityText(newJob);
            
            if (ToddlersExpansionSettings.EnableNatureRunningFollowers)
            {
                RecruitNearbyFollowers(leader);
            }
        }
        
        /// <summary>
        /// Find and recruit nearby children and toddlers to follow the nature runner
        /// </summary>
        private static void RecruitNearbyFollowers(Pawn leader)
        {
            if (leader?.Map == null)
            {
                return;
            }
            
            // Find all nearby children and toddlers
            List<Pawn> nearbyChildren = new List<Pawn>();
            
            foreach (Pawn pawn in leader.Map.mapPawns.FreeColonistsSpawned)
            {
                if (pawn == leader)
                {
                    continue;
                }
                
                // Must be a child or toddler
                if (!IsChildOrToddler(pawn))
                {
                    continue;
                }
                
                // Must be within recruitment radius
                if (!pawn.Position.InHorDistOf(leader.Position, RecruitmentRadius))
                {
                    continue;
                }
                
                // Must be able to follow (not downed, not in mental state, etc.)
                if (!CanFollow(pawn))
                {
                    continue;
                }
                
                // Must not already be following someone
                if (IsAlreadyFollowing(pawn))
                {
                    continue;
                }
                
                nearbyChildren.Add(pawn);
            }
            
            // Give each nearby child the follow job
            foreach (Pawn follower in nearbyChildren)
            {
                GiveFollowJob(follower, leader);
            }
            
            if (nearbyChildren.Count > 0)
            {
                Log.Message($"[RimTalk_ToddlersExpansion] {leader.LabelShort} started nature running, {nearbyChildren.Count} children/toddlers are following.");
            }
        }
        
        /// <summary>
        /// Check if a pawn is a child or toddler
        /// </summary>
        private static bool IsChildOrToddler(Pawn pawn)
        {
            if (pawn.DevelopmentalStage.Child())
            {
                return true;
            }
            
            // Check for toddler (Baby stage + Toddlers mod)
            if (ToddlersCompatUtility.IsToddler(pawn))
            {
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Check if a pawn can follow (basic availability checks)
        /// </summary>
        private static bool CanFollow(Pawn pawn)
        {
            if (pawn.Downed || pawn.Dead || !pawn.Spawned)
            {
                return false;
            }
            
            if (ToddlerMentalStateUtility.HasBlockingMentalState(pawn))
            {
                return false;
            }
            
            if (pawn.IsBurning())
            {
                return false;
            }
            
            // Check if pawn is drafted (shouldn't happen for children, but just in case)
            if (pawn.Drafted)
            {
                return false;
            }
            
            // Check needs thresholds - don't recruit if already hungry or tired
            if (pawn.needs?.food != null && pawn.needs.food.CurLevelPercentage < 0.15f)
            {
                return false;
            }
            
            if (pawn.needs?.rest != null && pawn.needs.rest.CurLevelPercentage < 0.15f)
            {
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Check if pawn is already following someone (has our follow job)
        /// </summary>
        private static bool IsAlreadyFollowing(Pawn pawn)
        {
            Job curJob = pawn.CurJob;
            if (curJob == null)
            {
                return false;
            }
            
            // Check if already following someone
            if (curJob.def == ToddlersExpansionJobDefOf.RimTalk_FollowNatureRunner)
            {
                return true;
            }
            
            // Check if already nature running themselves
            if (NatureRunningJobDef != null && curJob.def == NatureRunningJobDef)
            {
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Give the follow job to a pawn
        /// </summary>
        private static void GiveFollowJob(Pawn follower, Pawn leader)
        {
            if (ToddlersExpansionJobDefOf.RimTalk_FollowNatureRunner == null)
            {
                Log.Error("[RimTalk_ToddlersExpansion] RimTalk_FollowNatureRunner JobDef not found!");
                return;
            }
            
            Job followJob = JobMaker.MakeJob(ToddlersExpansionJobDefOf.RimTalk_FollowNatureRunner, leader);
            followJob.locomotionUrgency = LocomotionUrgency.Jog;
            followJob.reportStringOverride = NatureRunningDestinationUtility.GetFollowReport(leader);
            
            // Start the job, interrupting current job
            follower.jobs.StartJob(followJob, JobCondition.InterruptForced);
        }
        
        private static void TryFindNatureInterestTarget_Postfix(Pawn searcher, ref LocalTargetInfo interestTarget, ref bool __result)
        {
            if (__result && !NatureRunningSafetyUtility.IsSafeNatureRunningTarget(searcher, interestTarget.Cell))
            {
                interestTarget = LocalTargetInfo.Invalid;
                __result = false;
            }
        }

        private static bool TryFindNatureInterestTarget_Prefix(Pawn searcher, ref LocalTargetInfo interestTarget, ref bool __result)
        {
            if (!NatureRunningSafetyUtility.IsSafeToStartOrContinue(searcher))
            {
                interestTarget = LocalTargetInfo.Invalid;
                __result = false;
                return false;
            }

            return true;
        }

        private static void FindInterestingThing_Postfix(Toil __result, JobDriver_NatureRunning __instance)
        {
            var findVanillaTarget = __result.initAction;
            __result.initAction = () =>
            {
                Pawn leader = __instance.pawn;
                Map map = leader.Map;
                if (map == null || !leader.DevelopmentalStage.Child() || leader.Faction != Faction.OfPlayer)
                {
                    findVanillaTarget();
                    return;
                }

                Job job = __instance.job;
                IntVec3 anchor = NatureRunningDestinationUtility.GetOrRestoreAnchor(job, map);
                if (!anchor.IsValid)
                {
                    findVanillaTarget();
                    return;
                }

                if (NatureRunningDestinationUtility.TryFindNextWaypoint(leader, map, anchor, out IntVec3 waypoint))
                {
                    job.SetTarget(TargetIndex.A, waypoint);
                }
                else
                {
                    __instance.EndJobWith(JobCondition.Incompletable);
                }
            };
        }

        private static IEnumerable<Toil> MakeNewToils_Postfix(IEnumerable<Toil> __result, JobDriver_NatureRunning __instance)
        {
            foreach (Toil toil in __result)
            {
                toil.AddFailCondition(() => !NatureRunningSafetyUtility.IsSafeToStartOrContinue(__instance.pawn));
                yield return toil;
            }
        }
    }
}
