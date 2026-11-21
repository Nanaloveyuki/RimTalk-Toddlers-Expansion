using System;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.RimTalk;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_RimTalkContextBuilder
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			if (!RimTalkCompatUtility.IsRimTalkActive)
			{
				return;
			}

			Type promptServiceType = AccessTools.TypeByName("RimTalk.Service.PromptService");
			if (promptServiceType == null)
			{
				return;
			}

			MethodInfo pawnContextTarget = FindPawnContextTarget(promptServiceType);
			if (IsStringContextTarget(pawnContextTarget))
			{
				MethodInfo postfix = AccessTools.Method(typeof(Patch_RimTalkContextBuilder), nameof(CreatePawnContext_Postfix));
				harmony.Patch(pawnContextTarget, postfix: new HarmonyMethod(postfix));
			}

			MethodInfo listenerContextTarget = AccessTools.Method(promptServiceType, "CreateMinimalListenerContext", new[] { typeof(Pawn) });
			if (IsStringContextTarget(listenerContextTarget))
			{
				MethodInfo listenerPostfix = AccessTools.Method(typeof(Patch_RimTalkContextBuilder), nameof(CreateMinimalListenerContext_Postfix));
				harmony.Patch(listenerContextTarget, postfix: new HarmonyMethod(listenerPostfix));
			}
		}

		private static MethodInfo FindPawnContextTarget(Type promptServiceType)
		{
			Type infoLevelType = promptServiceType.GetNestedType("InfoLevel", BindingFlags.Public | BindingFlags.NonPublic);
			MethodInfo target = infoLevelType == null
				? null
				: AccessTools.Method(promptServiceType, "CreatePawnContext", new[] { typeof(Pawn), infoLevelType });
			return target ?? AccessTools.Method(promptServiceType, "CreatePawnContext", new[] { typeof(Pawn) });
		}

		private static bool IsStringContextTarget(MethodInfo target)
		{
			return target?.ReturnType == typeof(string) && !target.ContainsGenericParameters;
		}

		private static void CreatePawnContext_Postfix(Pawn __0, ref string __result)
		{
			if (__0 == null)
			{
				return;
			}

			__result = ToddlerContextInjector.InjectToddlerLanguageContext(__result, __0);
		}

		private static void CreateMinimalListenerContext_Postfix(Pawn __0, ref string __result)
		{
			if (__0 == null)
			{
				return;
			}

			__result = ToddlerContextInjector.InjectToddlerListenerContext(__result, __0);
		}
	}
}
