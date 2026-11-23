using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public static class Caravan_NeedsTracker_ToddlerFeeding_Patch
	{
		private const string ToddlerLearningUtilityTypeName = "Toddlers.ToddlerLearningUtility";

		private static bool _utilityTypesInitialized;
		private static Type _toddlerLearningUtilityType;
		private static MethodInfo _canFeedSelfMethod;

		public static void Init(HarmonyLib.Harmony harmony)
		{
			MethodInfo target = AccessTools.Method(typeof(Caravan_NeedsTracker), nameof(Caravan_NeedsTracker.TrySatisfyPawnsNeeds), new[] { typeof(int) });
			if (target != null)
			{
				harmony.Patch(target, postfix: new HarmonyMethod(typeof(Caravan_NeedsTracker_ToddlerFeeding_Patch), nameof(Postfix)));
			}
		}

		private static void Postfix(Caravan_NeedsTracker __instance)
		{
			try
			{
				if (__instance?.caravan == null || __instance.caravan.Faction != Faction.OfPlayer)
				{
					return;
				}

				// 初始化反射类型
				if (!_utilityTypesInitialized)
				{
					InitializeUtilityTypes();
				}

				// 尝试处理所有toddlers的喂食
				TryFeedAllToddlers(__instance);
			}
			catch (Exception ex)
			{
				Log.Warning($"[RimTalk_ToddlersExpansion] 在Toddler喂食补丁中出现错误: {ex.Message}");
			}
		}

		private static void InitializeUtilityTypes()
		{
			_utilityTypesInitialized = true;

			_toddlerLearningUtilityType = AccessTools.TypeByName(ToddlerLearningUtilityTypeName);
			if (_toddlerLearningUtilityType != null)
			{
				_canFeedSelfMethod = AccessTools.Method(_toddlerLearningUtilityType, "CanFeedSelf", new[] { typeof(Pawn) });
			}
		}

		private static void TryFeedAllToddlers(Caravan_NeedsTracker needsTracker)
		{
			List<Pawn> pawnsListForReading = needsTracker.caravan.PawnsListForReading;

			for (int i = 0; i < pawnsListForReading.Count; i++)
			{
				Pawn pawn = pawnsListForReading[i];
				if (pawn.Dead || !ToddlersCompatUtility.IsToddler(pawn) || pawn.needs?.food == null
					|| !ToddlerFoodSettings.FeedInCaravan.Allows(pawn))
				{
					continue;
				}

				Need_Food food = pawn.needs.food;

				// 如果toddler不饿或者已经吃饱，跳过
				if (food.CurCategory < HungerCategory.Hungry || food.CurLevelPercentage >= 0.9f)
				{
					continue;
				}

				// 如果能自主进食，跳过（让原方法处理）
				if (CanToddlerFeedSelf(pawn))
				{
					continue;
				}

				TryFeedToddlerWithAssistance(needsTracker, pawn, food);
			}
		}

		private static bool CanToddlerFeedSelf(Pawn toddler)
		{
			// 如果无法获取CanFeedSelf方法，默认可以自主进食
			if (_canFeedSelfMethod == null || _toddlerLearningUtilityType == null)
			{
				return true;
			}

			try
			{
				return (bool)_canFeedSelfMethod.Invoke(null, new object[] { toddler });
			}
			catch (Exception ex)
			{
				Log.Warning($"[RimTalk_ToddlersExpansion] 检查CanFeedSelf时出错: {ex.Message}");
				return true; // 出错时默认可以自主进食
			}
		}

		private static void TryFeedToddlerWithAssistance(Caravan_NeedsTracker needsTracker, Pawn toddler, Need_Food food)
		{
			Caravan caravan = needsTracker.caravan;

			// 查找婴儿食品
			Thing babyFood = CaravanInventoryUtility.AllInventoryItems(caravan)
				.FirstOrDefault(thing => thing.def == ThingDefOf.BabyFood && thing.stackCount > 0 && thing.IngestibleNow
					&& toddler.WillEat(thing, careIfNotAcceptableForTitle: false));

			if (babyFood == null)
			{
				return;
			}

			// 喂食
			float nutritionGained = babyFood.Ingested(toddler, food.NutritionWanted);

			if (nutritionGained > 0f)
			{
				food.CurLevel += nutritionGained;

				// 如果食物消耗完了，移除它
				if (babyFood.Destroyed || babyFood.stackCount <= 0)
				{
					Pawn owner = CaravanInventoryUtility.GetOwnerOf(caravan, babyFood);
					if (owner != null)
					{
						owner.inventory.innerContainer.Remove(babyFood);
					}
					caravan.RecacheInventory();
				}

				if (Prefs.DevMode)
				{
					Log.Message($"[RimTalk_ToddlersExpansion] 喂食toddler {toddler.Name}");
				}
			}
		}
	}
}
