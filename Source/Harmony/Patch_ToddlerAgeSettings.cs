using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_ToddlerAgeSettings
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			MethodInfo toddlerMinAge = AccessTools.Method("Toddlers.ToddlerUtility:ToddlerMinAge", new[] { typeof(Pawn) });
			if (toddlerMinAge?.ReturnType == typeof(float))
			{
				harmony.Patch(toddlerMinAge, prefix: new HarmonyMethod(typeof(Patch_ToddlerAgeSettings), nameof(ToddlerMinAge_Prefix)));
			}

			var reapplyAge = new HarmonyMethod(typeof(Patch_ToddlerAgeSettings), nameof(ReapplyConfiguredAge)) { priority = Priority.Last };
			var harCompat = AccessTools.TypeByName("Toddlers.HARCompat");
			if (harCompat != null && AccessTools.DeclaredMethod(harCompat, "Init", System.Type.EmptyTypes) is MethodInfo initializeHar)
			{
				harmony.Patch(initializeHar, postfix: reapplyAge);
			}

			// Loaded pawns recalculate their life stage during deserialization.
			harmony.Patch(AccessTools.DeclaredMethod(typeof(Game), nameof(Game.LoadGame)), prefix: reapplyAge);

			MethodInfo recalculateLifeStageIndex = AccessTools.Method(typeof(Pawn_AgeTracker), "RecalculateLifeStageIndex");
			if (recalculateLifeStageIndex == null)
			{
				return;
			}

			HarmonyMethod transpiler = null;
			if (ToddlerAgeSettingsUtility.IsRatkinToddlerAgeAdjustmentLoaded())
			{
				Log.Message("[RimTalk_ToddlersExpansion][ToddlerAge] RatkinToddlerAgeAdjustment detected; skipping duplicate RecalculateLifeStageIndex float-age transpiler.");
			}
			else
			{
				transpiler = new HarmonyMethod(typeof(Patch_ToddlerAgeSettings), nameof(RecalculateLifeStageIndex_Transpiler));
			}

			harmony.Patch(recalculateLifeStageIndex,
				prefix: new HarmonyMethod(typeof(Patch_ToddlerAgeSettings), nameof(RecalculateLifeStageIndex_Prefix)),
				postfix: new HarmonyMethod(typeof(Patch_ToddlerAgeSettings), nameof(RecalculateLifeStageIndex_Postfix)),
				transpiler: transpiler);
		}

		private static void ReapplyConfiguredAge() => ToddlerAgeSettingsUtility.ApplyConfiguredToddlerAge(forceReapply: true);

		private static bool ToddlerMinAge_Prefix(Pawn __0, ref float __result)
		{
			if (!ToddlerAgeSettingsUtility.TryGetConfiguredToddlerMinAge(__0, out float minAge))
			{
				return true;
			}

			__result = minAge;
			return false;
		}

		private static void RecalculateLifeStageIndex_Prefix(int ___cachedLifeStageIndex, out int __state)
		{
			__state = ___cachedLifeStageIndex;
		}

		private static void RecalculateLifeStageIndex_Postfix(Pawn ___pawn, int ___cachedLifeStageIndex, int __state)
		{
			if (___cachedLifeStageIndex != __state)
			{
				ToddlerSelfBathGameComponent.NotifyPawnEligibilityChanged(___pawn);
			}
		}

		private static IEnumerable<CodeInstruction> RecalculateLifeStageIndex_Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
			MethodInfo ageYearsGetter = AccessTools.PropertyGetter(typeof(Pawn_AgeTracker), nameof(Pawn_AgeTracker.AgeBiologicalYears));
			MethodInfo ageYearsFloatGetter = AccessTools.PropertyGetter(typeof(Pawn_AgeTracker), nameof(Pawn_AgeTracker.AgeBiologicalYearsFloat));
			int replacements = 0;

			for (int i = 0; i < codes.Count; i++)
			{
				if (codes[i].Calls(ageYearsGetter) && i + 1 < codes.Count && codes[i + 1].opcode == OpCodes.Conv_R4)
				{
					codes[i].operand = ageYearsFloatGetter;
					codes[i + 1].opcode = OpCodes.Nop;
					replacements++;
				}
			}

			if (replacements == 0)
			{
				Log.Warning("[RimTalk_ToddlersExpansion][ToddlerAge] Pawn_AgeTracker.RecalculateLifeStageIndex transpiler found no AgeBiologicalYears call; runtime age fallback remains active.");
			}
			else
			{
				Log.Message($"[RimTalk_ToddlersExpansion][ToddlerAge] Patched RecalculateLifeStageIndex to use float biological age ({replacements} replacement(s)).");
			}

			return codes;
		}

	}
}
