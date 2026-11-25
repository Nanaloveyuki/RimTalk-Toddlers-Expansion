using System.Collections.Generic;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using Toddlers;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Harmony
{
	internal static class Patch_ToddlerCribReturn
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			var bestPlace = AccessTools.Method(typeof(BabyMoveUtility), nameof(BabyMoveUtility.BestPlaceForBaby),
				new[] { typeof(Pawn), typeof(Pawn), typeof(BabyMoveReason).MakeByRefType(), typeof(bool) });
			harmony.Patch(bestPlace, prefix: new HarmonyMethod(typeof(Patch_ToddlerCribReturn), nameof(BestPlaceForBaby_Prefix)));

			var cribReturn = AccessTools.TypeByName("ToddlersPlayExpanded.MapComponent_ToddlerCribReturn");
			var findToddler = cribReturn == null ? null : AccessTools.Method(cribReturn, "FindBestToddler", new[] { typeof(Pawn), typeof(List<Pawn>) });
			if (findToddler != null)
			{
				harmony.Patch(findToddler, prefix: new HarmonyMethod(typeof(Patch_ToddlerCribReturn), nameof(FindBestToddler_Prefix)));
			}
		}

		private static bool BestPlaceForBaby_Prefix(Pawn baby, Pawn hauler, ref BabyMoveReason moveReason, ref LocalTargetInfo __result)
		{
			// The automatic scan passes None. Manual destination lookups start with Undetermined.
			Job job = hauler.CurJob;
			bool automaticMove = moveReason == BabyMoveReason.None
				|| (job is { playerForced: false } && job.def == JobDefOf.BringBabyToSafetyUnforced && job.targetA.Pawn == baby);
			if (!automaticMove || hauler.Faction?.IsPlayer != true || !ShouldSkipCribReturn(baby))
			{
				return true;
			}

			moveReason = BabyMoveReason.None;
			__result = LocalTargetInfo.Invalid;
			return false;
		}

		private static void FindBestToddler_Prefix(List<Pawn> __1)
		{
			__1.RemoveAll(ShouldSkipCribReturn);
		}

		private static bool ShouldSkipCribReturn(Pawn baby)
		{
			return baby.Spawned
				&& (baby.Faction?.IsPlayer == true || baby.IsPrisonerOfColony)
				&& ToddlersExpansionSettings.SkipSafeToddlerCribReturn.Allows(baby)
				&& ToddlersCompatUtility.IsToddler(baby)
				&& !baby.Downed
				&& baby.health.capacities.CapableOf(PawnCapacityDefOf.Moving)
				&& !HealthAIUtility.ShouldSeekMedicalRest(baby)
				&& !baby.Position.IsForbidden(baby)
				&& baby.SafeTemperatureAtCell(baby.Position, baby.Map);
		}
	}
}
