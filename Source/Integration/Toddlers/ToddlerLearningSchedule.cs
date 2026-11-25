using System;
using RimTalk_ToddlersExpansion.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	internal readonly struct ToddlerLearningSchedule
	{
		internal const int MaxPhaseDays = 180;

		private readonly float _firstDays;
		private readonly float _secondDays;
		private readonly float _thirdDays;
		private readonly float _secondStage;
		private readonly float _thirdStage;
		private readonly float _completion;
		internal float FirstPhaseDays => _firstDays;

		internal ToddlerLearningSchedule(int firstDays, float secondStage, int secondDays,
			float thirdStage = 1f, int thirdDays = 0, float completion = 1f)
		{
			_firstDays = Math.Clamp(firstDays, 1, MaxPhaseDays);
			_secondDays = Math.Clamp(secondDays, 1, MaxPhaseDays);
			_thirdDays = thirdStage < completion ? Math.Clamp(thirdDays, 1, MaxPhaseDays) : 0;
			_secondStage = secondStage;
			_thirdStage = thirdStage;
			_completion = completion;
		}

		internal static bool TryGetNativeSchedule(HediffDef def, out ToddlerLearningSchedule schedule)
		{
			schedule = default;
			if (!ToddlersExpansionSettings.UseCustomLearningSchedule)
			{
				return false;
			}

			if (def?.defName == "LearningToWalk" && def.stages?.Count == 2)
			{
				schedule = new ToddlerLearningSchedule(ToddlersExpansionSettings.CrawlingDays,
					def.stages[1].minSeverity, ToddlersExpansionSettings.WobblyWalkingDays);
				return true;
			}

			if (def?.defName == "LearningManipulation" && def.stages?.Count == 3)
			{
				schedule = new ToddlerLearningSchedule(ToddlersExpansionSettings.IneptManipulationDays,
					def.stages[1].minSeverity, ToddlersExpansionSettings.ClumsyManipulationDays,
					def.stages[2].minSeverity, ToddlersExpansionSettings.ConfidentManipulationDays);
				return true;
			}

			return false;
		}

		internal static float GetDaysSinceToddler(Pawn pawn) =>
			pawn.ageTracker.AgeBiologicalTicks / (float)GenDate.TicksPerDay
			- ToddlersCompatUtility.GetToddlerMinAgeYears(pawn) * GenDate.DaysPerYear;

		internal float GetProgress(float days)
		{
			if (days < _firstDays)
			{
				return Mathf.Lerp(0f, _secondStage, days / _firstDays);
			}

			days -= _firstDays;
			if (days < _secondDays)
			{
				return Mathf.Lerp(_secondStage, _thirdStage, days / _secondDays);
			}

			days -= _secondDays;
			return days < _thirdDays ? Mathf.Lerp(_thirdStage, _completion, days / _thirdDays) : 1f;
		}

		internal float Advance(float progress, float biologicalTicks)
		{
			if (progress >= _completion)
			{
				return 1f;
			}
			if (biologicalTicks <= 0f)
			{
				return progress;
			}

			return Mathf.Max(progress, GetProgress(GetElapsedDays(progress) + biologicalTicks / GenDate.TicksPerDay));
		}

		private float GetElapsedDays(float progress)
		{
			if (progress < _secondStage)
			{
				return _firstDays * Mathf.InverseLerp(0f, _secondStage, progress);
			}

			if (progress < _thirdStage)
			{
				return _firstDays + _secondDays * Mathf.InverseLerp(_secondStage, _thirdStage, progress);
			}

			return _firstDays + _secondDays + _thirdDays * Mathf.InverseLerp(_thirdStage, _completion, progress);
		}
	}
}
