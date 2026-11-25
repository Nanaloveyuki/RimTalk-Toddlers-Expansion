using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Language
{
	public sealed class Hediff_ToddlerLanguageLearning : Hediff
	{
		private const int UpdateIntervalTicks = 2500;
		private bool _removeBecauseNotToddler;

		public override bool ShouldRemove => pawn == null || _removeBecauseNotToddler;
		public override bool Visible => Severity < 1f && base.Visible;
		public override int CurStageIndex => ToddlersExpansionSettings.UseCustomLearningSchedule
			? Mathf.Min(def.stages.Count - 1, LanguageLevelUtility.GetLanguageTier(Severity))
			: base.CurStageIndex;

		public float Progress01 => Mathf.Clamp01(Severity);

		public override void TickInterval(int delta)
		{
			base.TickInterval(delta);
			if (pawn == null || !pawn.IsHashIntervalTick(UpdateIntervalTicks, delta))
			{
				return;
			}

			if (!ToddlersCompatUtility.IsToddler(pawn))
			{
				_removeBecauseNotToddler = true;
				return;
			}

			float agingRateFactor = pawn.ageTracker?.BiologicalTicksPerTick ?? 1f;
			float factor = UpdateIntervalTicks * agingRateFactor;
			if (Severity < 1f)
			{
				Severity = LanguageLevelUtility.Advance(pawn, Severity, factor);
			}
		}

		public override string SeverityLabel
		{
			get
			{
				if (Severity <= 0f)
				{
					return null;
				}

				return Severity.ToStringPercent();
			}
		}

		public override string TipStringExtra
		{
			get
			{
				if (Severity <= 0f)
				{
					return base.TipStringExtra;
				}

				string descriptor = LanguageLevelUtility.GetPromptDescriptor(Severity);
				return $"Progress: {Severity.ToStringPercent()}\nSpeech: {descriptor}";
			}
		}
	}
}
