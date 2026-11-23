using HarmonyLib;
using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_ToddlerMapExit
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			var target = AccessTools.Method(typeof(JobGiver_ExitMapBest), "TryFindGoodExitDest",
				new[] { typeof(Pawn), typeof(bool), typeof(bool), typeof(IntVec3).MakeByRefType() });
			if (target != null)
			{
				harmony.Patch(target, prefix: new HarmonyMethod(typeof(Patch_ToddlerMapExit), nameof(TryFindGoodExitDest_Prefix)));
			}
		}

		private static bool TryFindGoodExitDest_Prefix(JobGiver_ExitMapBest __instance, Pawn pawn, ref IntVec3 spot, ref bool __result)
		{
			if (!IsAutonomousExitBranch(__instance) || ToddlerExitSettings.AllowsAutomaticExit(pawn))
			{
				return true;
			}

			spot = IntVec3.Invalid;
			__result = false;
			return false;
		}

		private static bool IsAutonomousExitBranch(ThinkNode node)
		{
			// The main humanlike and toddler trees use these conditions for their idle exit jobs.
			for (ThinkNode parent = node.parent; parent != null; parent = parent.parent)
			{
				if (parent is ThinkNode_ConditionalColonist { invert: true }
					|| parent is ThinkNode_ConditionalNonPlayerNonHostileFactionOrFactionless)
				{
					return true;
				}
			}

			return false;
		}
	}
}
