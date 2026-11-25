using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Harmony;
using RimTalk_ToddlersExpansion.Integration.RimTalk;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Core
{
	public sealed partial class ToddlersExpansionMod : Mod
	{
		public static ToddlersExpansionSettings Settings;
		private static Vector2 generalScrollPosition = Vector2.zero;
		private static Vector2 outingScrollPosition = Vector2.zero;
		private static Vector2 animationScrollPosition = Vector2.zero;
		private static Vector2 talkScrollPosition = Vector2.zero;
		private static Vector2 babyHairScrollPosition;
		private static float babyHairContentHeight;

		public ToddlersExpansionMod(ModContentPack content) : base(content)
		{
			LegacyModSettingsMigration.CopyIfMissing(content.FolderName, nameof(ToddlersExpansionMod));
			Settings = GetSettings<ToddlersExpansionSettings>();

			HarmonyBootstrap.Init();
			LongEventHandler.ExecuteWhenFinished(InitializeAfterDefsLoaded);
		}

		private static void InitializeAfterDefsLoaded()
		{
			Integration.Toddlers.ToddlerAgeSettingsUtility.ApplyConfiguredToddlerAge();
			Integration.Toddlers.ChildrenOutingSettingsUtility.ApplyConfiguredRandomSelectionWeight();
			Integration.Toddlers.ToddlerPlayAnimationUtility.InitializePlayAnimationWorkers();
			ToddlersExpansionDiagnostics.Run();
			RimTalkCompatUtility.TryRegisterToddlerVariables();
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			ToddlersExpansionSettings settings = Settings;
			if (settings == null)
			{
				return;
			}

			settings.SettingsPageIndex = Mathf.Clamp(settings.SettingsPageIndex, 0, 4);

			const float topPadding = 40f;
			Rect tabsRect = new Rect(inRect.x, inRect.y + topPadding, inRect.width, 32f);
			List<TabRecord> tabs = new List<TabRecord>
			{
				new TabRecord("RimTalk_ToddlersExpansion_Settings_Page1".Translate(), () => settings.SettingsPageIndex = 0, settings.SettingsPageIndex == 0),
				new TabRecord("RimTalk_ToddlersExpansion_Settings_Page2".Translate(), () => settings.SettingsPageIndex = 1, settings.SettingsPageIndex == 1),
				new TabRecord("RimTalk_ToddlersExpansion_Settings_Page3".Translate(), () => settings.SettingsPageIndex = 2, settings.SettingsPageIndex == 2),
				new TabRecord("RimTalk_ToddlersExpansion_Settings_Page4".Translate(), () => settings.SettingsPageIndex = 3, settings.SettingsPageIndex == 3),
				new TabRecord((YoungPawnSettingsKey + "BabyHair").Translate(), () => settings.SettingsPageIndex = 4, settings.SettingsPageIndex == 4)
			};
			TabDrawer.DrawTabs(tabsRect, tabs);

			Rect pageRect = new Rect(inRect.x, tabsRect.yMax + 8f, inRect.width, inRect.height - (tabsRect.yMax - inRect.y) - 8f);
			if (settings.SettingsPageIndex == 0)
			{
				DrawGeneralSettingsPage(pageRect, settings);
			}
			else if (settings.SettingsPageIndex == 1)
			{
				DrawOutingSettingsPage(pageRect, settings);
			}
			else if (settings.SettingsPageIndex == 2)
			{
				DrawAnimationSettingsPage(pageRect);
			}
			else if (settings.SettingsPageIndex == 3)
			{
				DrawTalkSettingsPage(pageRect);
			}
			else
			{
				DrawBabyHairSettingsPage(pageRect);
			}
		}

		public override void WriteSettings()
		{
			base.WriteSettings();
			Integration.Toddlers.ToddlerAgeSettingsUtility.ApplyConfiguredToddlerAge();
			Integration.Toddlers.ChildrenOutingSettingsUtility.ApplyConfiguredRandomSelectionWeight();
		}

		private void DrawGeneralSettingsPage(Rect inRect, ToddlersExpansionSettings settings)
		{
			const float columnGap = 24f;
			float contentHeight = ToddlersExpansionSettings.UseCustomLearningSchedule ? 2850f : 2350f;
			Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, contentHeight);
			float columnWidth = (viewRect.width - columnGap) / 2f;
			Rect leftColumnRect = new Rect(0f, 0f, columnWidth, contentHeight);
			Rect rightColumnRect = new Rect(columnWidth + columnGap, 0f, columnWidth, contentHeight);

			Widgets.BeginScrollView(inRect, ref generalScrollPosition, viewRect);
			Listing_Standard leftColumn = new Listing_Standard();
			leftColumn.Begin(leftColumnRect);
			var leftSettings = new VanillaSettingsListing(leftColumn);
			DrawCaravanGenerationSettingsSection(leftSettings, settings);
			DrawBoredomSettingsSection(leftSettings);
			DrawLanguageSettingsSection(leftSettings);
			DrawFeedingSettingsSection(leftSettings);
			DrawBabyCrySettingsSection(leftSettings);
			leftColumn.End();

			Listing_Standard rightColumn = new Listing_Standard();
			rightColumn.Begin(rightColumnRect);
			var rightSettings = new VanillaSettingsListing(rightColumn);
			DrawPerformanceSettingsSection(rightSettings);
			DrawBehaviorSettingsSection(rightSettings);
			DrawMapExitSettingsSection(rightSettings);
			DrawInteractionSettingsSection(rightSettings);
			DrawMishapEventSettingsSection(rightSettings);
			rightColumn.End();

			Widgets.EndScrollView();
		}

		private void DrawOutingSettingsPage(Rect inRect, ToddlersExpansionSettings settings)
		{
			float contentHeight = 980f;
			Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, contentHeight);

			Widgets.BeginScrollView(inRect, ref outingScrollPosition, viewRect);
			Listing_Standard listingStandard = new Listing_Standard();
			listingStandard.Begin(viewRect);
			DrawOutingSettingsSection(new VanillaSettingsListing(listingStandard), settings);

			listingStandard.End();
			Widgets.EndScrollView();
		}

		internal static void DrawOutingSettingsSection(SettingsListing listingStandard, ToddlersExpansionSettings settings)
		{
			listingStandard.Heading("RimTalk_ToddlersExpansion_Outing_Settings_Header".Translate());
			listingStandard.Label("RimTalk_ToddlersExpansion_Outing_Settings_Desc".Translate());
			listingStandard.Gap();

			float previousOutingChanceFactor = ToddlersExpansionSettings.childrenOutingChanceFactor;
			ToddlersExpansionSettings.childrenOutingChanceFactor =
				listingStandard.Slider("RimTalk_ToddlersExpansion_Outing_ChanceFactor",
					ToddlersExpansionSettings.GetChildrenOutingChanceFactor(), 0f, 5f,
					ToddlersExpansionSettings.GetChildrenOutingChanceFactor().ToString("0.##"));
			if (!Mathf.Approximately(previousOutingChanceFactor, ToddlersExpansionSettings.childrenOutingChanceFactor))
			{
				Integration.Toddlers.ChildrenOutingSettingsUtility.ApplyConfiguredRandomSelectionWeight();
			}
			listingStandard.Label("RimTalk_ToddlersExpansion_Outing_ChanceFactor_Tooltip".Translate());
			listingStandard.Gap();
			listingStandard.GapLine();

			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_EnableNatureRunningSafetyChecks",
				ref ToddlersExpansionSettings.enableNatureRunningSafetyChecks,
				"RimTalk_ToddlersExpansion_EnableNatureRunningSafetyChecks_Tooltip".Translate());
			listingStandard.Gap();
			DrawYoungPawnToggle(listingStandard, "NatureRunningFollowers", ref ToddlersExpansionSettings.EnableNatureRunningFollowers);
			listingStandard.GapLine();

			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_VanillaEdgeRandom", ref settings.EnableOutingPoolVanillaEdgeRandom);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_GrowingZone", ref settings.EnableOutingPoolGrowingZone);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_StockpileZone", ref settings.EnableOutingPoolStockpileZone);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_ResearchRoom", ref settings.EnableOutingPoolResearchRoom);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_TempleRoom", ref settings.EnableOutingPoolTempleRoom);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_KitchenRoom", ref settings.EnableOutingPoolKitchenRoom);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_RecreationRoom", ref settings.EnableOutingPoolRecreationRoom);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_HospitalRoom", ref settings.EnableOutingPoolHospitalRoom);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_OtherNonBedroomRooms", ref settings.EnableOutingPoolOtherNonBedroomRooms);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_ThingWithCompsLandmark", ref settings.EnableOutingPoolThingWithCompsLandmark);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_River", ref settings.EnableOutingPoolRiver);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_Lake", ref settings.EnableOutingPoolLake);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_Snow", ref settings.EnableOutingPoolSnow);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_Cave", ref settings.EnableOutingPoolCave);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_Sand", ref settings.EnableOutingPoolSand);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_OutingPool_AncientRoad", ref settings.EnableOutingPoolAncientRoad);

			if (!HasAnyOutingPoolEnabled(settings))
			{
				listingStandard.Gap();
				listingStandard.Warning("RimTalk_ToddlersExpansion_OutingPool_AllDisabledWarning".Translate());
			}
		}

		private void DrawAnimationSettingsPage(Rect inRect)
		{
			float contentHeight = 1100f;
			Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, contentHeight);

			Widgets.BeginScrollView(inRect, ref animationScrollPosition, viewRect);
			Listing_Standard listingStandard = new Listing_Standard();
			listingStandard.Begin(viewRect);
			DrawAnimationSettingsSection(new VanillaSettingsListing(listingStandard));

			listingStandard.End();
			Widgets.EndScrollView();
		}

		internal static void DrawAnimationSettingsSection(SettingsListing listingStandard, bool repeatSharedAnimations = true)
		{
			bool yayoLoaded = Integration.YayoAnimation.YayoAnimationCompatUtility.IsYayoAnimationLoaded;
			listingStandard.Heading("RimTalk_ToddlersExpansion_Animation_Settings_Header".Translate());
			listingStandard.Label("RimTalk_ToddlersExpansion_Animation_Settings_Desc".Translate());
			listingStandard.Gap();
			listingStandard.Label((yayoLoaded
				? "RimTalk_ToddlersExpansion_Animation_YayoLoaded"
				: "RimTalk_ToddlersExpansion_Animation_YayoNotLoaded").Translate());
			listingStandard.Gap();

			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_Animation_EnableNewborn",
				ref ToddlersExpansionSettings.EnableNewbornPlayAnimations,
				"RimTalk_ToddlersExpansion_Animation_EnableNewborn_Tooltip".Translate());
			listingStandard.GapLine();

			listingStandard.Label((repeatSharedAnimations
				? "RimTalk_ToddlersExpansion_Animation_NoYayo_Header"
				: "RimTalk_ToddlersExpansion_Iris_SharedAnimations").Translate());
			listingStandard.Gap();
			DrawSharedAnimationOptions(listingStandard);
			listingStandard.GapLine();

			listingStandard.Label((repeatSharedAnimations
				? "RimTalk_ToddlersExpansion_Animation_Yayo_Header"
				: "RimTalk_ToddlersExpansion_Iris_YayoAnimations").Translate());
			listingStandard.Gap();
			if (repeatSharedAnimations)
			{
				DrawSharedAnimationOptions(listingStandard);
			}
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_PlayToys", ref ToddlersExpansionSettings.EnableYayoPlayToys);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_Hoopstone", ref ToddlersExpansionSettings.EnableYayoPlayHoopstone);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_DartsBoard", ref ToddlersExpansionSettings.EnableYayoPlayDartsBoard);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_GoldenCube", ref ToddlersExpansionSettings.EnableYayoGoldenCube);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_SocialRelax", ref ToddlersExpansionSettings.EnableYayoSocialRelax);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_BabyRoll", ref ToddlersExpansionSettings.EnableYayoBabyRoll);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_Roll", ref ToddlersExpansionSettings.EnableYayoCustomRoll);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_Spin", ref ToddlersExpansionSettings.EnableYayoCustomSpin);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_Hop", ref ToddlersExpansionSettings.EnableYayoCustomHop);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_RunLoop", ref ToddlersExpansionSettings.EnableYayoCustomRunLoop);
		}

		private static void DrawSharedAnimationOptions(SettingsListing listingStandard)
		{
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_Wiggle", ref ToddlersExpansionSettings.EnableNativePlayWiggle);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_Sway", ref ToddlersExpansionSettings.EnableNativePlaySway);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_Lay", ref ToddlersExpansionSettings.EnableNativePlayLay);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_ProneCrawl", ref ToddlersExpansionSettings.EnableNativePlayCrawl);
			listingStandard.CheckboxLabeled("RimTalk_ToddlersExpansion_Animation_ToddlersWobble", ref ToddlersExpansionSettings.EnableNativePlayToddlerWobble);
		}

		internal static void DrawPerformanceSettingsSection(SettingsListing listingStandard)
		{
			listingStandard.Heading("RimTalk_ToddlersExpansion_Performance_Settings_Header".Translate());
			listingStandard.Label("RimTalk_ToddlersExpansion_Performance_Settings_Desc".Translate());
			listingStandard.Gap();

			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_Performance_MuteSpamLogs",
				ref ToddlersExpansionSettings.MuteSpamDebugLogs,
				"RimTalk_ToddlersExpansion_Performance_MuteSpamLogs_Tooltip".Translate());
			listingStandard.Gap();

			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_Performance_MuteAllLogs",
				ref ToddlersExpansionSettings.MuteAllLogs,
				"RimTalk_ToddlersExpansion_Performance_MuteAllLogs_Tooltip".Translate());
			listingStandard.Gap();

			ToddlersExpansionSettings.MutualPlayPartnerCheckIntervalTicks =
				listingStandard.Slider("RimTalk_ToddlersExpansion_Performance_MutualPlayCheckInterval",
					ToddlersExpansionSettings.MutualPlayPartnerCheckIntervalTicks, 1, 600, ToddlersExpansionSettings.GetMutualPlayPartnerCheckIntervalTicks());
			listingStandard.Gap();

			ToddlersExpansionSettings.ToddlerMainLoopCheckIntervalTicks =
				listingStandard.Slider("RimTalk_ToddlersExpansion_Performance_MainLoopCheckInterval",
					ToddlersExpansionSettings.ToddlerMainLoopCheckIntervalTicks, 1, 120, ToddlersExpansionSettings.GetToddlerMainLoopCheckIntervalTicks());
			listingStandard.Gap();

			ToddlersExpansionSettings.BabyCarryCheckIntervalTicks =
				listingStandard.Slider("RimTalk_ToddlersExpansion_Performance_BabyCarryCheckInterval",
					ToddlersExpansionSettings.BabyCarryCheckIntervalTicks, 30, 600, ToddlersExpansionSettings.GetBabyCarryPickupCheckIntervalTicks());
			listingStandard.Gap();
		}

		private void DrawTalkSettingsPage(Rect inRect)
		{
			float contentHeight = 900f;
			Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, contentHeight);

			Widgets.BeginScrollView(inRect, ref talkScrollPosition, viewRect);
			Listing_Standard listingStandard = new Listing_Standard();
			listingStandard.Begin(viewRect);

			DrawLlmSettingsSection(listingStandard, Settings);
			listingStandard.GapLine();

			DrawTalkRequestSettingsSection(new VanillaSettingsListing(listingStandard));

			listingStandard.End();
			Widgets.EndScrollView();
		}

		internal static void DrawCaravanGenerationSettingsSection(SettingsListing listingStandard, ToddlersExpansionSettings settings)
		{
			listingStandard.Heading("RimTalk_Caravan_Settings_Header".Translate());

			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_EnableCaravanToddlerGeneration",
				ref settings.EnableCaravanToddlerGeneration,
				"RimTalk_ToddlersExpansion_EnableCaravanToddlerGeneration_Tooltip".Translate());
			listingStandard.Gap();

			if (!settings.EnableCaravanToddlerGeneration)
			{
				listingStandard.GapLine();
				return;
			}

			settings.MaxToddlersPerGroup =
				listingStandard.Slider("RimTalk_ToddlersExpansion_MaxToddlersPerGroup",
					settings.MaxToddlersPerGroup, 0, 5, settings.MaxToddlersPerGroup);
			listingStandard.Gap();

			settings.MaxChildrenPerGroup =
				listingStandard.Slider("RimTalk_ToddlersExpansion_MaxChildrenPerGroup",
					settings.MaxChildrenPerGroup, 0, 5, settings.MaxChildrenPerGroup);
			listingStandard.Gap();

			settings.ToddlerGenerationChance =
				listingStandard.Slider("RimTalk_ToddlersExpansion_ToddlerGenerationChance",
					settings.ToddlerGenerationChance, 0f, 1f, settings.ToddlerGenerationChance.ToStringPercent());
			listingStandard.Gap();

			settings.ChildGenerationChance =
				listingStandard.Slider("RimTalk_ToddlersExpansion_ChildGenerationChance",
					settings.ChildGenerationChance, 0f, 1f, settings.ChildGenerationChance.ToStringPercent());
			listingStandard.Gap();

			settings.MinBatchCount =
				listingStandard.Slider("RimTalk_ToddlersExpansion_MinBatchCount",
					settings.MinBatchCount, 1, 5, settings.MinBatchCount);
			listingStandard.Gap();

			settings.MaxBatchCount =
				listingStandard.Slider("RimTalk_ToddlersExpansion_MaxBatchCount",
					settings.MaxBatchCount, 1, 5, settings.MaxBatchCount);
			listingStandard.Gap();

			if (settings.MinBatchCount > settings.MaxBatchCount)
			{
				settings.MinBatchCount = settings.MaxBatchCount;
			}

			settings.ExtraBatchChance =
				listingStandard.Slider("RimTalk_ToddlersExpansion_ExtraBatchChance",
					settings.ExtraBatchChance, 0f, 1f, settings.ExtraBatchChance.ToStringPercent());
			listingStandard.Gap();
			listingStandard.GapLine();
		}

		internal static void DrawBoredomSettingsSection(SettingsListing listingStandard)
		{
			listingStandard.Heading("RimTalk_Boredom_Settings_Header".Translate());
			listingStandard.CheckboxLabeled("RimTalk_Boredom_Enable", ref ToddlersExpansionSettings.enableBoredomSystem, "RimTalk_Boredom_Enable_Tooltip".Translate());
			listingStandard.Gap();

			if (ToddlersExpansionSettings.enableBoredomSystem)
			{
				ToddlersExpansionSettings.boredomIncreasePerActivity =
					listingStandard.Slider("RimTalk_Boredom_IncreasePerActivity",
						ToddlersExpansionSettings.boredomIncreasePerActivity, 0.01f, 0.2f, ToddlersExpansionSettings.boredomIncreasePerActivity.ToStringPercent());
				listingStandard.Gap();

				ToddlersExpansionSettings.boredomMaxCap =
					listingStandard.Slider("RimTalk_Boredom_MaxCap",
						ToddlersExpansionSettings.boredomMaxCap, 0.5f, 1f, ToddlersExpansionSettings.boredomMaxCap.ToStringPercent());
				listingStandard.Gap();

				ToddlersExpansionSettings.boredomDailyRecoveryRate =
					listingStandard.Slider("RimTalk_Boredom_DailyRecoveryRate",
						ToddlersExpansionSettings.boredomDailyRecoveryRate, 0.05f, 0.5f, ToddlersExpansionSettings.boredomDailyRecoveryRate.ToStringPercent());
				listingStandard.Gap();

				listingStandard.CheckboxLabeled("RimTalk_Boredom_AutoDetection", ref ToddlersExpansionSettings.enableAutoDetection, "RimTalk_Boredom_AutoDetection_Tooltip".Translate());
				listingStandard.Gap();
			}

			listingStandard.GapLine();
		}

		internal static void DrawLanguageSettingsSection(SettingsListing listingStandard)
		{
			const string key = "RimTalk_ToddlerGrowth_";
			listingStandard.Heading("RimTalk_ToddlersExpansion_Iris_Growth".Translate());
			int oldThresholdDays = ToddlersExpansionSettings.GetNewbornToToddlerDays();
			ToddlersExpansionSettings.newbornToToddlerDays =
				listingStandard.Slider("RimTalk_ToddlersExpansion_NewbornToToddlerDays",
					ToddlersExpansionSettings.GetNewbornToToddlerDays(), 1, 179, oldThresholdDays, ToddlersExpansionSettings.GetNewbornToToddlerYears().ToString("0.##"));
			if (ToddlersExpansionSettings.GetNewbornToToddlerDays() != oldThresholdDays)
			{
				Integration.Toddlers.ToddlerAgeSettingsUtility.ApplyConfiguredToddlerAge();
			}
			listingStandard.Gap();
			listingStandard.CheckboxLabeled(key + "CustomSchedule", ref ToddlersExpansionSettings.UseCustomLearningSchedule);
			if (ToddlersExpansionSettings.UseCustomLearningSchedule)
			{
				listingStandard.Label((key + "ScheduleHelp").Translate());
				listingStandard.Gap();
				ToddlersExpansionSettings.CrawlingDays = PhaseDays("Crawling", ToddlersExpansionSettings.CrawlingDays);
				ToddlersExpansionSettings.WobblyWalkingDays = PhaseDays("WobblyWalking", ToddlersExpansionSettings.WobblyWalkingDays);
				listingStandard.Gap();
				ToddlersExpansionSettings.IneptManipulationDays = PhaseDays("IneptManipulation", ToddlersExpansionSettings.IneptManipulationDays);
				ToddlersExpansionSettings.ClumsyManipulationDays = PhaseDays("ClumsyManipulation", ToddlersExpansionSettings.ClumsyManipulationDays);
				ToddlersExpansionSettings.ConfidentManipulationDays = PhaseDays("ConfidentManipulation", ToddlersExpansionSettings.ConfidentManipulationDays);
				listingStandard.Gap();
				ToddlersExpansionSettings.BabblingDays = PhaseDays("Babbling", ToddlersExpansionSettings.BabblingDays);
				ToddlersExpansionSettings.SingleWordDays = PhaseDays("SingleWords", ToddlersExpansionSettings.SingleWordDays);
				ToddlersExpansionSettings.ShortPhraseDays = PhaseDays("ShortPhrases", ToddlersExpansionSettings.ShortPhraseDays);
			}
			else
			{
				float duration = ToddlersExpansionSettings.learningFactor_Talking;
				ToddlersExpansionSettings.learningFactor_Talking =
					listingStandard.Slider(key + "LanguageDuration", duration, 0.1f, 3f, duration.ToString("0.##"));
			}
			listingStandard.Gap();
			listingStandard.GapLine();

			int PhaseDays(string phase, int days) =>
				listingStandard.Slider(key + phase, days, 1, ToddlerLearningSchedule.MaxPhaseDays, days);
		}

		internal static void DrawFeedingSettingsSection(SettingsListing listingStandard)
		{
			listingStandard.Heading("RimTalk_ToddlersExpansion_Feeding_Settings_Header".Translate());
			ToddlersExpansionSettings.toddlerEatingSpeedFactor =
				listingStandard.Slider("RimTalk_ToddlersExpansion_ToddlerEatingSpeed",
					ToddlersExpansionSettings.toddlerEatingSpeedFactor, 0.1f, 10f, ToddlersExpansionSettings.toddlerEatingSpeedFactor.ToString("0.##"));
			listingStandard.Gap();
			DrawYoungPawnGroupSettings(listingStandard, "SupplyFood", ToddlerFoodSettings.SupplyFood);
			DrawYoungPawnGroupSettings(listingStandard, "FeedWhileCarried", ToddlerFoodSettings.FeedWhileCarried);
			DrawYoungPawnGroupSettings(listingStandard, "FeedInCaravan", ToddlerFoodSettings.FeedInCaravan);
			DrawYoungPawnToggle(listingStandard, "MidnightSnacks", ref ToddlersExpansionSettings.EnableMidnightSnacks);
			listingStandard.GapLine();
		}

		internal static void DrawBabyCrySettingsSection(SettingsListing listingStandard)
		{
			listingStandard.Heading("RimTalk_ToddlersExpansion_BabyCry_Settings_Header".Translate());

			bool muteSocialImpact = ToddlersExpansionSettings.babyCryAffectsMoodOnly;
			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_BabyCry_NoSocial",
				ref muteSocialImpact,
				"RimTalk_ToddlersExpansion_BabyCry_NoSocial_Tooltip".Translate());
			ToddlersExpansionSettings.babyCryAffectsMoodOnly = muteSocialImpact;
			listingStandard.Gap();

			bool muteMoodImpact = !ToddlersExpansionSettings.babyCryAffectsMood;
			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_BabyCry_NoMood",
				ref muteMoodImpact,
				"RimTalk_ToddlersExpansion_BabyCry_NoMood_Tooltip".Translate());
			ToddlersExpansionSettings.babyCryAffectsMood = !muteMoodImpact;
			listingStandard.Gap();
			DrawYoungPawnToggle(listingStandard, "NonBlockingBabyFits", ref ToddlersExpansionSettings.EnableNonBlockingBabyFits);
		}

		internal static void DrawBehaviorSettingsSection(SettingsListing listingStandard)
		{
			listingStandard.Heading("RimTalk_ToddlersExpansion_Behavior_Settings_Header".Translate());
			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_EnablePrisonerToddlerColonistThinkTree",
				ref ToddlersExpansionSettings.enablePrisonerToddlerColonistThinkTree,
				"RimTalk_ToddlersExpansion_EnablePrisonerToddlerColonistThinkTree_Tooltip".Translate());
			listingStandard.Gap();

			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_EnableHostileToddlerColonistThinkTree",
				ref ToddlersExpansionSettings.enableHostileToddlerColonistThinkTree,
				"RimTalk_ToddlersExpansion_EnableHostileToddlerColonistThinkTree_Tooltip".Translate());
			listingStandard.Gap();

			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_PreventColonistAttackingHostileToddler",
				ref ToddlersExpansionSettings.preventColonistAttackingHostileToddler,
				"RimTalk_ToddlersExpansion_PreventColonistAttackingHostileToddler_Tooltip".Translate());
			listingStandard.Gap();

			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_EnableToddlerPlaySafetyChecks",
				ref ToddlersExpansionSettings.enableToddlerPlaySafetyChecks,
				"RimTalk_ToddlersExpansion_EnableToddlerPlaySafetyChecks_Tooltip".Translate());
			listingStandard.Gap();
			DrawYoungPawnToggle(listingStandard, "FamilyBedroomThoughts", ref ToddlersExpansionSettings.EnableFamilyBedroomThoughts);
			DrawYoungPawnGroupSettings(listingStandard, "SkipSafeToddlerCribReturn", ToddlersExpansionSettings.SkipSafeToddlerCribReturn, includeNonColonists: false);
			listingStandard.GapLine();
		}

		internal static void DrawInteractionSettingsSection(SettingsListing listingStandard)
		{
			listingStandard.Heading("RimTalk_ToddlersExpansion_Interaction_Settings_Header".Translate());
			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_Interaction_EnablePrisonerCarry",
				ref ToddlersExpansionSettings.EnablePrisonerBabyCarryInteractions,
				"RimTalk_ToddlersExpansion_Interaction_EnablePrisonerCarry_Tooltip".Translate());
			listingStandard.Gap();
			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_Interaction_EnableChildCarry",
				ref ToddlersExpansionSettings.EnableChildBabyCarryInteractions,
				"RimTalk_ToddlersExpansion_Interaction_EnableChildCarry_Tooltip".Translate());
			listingStandard.Gap();
			DrawYoungPawnToggle(listingStandard, "BabyCarryDesire", ref ToddlersExpansionSettings.EnableBabyCarryDesire);
			DrawYoungPawnToggle(listingStandard, "AutomaticToddlerBathing", ref ToddlersExpansionSettings.EnableAutomaticToddlerBathing);
			DrawYoungPawnToggle(listingStandard, "FriendlyBabyAdoption", ref ToddlersExpansionSettings.EnableFriendlyBabyAdoption);
			DrawYoungPawnToggle(listingStandard, "IgnoreBabyStripGoodwill", ref ToddlersExpansionSettings.EnableIgnoreBabyStripGoodwill);
			DrawYoungPawnToggle(listingStandard, "KiiroRefugeeProtection", ref ToddlersExpansionSettings.EnableKiiroRefugeeProtection);
			if (RimTalkCompatUtility.IsRimTalkActive)
			{
				listingStandard.CheckboxLabeled(
					"RimTalk_ToddlersExpansion_Interaction_EnableYoungPawnSoulReplacement",
					ref ToddlersExpansionSettings.EnableYoungPawnSoulReplacement,
					"RimTalk_ToddlersExpansion_Interaction_EnableYoungPawnSoulReplacement_Tooltip".Translate());
				listingStandard.Gap();
			}
		}


		internal static void DrawMishapEventSettingsSection(SettingsListing listingStandard)
		{
			listingStandard.Heading("RimTalk_ToddlersExpansion_Mishap_Settings_Header".Translate());

			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_Mishap_EnableTumble",
				ref ToddlersExpansionSettings.enableToddlerTumble,
				"RimTalk_ToddlersExpansion_Mishap_EnableTumble_Tooltip".Translate());
			listingStandard.Gap();

			if (ToddlersExpansionSettings.enableToddlerTumble)
			{
				ToddlersExpansionSettings.toddlerTumbleChanceFactor =
					listingStandard.Slider("RimTalk_ToddlersExpansion_Mishap_TumbleChance",
						ToddlersExpansionSettings.toddlerTumbleChanceFactor, 0f, 3f, ToddlersExpansionSettings.toddlerTumbleChanceFactor.ToString("0.##"));
				listingStandard.Gap();

				ToddlersExpansionSettings.toddlerTumbleDamageMax =
					listingStandard.Slider("RimTalk_ToddlersExpansion_Mishap_TumbleDamageMax",
						ToddlersExpansionSettings.toddlerTumbleDamageMax, 1, 15, ToddlersExpansionSettings.toddlerTumbleDamageMax);
				listingStandard.Gap();
			}

			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_Mishap_EnableScuffle",
				ref ToddlersExpansionSettings.enableToddlerScuffle,
				"RimTalk_ToddlersExpansion_Mishap_EnableScuffle_Tooltip".Translate());
			listingStandard.Gap();

			if (ToddlersExpansionSettings.enableToddlerScuffle)
			{
				ToddlersExpansionSettings.toddlerScuffleChanceFactor =
					listingStandard.Slider("RimTalk_ToddlersExpansion_Mishap_ScuffleChance",
						ToddlersExpansionSettings.toddlerScuffleChanceFactor, 0f, 3f, ToddlersExpansionSettings.toddlerScuffleChanceFactor.ToString("0.##"));
				listingStandard.Gap();

				ToddlersExpansionSettings.toddlerScuffleDamageMax =
					listingStandard.Slider("RimTalk_ToddlersExpansion_Mishap_ScuffleDamageMax",
						ToddlersExpansionSettings.toddlerScuffleDamageMax, 1, 15, ToddlersExpansionSettings.toddlerScuffleDamageMax);
				listingStandard.Gap();
			}

			listingStandard.GapLine();
		}
		internal static void DrawTalkRequestSettingsSection(SettingsListing listingStandard)
		{
			if (!RimTalkCompatUtility.IsRimTalkActive)
			{
				return;
			}
			listingStandard.Heading("RimTalk_ToddlersExpansion_Talk_Settings_Header".Translate());
			listingStandard.Label("RimTalk_ToddlersExpansion_Talk_Settings_Desc".Translate());
			listingStandard.Gap();
			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_Talk_EnableSelfPlay",
				ref ToddlersExpansionSettings.EnableRimTalkSelfPlayEventTalkRequests,
				"RimTalk_ToddlersExpansion_Talk_EnableSelfPlay_Tooltip".Translate());
			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_Talk_EnableMutualPlay",
				ref ToddlersExpansionSettings.EnableRimTalkMutualPlayEventTalkRequests,
				"RimTalk_ToddlersExpansion_Talk_EnableMutualPlay_Tooltip".Translate());
			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_Talk_EnableWatchPlay",
				ref ToddlersExpansionSettings.EnableRimTalkWatchPlayEventTalkRequests,
				"RimTalk_ToddlersExpansion_Talk_EnableWatchPlay_Tooltip".Translate());
			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_Talk_EnableCarriedPlay",
				ref ToddlersExpansionSettings.EnableRimTalkCarriedPlayEventTalkRequests,
				"RimTalk_ToddlersExpansion_Talk_EnableCarriedPlay_Tooltip".Translate());
			listingStandard.CheckboxLabeled(
				"RimTalk_ToddlersExpansion_Talk_EnableStruggle",
				ref ToddlersExpansionSettings.EnableRimTalkStruggleEventTalkRequests,
				"RimTalk_ToddlersExpansion_Talk_EnableStruggle_Tooltip".Translate());
		}

		private static bool HasAnyOutingPoolEnabled(ToddlersExpansionSettings settings)
		{
			return settings.EnableOutingPoolVanillaEdgeRandom
				|| settings.EnableOutingPoolGrowingZone
				|| settings.EnableOutingPoolStockpileZone
				|| settings.EnableOutingPoolResearchRoom
				|| settings.EnableOutingPoolTempleRoom
				|| settings.EnableOutingPoolKitchenRoom
				|| settings.EnableOutingPoolRecreationRoom
				|| settings.EnableOutingPoolHospitalRoom
				|| settings.EnableOutingPoolOtherNonBedroomRooms
				|| settings.EnableOutingPoolThingWithCompsLandmark
				|| settings.EnableOutingPoolRiver
				|| settings.EnableOutingPoolLake
				|| settings.EnableOutingPoolSnow
				|| settings.EnableOutingPoolCave
				|| settings.EnableOutingPoolSand
				|| settings.EnableOutingPoolAncientRoad;
		}

		public override string SettingsCategory() => "RimTalk_ToddlersExpansion_Settings_Title".Translate();
	}
}
