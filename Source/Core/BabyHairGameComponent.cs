using System;
using System.Collections.Generic;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Core
{
	public sealed class BabyHairGameComponent : GameComponent
	{
		private Dictionary<int, bool> _overrides = new Dictionary<int, bool>();
		private List<Pawn> _loadedBabies;
		private float _nextRosterRefresh;

		public BabyHairGameComponent(Game game)
		{
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref _overrides, "babyHairOverrides", LookMode.Value, LookMode.Value);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				_overrides ??= new Dictionary<int, bool>();
			}
		}

		public static IReadOnlyList<Pawn> GetLoadedBabies(bool refresh = false)
		{
			BabyHairGameComponent component = Current.Game?.GetComponent<BabyHairGameComponent>();
			if (Current.ProgramState != ProgramState.Playing || component == null || Find.Maps == null)
			{
				return Array.Empty<Pawn>();
			}

			float now = Time.realtimeSinceStartup;
			if (refresh || component._loadedBabies == null || now >= component._nextRosterRefresh)
			{
				component._loadedBabies = CollectLoadedBabies();
				component._nextRosterRefresh = now + 1f;
			}

			return component._loadedBabies;
		}

		private static List<Pawn> CollectLoadedBabies()
		{
			var babies = new List<Pawn>();
			foreach (Map map in Find.Maps)
			{
				List<Pawn> pawns = map?.mapPawns?.AllPawns;
				if (pawns == null)
				{
					continue;
				}

				foreach (Pawn pawn in pawns)
				{
					if (CanConfigureHair(pawn) && !pawn.Dead && !pawn.Destroyed)
					{
						babies.Add(pawn);
					}
				}
			}

			babies.Sort(CompareBabies);
			return babies;
		}

		private static int CompareBabies(Pawn left, Pawn right)
		{
			int groupOrder = YoungPawnIdentity.GetGroup(left).CompareTo(YoungPawnIdentity.GetGroup(right));
			if (groupOrder != 0)
			{
				return groupOrder;
			}
			int mapOrder = (left.MapHeld?.uniqueID ?? -1).CompareTo(right.MapHeld?.uniqueID ?? -1);
			return mapOrder != 0 ? mapOrder : left.thingIDNumber.CompareTo(right.thingIDNumber);
		}

		public static bool? GetOverride(Pawn pawn)
		{
			BabyHairGameComponent component = Current.Game?.GetComponent<BabyHairGameComponent>();
			if (pawn != null && component?._overrides?.TryGetValue(pawn.thingIDNumber, out bool enabled) == true)
			{
				return enabled;
			}

			return null;
		}

		public static void SetOverride(Pawn pawn, bool? enabled)
		{
			BabyHairGameComponent component = Current.Game?.GetComponent<BabyHairGameComponent>();
			if (component == null || !CanConfigureHair(pawn) || GetOverride(pawn) == enabled)
			{
				return;
			}

			if (enabled.HasValue)
			{
				component._overrides[pawn.thingIDNumber] = enabled.Value;
			}
			else
			{
				component._overrides.Remove(pawn.thingIDNumber);
			}

			RefreshPawn(pawn);
		}

		public static void RefreshLoadedBabies()
		{
			foreach (Pawn pawn in GetLoadedBabies(refresh: true))
			{
				RefreshPawn(pawn);
			}
		}

		internal static bool CanConfigureHair(Pawn pawn)
		{
			if (pawn?.RaceProps?.Humanlike != true || pawn.ageTracker == null)
			{
				return false;
			}

			DevelopmentalStage stage = pawn.DevelopmentalStage;
			return stage.Baby() || stage.Newborn() || (stage == DevelopmentalStage.Child && ToddlersCompatUtility.IsToddler(pawn));
		}

		internal static bool GroupDefaultEnablesHair(Pawn pawn)
		{
			if (pawn.ageTracker.AgeBiologicalYearsFloat >= 3f)
			{
				return false;
			}

			return YoungPawnIdentity.GetGroup(pawn) switch
			{
				YoungPawnGroup.Prisoner => ToddlersExpansionSettings.enablePrisonerBabyHair,
				YoungPawnGroup.Slave => ToddlersExpansionSettings.enableSlaveBabyHair,
				YoungPawnGroup.Colonist => ToddlersExpansionSettings.enableColonistBabyHair,
				_ => ToddlersExpansionSettings.enableUnder3HairRendering
			};
		}

		internal static void RefreshPawn(Pawn pawn)
		{
			if (pawn?.MapHeld != null && CanConfigureHair(pawn))
			{
				pawn.Drawer?.renderer?.SetAllGraphicsDirty();
			}
		}
	}
}
