using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.YayoAnimation;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public sealed class JobDriver_ToddlerMutualPlayPartner : JobDriver
	{
		private const TargetIndex InitiatorInd = TargetIndex.A;
		private const int MaxPartnerDistance = 6;
		private const int PlayToilIndex = 1;
		private AnimationDef _playAnimation;
		private bool _restorePlayAnimation;

		private Pawn Initiator => TargetA.Thing as Pawn;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Defs.Look(ref _playAnimation, "playAnimation");
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				_restorePlayAnimation = CurToilIndex == PlayToilIndex;
			}
		}

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return true;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDestroyedOrNull(InitiatorInd);
			this.FailOn(() => pawn.Downed || pawn.Drafted || ToddlerMentalStateUtility.HasBlockingMentalState(pawn));
			this.FailOn(() => Initiator == null || Initiator.Downed || Initiator.Drafted || ToddlerMentalStateUtility.HasBlockingMentalState(Initiator));
			this.FailOn(() => Initiator?.Map != pawn.Map);

			Toil waitForInitiator = ToilMaker.MakeToil("WaitForInitiator");
			waitForInitiator.initAction = () =>
			{
				pawn.pather.StopDead();
			};
			waitForInitiator.tickAction = () =>
			{
				bool hasPlayCell = Initiator?.CurJob?.targetC.IsValid == true;
				if (!IsInitiatorCommitted(requireReachability: !hasPlayCell))
				{
					EndJobWith(JobCondition.Incompletable);
					return;
				}

				pawn.rotationTracker.FaceCell(Initiator.Position);

				bool ready = hasPlayCell
					? Initiator.jobs.curDriver is JobDriver_ToddlerMutualPlay { IsPlaying: true }
					: pawn.Position.InHorDistOf(Initiator.Position, 2);
				if (ready)
				{
					ReadyForNextToil();
				}
			};
			waitForInitiator.handlingFacing = true;
			waitForInitiator.defaultCompleteMode = ToilCompleteMode.Never;
			yield return waitForInitiator;

			Toil play = ToilMaker.MakeToil("ToddlerMutualPlayPartner");
			play.initAction = InitializePlayAnimation;
			play.tickAction = () =>
			{
				if (_restorePlayAnimation)
				{
					_restorePlayAnimation = false;
					InitializePlayAnimation();
				}
				else if (_playAnimation != null && pawn.Drawer?.renderer?.CurAnimation != _playAnimation)
				{
					ToddlerPlayAnimationUtility.TryApplyAnimation(pawn, _playAnimation);
				}

				if (!IsInitiatorCommitted(requireReachability: false))
				{
					EndJobWith(JobCondition.Incompletable);
					return;
				}

				if (!pawn.Position.InHorDistOf(Initiator.Position, MaxPartnerDistance))
				{
					EndJobWith(JobCondition.Incompletable);
					return;
				}

				pawn.rotationTracker.FaceCell(Initiator.Position);
				pawn.GainComfortFromCellIfPossible(1);

				if (!Initiator.CurJob.targetC.IsValid && SocialNeedTuning_Toddlers.IsMutualPlaySatisfied(pawn, Initiator))
				{
					EndJobWith(JobCondition.Succeeded);
				}
			};
			play.handlingFacing = true;
			play.defaultCompleteMode = ToilCompleteMode.Delay;
			play.defaultDuration = job.def.joyDuration;
			play.AddFinishAction(() => ToddlerPlayAnimationUtility.ClearAnimation(pawn, _playAnimation));

			yield return play;
		}

		private void InitializePlayAnimation()
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
				_playAnimation ??= ToddlerPlayAnimationUtility.GetSharedMutualPlayAnimation(pawn, Initiator);
			}

			ToddlerPlayAnimationUtility.TryApplyAnimation(pawn, _playAnimation);
		}

		private bool IsInitiatorCommitted(bool requireReachability)
		{
			if (Initiator == null)
			{
				return false;
			}

			Job initiatorJob = Initiator.CurJob;
			if (initiatorJob?.def != ToddlersExpansionJobDefOf.RimTalk_ToddlerMutualPlayJob)
			{
				return false;
			}

			if (initiatorJob.targetA.Thing != pawn)
			{
				return false;
			}

			if (requireReachability && !Initiator.CanReach(pawn, PathEndMode.Touch, Danger.Some))
			{
				return false;
			}

			return true;
		}
	}
}
