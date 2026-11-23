using System;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_BabyHairRendering
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			var graphicFor = AccessTools.Method(typeof(PawnRenderNode_Hair), nameof(PawnRenderNode_Hair.GraphicFor));
			if (graphicFor != null)
			{
				var postfix = new HarmonyMethod(typeof(Patch_BabyHairRendering), nameof(GraphicFor_Postfix)) { priority = Priority.Last };
				harmony.Patch(graphicFor, postfix: postfix);
			}

			var setFaction = AccessTools.Method(typeof(Pawn), nameof(Pawn.SetFaction), new[] { typeof(Faction), typeof(Pawn) });
			if (setFaction != null)
			{
				harmony.Patch(setFaction, postfix: new HarmonyMethod(typeof(Patch_BabyHairRendering), nameof(SetFaction_Postfix)));
			}

			var setGuestStatus = AccessTools.Method(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.SetGuestStatus), new[] { typeof(Faction), typeof(GuestStatus) });
			if (setGuestStatus != null)
			{
				harmony.Patch(setGuestStatus, postfix: new HarmonyMethod(typeof(Patch_BabyHairRendering), nameof(SetGuestStatus_Postfix)));
			}
		}

		private static void GraphicFor_Postfix(ref Graphic __result, Pawn pawn, PawnRenderNode_Hair __instance)
		{
			if (!BabyHairGameComponent.CanConfigureHair(pawn))
			{
				return;
			}

			bool? enabled = BabyHairGameComponent.GetOverride(pawn);
			if (enabled == false)
			{
				__result = null;
				return;
			}

			if (__result != null || !(enabled ?? BabyHairGameComponent.GroupDefaultEnablesHair(pawn)))
			{
				return;
			}

			HairDef hair = pawn.story?.hairDef;
			if (hair == null || hair.noGraphic)
			{
				return;
			}

			try
			{
				__result = hair.GraphicFor(pawn, __instance.ColorFor(pawn));
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[RimTalk_ToddlersExpansion] Error generating hair graphic for {pawn.LabelShort}: {ex.Message}", pawn.thingIDNumber ^ 0x12345678);
			}
		}

		private static void SetFaction_Postfix(Pawn __instance)
		{
			BabyHairGameComponent.RefreshPawn(__instance);
		}

		private static void SetGuestStatus_Postfix(Pawn ___pawn)
		{
			BabyHairGameComponent.RefreshPawn(___pawn);
		}
	}
}
