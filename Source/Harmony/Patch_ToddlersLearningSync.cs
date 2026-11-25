using System;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimTalk_ToddlersExpansion.Language;
using RimWorld;
using Verse;
using global::Toddlers;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_ToddlersLearningSync
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			Type learningUtilityType = AccessTools.TypeByName("Toddlers.ToddlerLearningUtility");
			if (learningUtilityType == null)
			{
				return;
			}

			MethodInfo resetForAge = AccessTools.Method(learningUtilityType, "ResetHediffsForAge", new[] { typeof(Pawn), typeof(bool) })
				?? AccessTools.Method(learningUtilityType, "ResetHediffsForAge", new[] { typeof(Pawn) });
			if (resetForAge != null)
			{
				bool hasClearExisting = resetForAge.GetParameters().Length == 2;
				harmony.Patch(resetForAge,
					prefix: hasClearExisting ? new HarmonyMethod(typeof(Patch_ToddlersLearningSync), nameof(CaptureNativeProgress)) : null,
					postfix: new HarmonyMethod(typeof(Patch_ToddlersLearningSync), hasClearExisting
						? nameof(ResetHediffsForAge_WithFlag_Postfix) : nameof(ResetHediffsForAge_Postfix)));
			}

			Type learningHediffType = AccessTools.TypeByName("Toddlers.Hediff_ToddlerLearning");
			MethodInfo innerTick = AccessTools.Method(learningHediffType, "InnerTick", new[] { typeof(float) });
			if (innerTick != null)
			{
				harmony.Patch(innerTick, prefix: new HarmonyMethod(typeof(Patch_ToddlersLearningSync), nameof(AdjustLearningInterval)));
			}
		}

		private static void AdjustLearningInterval(Hediff __instance, ref float __0)
		{
			if (!ToddlerLearningSchedule.TryGetNativeSchedule(__instance.def, out ToddlerLearningSchedule schedule))
			{
				return;
			}

			float nativeFactor = __instance.def == Toddlers_DefOf.LearningToWalk
				? Toddlers_Settings.learningFactor_Walk : Toddlers_Settings.learningFactor_Manipulation;
			float nativeRate = ToddlerLearningUtility.GetLearningPerBioTick(__instance.pawn) / nativeFactor;
			if (float.IsFinite(nativeRate) && nativeRate > 0f)
			{
				// Retain Toddlers' stage callbacks and growth-vat learning path.
				__0 = (schedule.Advance(__instance.Severity, __0) - __instance.Severity) / nativeRate;
			}
		}

		private static void CaptureNativeProgress(Pawn __0, bool __1, out (float Walk, float Manipulation) __state)
		{
			__state = default;
			if (!__1 && ToddlersExpansionSettings.UseCustomLearningSchedule && __0?.health?.hediffSet is HediffSet hediffs)
			{
				__state.Walk = hediffs.GetFirstHediffOfDef(Toddlers_DefOf.LearningToWalk)?.Severity ?? 0f;
				__state.Manipulation = hediffs.GetFirstHediffOfDef(Toddlers_DefOf.LearningManipulation)?.Severity ?? 0f;
			}
		}

		private static void ResetHediffsForAge_WithFlag_Postfix(Pawn __0, bool __1, (float Walk, float Manipulation) __state) =>
			SyncLearningForReset(__0, __1, __state);

		private static void ResetHediffsForAge_Postfix(Pawn __0) => SyncLearningForReset(__0, clearExisting: true, default);

		private static void SyncLearningForReset(Pawn pawn, bool clearExisting, (float Walk, float Manipulation) previous)
		{
			if (!ToddlersCompatUtility.IsToddler(pawn) || pawn.health?.hediffSet == null)
			{
				return;
			}

			if (ToddlersExpansionSettings.UseCustomLearningSchedule)
			{
				float days = ToddlerLearningSchedule.GetDaysSinceToddler(pawn);
				ApplyNativeProgress(pawn, Toddlers_DefOf.LearningToWalk, days, previous.Walk);
				ApplyNativeProgress(pawn, Toddlers_DefOf.LearningManipulation, days, previous.Manipulation);
			}

			HediffDef babblingDef = ToddlersExpansionHediffDefOf.RimTalk_BabyBabbling;
			if (babblingDef != null && pawn.health.hediffSet.GetFirstHediffOfDef(babblingDef) is Hediff babbling)
			{
				pawn.health.RemoveHediff(babbling);
			}

			LanguageLevelUtility.EnsureLanguageHediffForAge(pawn, clearExisting);
		}

		private static void ApplyNativeProgress(Pawn pawn, HediffDef def, float days, float priorProgress)
		{
			if (ToddlerLearningSchedule.TryGetNativeSchedule(def, out ToddlerLearningSchedule schedule)
				&& pawn.health.hediffSet.GetFirstHediffOfDef(def) is Hediff learning)
			{
				learning.Severity = Math.Max(priorProgress, schedule.GetProgress(days));
			}
		}
	}
}
