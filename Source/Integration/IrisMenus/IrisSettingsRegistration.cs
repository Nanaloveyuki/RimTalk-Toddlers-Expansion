using System;
using IrisMenus;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.RimTalk;
using Verse;
using static RimTalk_ToddlersExpansion.Core.ToddlersExpansionMod;

namespace RimTalk_ToddlersExpansion.Integration.Iris
{
	[StaticConstructorOnStartup]
	internal static class IrisSettingsRegistration
	{
		static IrisSettingsRegistration()
		{
			LongEventHandler.ExecuteWhenFinished(Register);
		}

		private static void Register()
		{
			ToddlersExpansionMod owner = LoadedModManager.GetMod<ToddlersExpansionMod>();
			if (owner == null)
			{
				return;
			}

			RegisterPage(owner, "growth", "RimTalk_ToddlersExpansion_Iris_Growth", DrawGrowth);
			RegisterPage(owner, "baby-hair", YoungPawnSettingsKey + "BabyHair", DrawBabyAppearanceSettingsSection);
			RegisterPage(owner, "play", "RimTalk_ToddlersExpansion_Iris_Play", DrawPlay);
			RegisterPage(owner, "behavior", YoungPawnSettingsKey + "Behavior", DrawBehavior);
			RegisterPage(owner, "interactions", "RimTalk_ToddlersExpansion_Interaction_Settings_Header", DrawInteractionSettingsSection);
			RegisterPage(owner, "visitors", "RimTalk_ToddlersExpansion_Iris_Visitors", list => DrawCaravanGenerationSettingsSection(list, Settings));
			RegisterPage(owner, "outings", "RimTalk_ToddlersExpansion_Settings_Page2", list => DrawOutingSettingsSection(list, Settings));
			RegisterPage(owner, "animations", "RimTalk_ToddlersExpansion_Settings_Page3",
				list => DrawAnimationSettingsSection(list, repeatSharedAnimations: false));
			RegisterPage(owner, "performance", "RimTalk_ToddlersExpansion_Iris_Performance", DrawPerformanceSettingsSection);
			MenuRegistry.RegisterSubItemListing(owner, "activity-text", () => "RimTalk_ToddlersExpansion_Iris_ActivityText".Translate(),
				list => DrawLlmSettingsSection(list, Settings), owner.WriteSettings);
			MenuRegistry.RegisterPresentation(owner, "activity-text", new MenuPresentation(context => new IrisApiSettingsPage(context, Settings)));
			if (RimTalkCompatUtility.IsRimTalkActive)
			{
				RegisterPage(owner, "dialogue", "RimTalk_ToddlersExpansion_Iris_Dialogue", DrawTalkRequestSettingsSection);
			}
		}

		private static void RegisterPage(ToddlersExpansionMod owner, string id, string titleKey, Action<SettingsListing> draw)
		{
			MenuRegistry.RegisterSubItemListing(owner, id, () => titleKey.Translate(),
				list => draw(new VanillaSettingsListing(list)), owner.WriteSettings);
			MenuRegistry.RegisterPresentation(owner, id, new MenuPresentation(context => new IrisSettingsPage(context, draw)));
		}

		private static void DrawGrowth(SettingsListing list)
		{
			DrawLanguageSettingsSection(list);
			DrawFeedingSettingsSection(list);
			DrawBabyCrySettingsSection(list);
		}

		private static void DrawPlay(SettingsListing list)
		{
			DrawBoredomSettingsSection(list);
			DrawMishapEventSettingsSection(list);
		}

		private static void DrawBehavior(SettingsListing list)
		{
			DrawBehaviorSettingsSection(list);
			DrawMapExitSettingsSection(list);
		}
	}
}
