using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Language
{
	public static class LanguageLevelUtility
	{
		private const string VersionKey = "lang1";
		private const float SingleWordProgress = 0.25f;
		private const float ShortPhraseProgress = 0.5f;
		private const float ClearSpeechProgress = 0.75f;
		private static readonly string[] TierKeys =
		{
			"babble",
			"words",
			"phrases",
			"clear"
		};

		private static readonly string[] PromptDescriptors =
		{
			"lang1:babble",
			"lang1:words",
			"lang1:phrases",
			"lang1:clear"
		};

		public static int GetLanguageTier(float progress01)
		{
			float clamped = Mathf.Clamp01(progress01);
			if (clamped < SingleWordProgress)
			{
				return 0;
			}

			if (clamped < ShortPhraseProgress)
			{
				return 1;
			}

			if (clamped < ClearSpeechProgress)
			{
				return 2;
			}

			return 3;
		}

		public static string GetTierKey(int tier)
		{
			if (tier < 0 || tier >= TierKeys.Length)
			{
				return VersionKey + ":babble";
			}

			return VersionKey + ":" + TierKeys[tier];
		}

		public static string GetPromptDescriptor(float progress01)
		{
			int tier = GetLanguageTier(progress01);
			return PromptDescriptors[tier];
		}

		public static bool TryGetLanguageProgress(Pawn pawn, out float progress01)
		{
			progress01 = 0f;
			if (pawn?.health?.hediffSet == null || ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning == null)
			{
				return false;
			}

			Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning);
			if (hediff == null)
			{
				return false;
			}

			progress01 = Mathf.Clamp01(hediff.Severity);
			return true;
		}

		public static bool TryGetOrCreateLanguageHediff(Pawn pawn, out Hediff language)
		{
			language = null;
			if (pawn?.health?.hediffSet == null || ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning == null)
			{
				return false;
			}

			language = pawn.health.hediffSet.GetFirstHediffOfDef(ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning);
			if (language == null)
			{
				language = HediffMaker.MakeHediff(ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning, pawn);
				pawn.health.AddHediff(language);
			}

			return language != null;
		}

		internal static void EnsureLanguageHediffForAge(Pawn pawn, bool resetExisting = false)
		{
			HediffDef def = ToddlersExpansionHediffDefOf.RimTalk_ToddlerLanguageLearning;
			if (def == null || pawn?.health?.hediffSet == null)
			{
				return;
			}

			Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(def);
			if ((existing != null && !resetExisting) || !TryGetToddlersLanguageInitialProgress(pawn, out float progress))
			{
				return;
			}

			if (existing != null)
			{
				existing.Severity = progress;
				return;
			}

			Hediff language = HediffMaker.MakeHediff(def, pawn);
			language.Severity = progress;
			pawn.health.AddHediff(language);
		}

		public static bool TryGetToddlersLanguageInitialProgress(Pawn pawn, out float progress01)
		{
			progress01 = 0f;
			if (pawn?.ageTracker == null)
			{
				return false;
			}

			if (ToddlersCompatUtility.IsToddlersActive && !ToddlersCompatUtility.IsToddler(pawn))
			{
				return false;
			}

			float days = ToddlerLearningSchedule.GetDaysSinceToddler(pawn);
			progress01 = ToddlersExpansionSettings.UseCustomLearningSchedule
				? GetCustomSchedule().GetProgress(days)
				: Mathf.Clamp01(days * GenDate.TicksPerDay * GetLearningPerBioTick(pawn));
			return true;
		}

		internal static float Advance(Pawn pawn, float progress, float biologicalTicks)
		{
			return ToddlersExpansionSettings.UseCustomLearningSchedule
				? GetCustomSchedule().Advance(progress, biologicalTicks)
				: progress + GetLearningPerBioTick(pawn) * biologicalTicks;
		}

		private static float GetLearningPerBioTick(Pawn pawn)
		{
			float minAge = ToddlersCompatUtility.GetToddlerMinAgeYears(pawn);
			float endAge = ToddlersCompatUtility.GetToddlerEndAgeYears(pawn);
			float stageTicks = Mathf.Max(1f, (endAge - minAge) * GenDate.DaysPerYear * GenDate.TicksPerDay);
			float duration = ToddlersExpansionSettings.learningFactor_Talking;
			return 1f / (stageTicks * (float.IsFinite(duration) ? Mathf.Clamp(duration, 0.1f, 3f) : 0.8f));
		}

		private static ToddlerLearningSchedule GetCustomSchedule() => new ToddlerLearningSchedule(
			ToddlersExpansionSettings.BabblingDays, SingleWordProgress, ToddlersExpansionSettings.SingleWordDays,
			ShortPhraseProgress, ToddlersExpansionSettings.ShortPhraseDays, ClearSpeechProgress);
	}
}
