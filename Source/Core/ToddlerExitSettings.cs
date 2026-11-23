using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimTalk_ToddlersExpansion.Core
{
	internal static class ToddlerExitSettings
	{
		public static readonly YoungPawnGroupSetting AllowMapExit = new(false, false, false, true);

		public static void ExposeData()
		{
			AllowMapExit.ExposeData("allowYoungPawnMapExit");
		}

		internal static bool AllowsAutomaticExit(Pawn pawn)
		{
			return !ToddlersCompatUtility.IsToddlerOrBaby(pawn)
				|| pawn.guest?.Released == true
				|| IsFormingPlayerCaravan(pawn)
				|| AllowMapExit.Allows(pawn);
		}

		internal static bool IsFormingPlayerCaravan(Pawn pawn)
		{
			return pawn.GetLord()?.LordJob is LordJob_FormAndSendCaravan;
		}
	}
}
