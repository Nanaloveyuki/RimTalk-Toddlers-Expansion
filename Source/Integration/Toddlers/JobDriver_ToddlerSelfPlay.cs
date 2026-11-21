using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.RimTalk;
using RimTalk_ToddlersExpansion.Integration.YayoAnimation;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public sealed class JobDriver_ToddlerSelfPlay : JobDriver
	{
		private const TargetIndex PlaySpotInd = TargetIndex.A;
		private const int PlayToilIndex = 1;
		private AnimationDef _playAnimation;
		private bool _restorePlayPresentation;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Defs.Look(ref _playAnimation, "playAnimation");
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				_restorePlayPresentation = CurToilIndex == PlayToilIndex;
			}
		}

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			LocalTargetInfo playSpot = job.GetTarget(PlaySpotInd);
			if (!pawn.CanReach(playSpot, PathEndMode.OnCell, Danger.Some))
			{
				ToddlerPlayFailureCooldownUtility.StartSelfPlayCooldown(pawn);
				return false;
			}

			bool reserved = pawn.Reserve(playSpot, job, 1, -1, null, errorOnFailed);
			if (!reserved)
			{
				ToddlerPlayFailureCooldownUtility.StartSelfPlayCooldown(pawn);
			}

			return reserved;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOn(() => pawn.Downed || pawn.Drafted || ToddlerMentalStateUtility.HasBlockingMentalState(pawn));

			Toil goToSpot = Toils_Goto.GotoCell(PlaySpotInd, PathEndMode.OnCell);
			goToSpot.AddPreInitAction(() => job.reportStringOverride = "RimTalk_ToddlersExpansion_PlayReport_SelfMoving".Translate());
			yield return goToSpot;

			Toil play = ToilMaker.MakeToil("ToddlerSelfPlay");
			play.initAction = () =>
			{
				job.reportStringOverride = null;
				InitializePlayPresentation();
			};
			play.tickIntervalAction = delta =>
			{
				if (_restorePlayPresentation)
				{
					_restorePlayPresentation = false;
					InitializePlayPresentation();
				}
				else if (_playAnimation != null && pawn.Drawer?.renderer?.CurAnimation != _playAnimation)
				{
					ToddlerPlayAnimationUtility.TryApplyAnimation(pawn, _playAnimation);
				}

				ToddlerPlayEffectUtility.TryTriggerGigglingEffect(pawn);
				if (ToddlerCareEventUtility.TryTriggerSelfPlayMishap(pawn, delta))
				{
					return;
				}

				if (UsesGenericSelfPlayReport(job))
				{
					ToddlerPlayReportUtility.TryApplyPendingReport(job);
				}
				pawn.GainComfortFromCellIfPossible(delta);
				SocialNeedTuning_Toddlers.ApplySelfPlayTickEffects(pawn, delta);
				if (SocialNeedTuning_Toddlers.IsPlayNeedSatisfied(pawn))
				{
					EndJobWith(JobCondition.Succeeded);
				}
			};
			play.handlingFacing = true;
			play.defaultCompleteMode = ToilCompleteMode.Delay;
			play.defaultDuration = job.def.joyDuration;

			play.AddFinishAction(() =>
			{
				ToddlerPlayAnimationUtility.ClearAnimation(pawn, _playAnimation);
			});

			AddFinishAction(condition =>
			{
				ToddlerPlayReportUtility.CancelJob(job);
				if (condition != JobCondition.Succeeded || CurToilIndex < PlayToilIndex)
				{
					ToddlerPlayFailureCooldownUtility.StartSelfPlayCooldown(pawn);
					return;
				}

				ToddlerPlayDialogueEvents.OnToddlerSelfPlayCompleted(pawn, job, Map);
			});

			yield return play;
		}

		private void InitializePlayPresentation()
		{
			if (YayoAnimationCompatUtility.TryGetNativePlayAnimationOverride(pawn, out AnimationDef nativeAnimation))
			{
				_playAnimation = nativeAnimation;
			}
			else if (YayoAnimationCompatUtility.ShouldUseYayoPlayAnimation(pawn))
			{
				_playAnimation = null;
			}
			else
			{
				_playAnimation ??= ToddlerPlayAnimationUtility.GetRandomSelfPlayAnimation(pawn);
			}

			ToddlerPlayAnimationUtility.TryApplyAnimation(pawn, _playAnimation);
			if (UsesGenericSelfPlayReport(job))
			{
				ToddlerPlayReportUtility.EnsureReportRequested(job, pawn, null, ToddlerPlayReportKind.SelfPlay);
				ToddlerPlayReportUtility.TryApplyPendingReport(job);
			}
		}

		private static bool UsesGenericSelfPlayReport(Job job)
		{
			return job?.def == ToddlersExpansionJobDefOf.RimTalk_ToddlerSelfPlayJob;
		}
	}
}
