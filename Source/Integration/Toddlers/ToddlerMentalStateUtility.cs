using RimTalk_ToddlersExpansion.Core;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public static class ToddlerMentalStateUtility
	{
		private const string CryingDefName = "Crying";
		private const string GigglingDefName = "Giggling";

		public static bool HasBlockingMentalState(Pawn pawn)
		{
			if (pawn?.InMentalState != true)
			{
				return false;
			}

			return !IsNonBlockingBabyMentalState(pawn.MentalStateDef);
		}

		internal static bool IsNonBlockingBabyMentalState(MentalStateDef mentalStateDef)
		{
			return ToddlersExpansionSettings.EnableNonBlockingBabyFits
				&& mentalStateDef?.defName is CryingDefName or GigglingDefName;
		}
	}
}
