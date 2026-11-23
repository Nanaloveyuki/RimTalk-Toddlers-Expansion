using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Core
{
	internal static class ToddlerFoodSettings
	{
		public static readonly YoungPawnGroupSetting SupplyFood = new(false, false, false, true);
		public static readonly YoungPawnGroupSetting FeedWhileCarried = new(false, false, false, true);
		public static readonly YoungPawnGroupSetting FeedInCaravan = new(false, false, false, false);

		public static void ExposeData()
		{
			SupplyFood.ExposeData("supplyYoungPawnFood");
			FeedWhileCarried.ExposeData("feedYoungPawnWhileCarried");
			FeedInCaravan.ExposeData("feedYoungPawnInCaravan");
		}

		public static bool AllowsOnPlayerHome(YoungPawnGroupSetting setting, Pawn pawn)
		{
			if (pawn?.Map?.IsPlayerHome != true || pawn.Dead || pawn.Destroyed)
			{
				return false;
			}

			YoungPawnGroup group = YoungPawnIdentity.GetGroup(pawn);
			return setting[group]
				&& (group != YoungPawnGroup.NonColonist
					|| pawn.Faction != null && !pawn.Faction.HostileTo(Faction.OfPlayer));
		}
	}
}
