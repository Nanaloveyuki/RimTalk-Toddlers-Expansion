using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Core
{
	public sealed partial class ToddlersExpansionMod
	{
		internal const string YoungPawnSettingsKey = "RimTalk_ToddlersExpansion_YoungPawn_";
		private static readonly YoungPawnGroup[] youngPawnGroups = Enum.GetValues<YoungPawnGroup>();

		private static void DrawBabyHairSettingsPage(Rect page)
		{
			float height = Mathf.Max(page.height, babyHairContentHeight);
			Rect view = new Rect(0f, 0f, page.width - 20f, height);
			Widgets.BeginScrollView(page, ref babyHairScrollPosition, view);
			Listing_Standard listing = new Listing_Standard { maxOneColumn = true };
			listing.Begin(view);
			DrawBabyAppearanceSettingsSection(new VanillaSettingsListing(listing));
			babyHairContentHeight = listing.CurHeight + 12f;
			listing.End();
			Widgets.EndScrollView();
		}

		internal static void DrawBabyAppearanceSettingsSection(SettingsListing list)
		{
			list.Heading((YoungPawnSettingsKey + "HairGroupDefaults").Translate());
			string tooltip = (YoungPawnSettingsKey + "HairGroupTooltip").Translate();
			bool prisoner = ToddlersExpansionSettings.enablePrisonerBabyHair;
			bool slave = ToddlersExpansionSettings.enableSlaveBabyHair;
			bool colonist = ToddlersExpansionSettings.enableColonistBabyHair;
			list.CheckboxLabeled(YoungPawnSettingsKey + "Prisoner", ref ToddlersExpansionSettings.enablePrisonerBabyHair, tooltip);
			list.CheckboxLabeled(YoungPawnSettingsKey + "Slave", ref ToddlersExpansionSettings.enableSlaveBabyHair, tooltip);
			list.CheckboxLabeled(YoungPawnSettingsKey + "Colonist", ref ToddlersExpansionSettings.enableColonistBabyHair, tooltip);
			if (prisoner != ToddlersExpansionSettings.enablePrisonerBabyHair
				|| slave != ToddlersExpansionSettings.enableSlaveBabyHair
				|| colonist != ToddlersExpansionSettings.enableColonistBabyHair)
			{
				BabyHairGameComponent.RefreshLoadedBabies();
			}

			list.Gap();
			list.Heading((YoungPawnSettingsKey + "HairIndividual").Translate());
			IReadOnlyList<Pawn> babies = BabyHairGameComponent.GetLoadedBabies();
			if (babies.Count == 0)
			{
				string key = Current.Game == null ? "HairLoadGame" : "HairNoBabies";
				list.Label((YoungPawnSettingsKey + key).Translate());
				return;
			}

			list.Label((YoungPawnSettingsKey + "HairIndividualDesc").Translate());
			list.Gap();
			string[] choices =
			[
				(YoungPawnSettingsKey + "HairDefault").Translate(),
				(YoungPawnSettingsKey + "HairShow").Translate(),
				(YoungPawnSettingsKey + "HairHide").Translate()
			];
			foreach (Pawn pawn in babies)
			{
				if (!pawn.Dead && !pawn.Destroyed && pawn.MapHeld != null && BabyHairGameComponent.CanConfigureHair(pawn))
				{
					DrawBabyHairPreference(list, pawn, choices);
				}
			}
		}

		private static void DrawBabyHairPreference(SettingsListing list, Pawn pawn, string[] choices)
		{
			string name = pawn.Name?.ToStringFull ?? pawn.LabelShortCap.ToString();
			string group = (YoungPawnSettingsKey + YoungPawnIdentity.GetGroup(pawn)).Translate();
			string faction = pawn.Faction?.Name ?? (YoungPawnSettingsKey + "NoFaction").Translate().ToString();
			Map map = pawn.MapHeld;
			string location = map?.Parent?.LabelCap.ToString() ?? (YoungPawnSettingsKey + "MapName").Translate(map?.uniqueID ?? 0).ToString();
			long ageDays = pawn.ageTracker.AgeBiologicalTicks / GenDate.TicksPerDay;
			string detail = (YoungPawnSettingsKey + "HairPawnDetails").Translate(group, faction, pawn.def.LabelCap, ageDays, location, pawn.thingIDNumber);
			list.PawnIdentity(pawn, name + "\n" + detail);

			int selected = BabyHairGameComponent.GetOverride(pawn) switch { true => 1, false => 2, _ => 0 };
			int next = list.Choice(pawn.GetUniqueLoadID(), choices, selected);
			if (next != selected)
			{
				BabyHairGameComponent.SetOverride(pawn, next switch { 1 => true, 2 => false, _ => null });
			}
			list.Gap();
		}

		internal static void DrawMapExitSettingsSection(SettingsListing list)
		{
			list.Heading((YoungPawnSettingsKey + "MapExit").Translate());
			DrawYoungPawnGroupSettings(list, "AllowMapExit", ToddlerExitSettings.AllowMapExit);
		}

		private static void DrawYoungPawnGroupSettings(SettingsListing list, string key, YoungPawnGroupSetting setting, bool includeNonColonists = true)
		{
			string feature = YoungPawnSettingsKey + key;
			list.Label(feature.Translate());
			string tooltip = (feature + "Tooltip").Translate();
			foreach (YoungPawnGroup group in youngPawnGroups)
			{
				if (!includeNonColonists && group == YoungPawnGroup.NonColonist)
				{
					continue;
				}
				bool enabled = setting[group];
				list.CheckboxLabeled(feature + group, (YoungPawnSettingsKey + group).Translate(), ref enabled, tooltip);
				setting[group] = enabled;
			}
			list.Gap();
		}

		private static void DrawYoungPawnToggle(SettingsListing list, string key, ref bool enabled)
		{
			string feature = YoungPawnSettingsKey + key;
			list.CheckboxLabeled(feature, ref enabled, (feature + "Tooltip").Translate());
			list.Gap();
		}
	}
}
