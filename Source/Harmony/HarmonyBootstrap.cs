using System;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.RimTalk;
using RimTalk_ToddlersExpansion.Integration.YayoAnimation;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class HarmonyBootstrap
	{
		private static bool _initialized;

		public static void Init()
		{
			if (_initialized)
			{
				return;
			}

			_initialized = true;
			var harmony = new HarmonyLib.Harmony("cj.rimtalk.toddlers");
			Patch_ModLogFiltering.Init(harmony);
			ApplyRimTalkPatches(harmony);
			Patch_ToddlersWashBaby.Init(harmony);
			Patch_ToddlersWashBabyBathRules.Init(harmony);
			Patch_ToddlersLearningSync.Init(harmony);
			Patch_ToddlerAgeSettings.Init(harmony);
			Patch_ToddlersPlayInCribReservation.Init(harmony);
			Patch_BiotechSharedBedroomThoughts.Init(harmony);
			Patch_PawnGroupMakerUtility.Init(harmony);
			Patch_PawnBioAndNameGenerator_ChildhoodFallback.Init(harmony);
			Patch_FloatMenu_ToddlerToyPlay.Init(harmony);
			Patch_TravelingLord.Init(harmony);
			Patch_CarriedToddlerMapEdgeReachability.Init(harmony);
			Patch_ExitMapDuty.Init(harmony);
			
			Patch_ToddlerPrisonerThinkTree.Init(harmony);
			Patch_ToddlerPrisonerApparel.Init(harmony);
			Patch_YoungPawnCombatSafety.Init(harmony);
			Patch_ToddlerPlaySafety.Init(harmony);
			Patch_LearningGiver_NatureRunning.Init(harmony);
			Patch_ToddlerJobLogging.Init(harmony);
			Patch_ToddlerBathRendering.Init(harmony);
			
			Patch_ToddlerCarrying.Init(harmony);
			Patch_ToddlerCarriedDamageFactor.Init(harmony);
			Patch_ToddlersLocomotionAnimation.Init(harmony);
			Patch_BePlayedWithJobSafety.Init(harmony);
			Patch_ToddlerLonelyPlay.Init(harmony);
			
			Patch_ToddlerBoredom.ApplyPatches(harmony);
			Patch_ToddlerEatingSpeed.Init(harmony);
			Patch_ToddlerEatingSafety.Init(harmony);
			Patch_ThinkNode_ConditionalHasLordDuty.Init(harmony);
			Patch_KiiroRefugeeBabyStay.Init(harmony);
			Patch_IgnoreToddlerMentalStates.Init(harmony);
			Patch_IgnoreToddlerStripGoodwill.Init(harmony);
			Patch_BabyCryMoodOnly.Init(harmony);
			Patch_AdoptableFriendlyBaby.Init(harmony);
			Patch_ApparelGraphicRecordGetter_BabyFallback.Init(harmony);
			Patch_ToddlersRemoveClothes.Init(harmony);
			ApplyOptionalPatch(harmony, Patch_HospitalityBedUtilityIdeology.Init);
			
			Patch_BabyHairRendering.Init(harmony);
			
			YayoAnimationCompatUtility.Initialize();
			YayoAnimationCompatUtility.ApplyPatches(harmony);
			
			Patch_VisitorToddlerBabyFood.Init(harmony);
			
			Log.Message("[RimTalk Toddlers Expansion] Patch initialization finished");
		}

		private static void ApplyRimTalkPatches(HarmonyLib.Harmony harmony)
		{
			if (!RimTalkCompatUtility.IsRimTalkActive)
			{
				return;
			}

			ApplyOptionalPatch(harmony, Patch_RimTalkContextBuilder.Init);
			ApplyOptionalPatch(harmony, Patch_RimTalkDecorateTextCompatibility.Init);
			ApplyOptionalPatch(harmony, Patch_RimTalkTalkService.Init);
			ApplyOptionalPatch(harmony, Patch_RimTalkPersona.Init);
		}

		private static void ApplyOptionalPatch(HarmonyLib.Harmony harmony, Action<HarmonyLib.Harmony> init)
		{
			try
			{
				init(harmony);
			}
			catch (Exception ex)
			{
				Log.Warning($"[RimTalk Toddlers Expansion] Could not initialize {init.Method.DeclaringType?.Name}: {ex}");
			}
		}
	}
}
