using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	public static class CarriedPlayAnimationTracker
	{
		public enum CarriedPlayAnimationType
		{
			None,
			TossUp,
			SpinAround
		}

		private class AnimationData
		{
			public CarriedPlayAnimationType Type;
			public int StartTick;
			public Pawn Carrier;
			public Pawn Toddler;
		}

		private static readonly Dictionary<Pawn, AnimationData> ActiveAnimations = new Dictionary<Pawn, AnimationData>();

		public static void StartTossUpAnimation(Pawn carrier, Pawn toddler)
		{
			StartAnimation(carrier, toddler, CarriedPlayAnimationType.TossUp);
		}

		public static void StartSpinAroundAnimation(Pawn carrier, Pawn toddler)
		{
			StartAnimation(carrier, toddler, CarriedPlayAnimationType.SpinAround);
		}

		private static void StartAnimation(Pawn carrier, Pawn toddler, CarriedPlayAnimationType type)
		{
			if (carrier == null || toddler == null)
			{
				return;
			}

			ActiveAnimations[carrier] = new AnimationData
			{
				Type = type,
				StartTick = Find.TickManager.TicksGame,
				Carrier = carrier,
				Toddler = toddler
			};
		}

		public static void StopAnimation(Pawn carrier, Pawn toddler)
		{
			if (carrier != null)
			{
				ActiveAnimations.Remove(carrier);
			}
		}

		public static CarriedPlayAnimationType GetCurrentAnimationType(Pawn carrier)
		{
			if (carrier == null || !ActiveAnimations.TryGetValue(carrier, out AnimationData data))
			{
				return CarriedPlayAnimationType.None;
			}

			return data.Type;
		}

		public static bool HasActiveAnimation(Pawn carrier)
		{
			return carrier != null && ActiveAnimations.ContainsKey(carrier);
		}

		public static Vector3 GetAnimationOffset(Pawn carrier, Pawn toddler)
		{
			if (carrier == null || !ActiveAnimations.TryGetValue(carrier, out AnimationData data))
			{
				return Vector3.zero;
			}

			int elapsed = Find.TickManager.TicksGame - data.StartTick;
			float progress = elapsed / 60f;

			switch (data.Type)
			{
				case CarriedPlayAnimationType.TossUp:
					return GetTossUpOffset(progress);
				case CarriedPlayAnimationType.SpinAround:
					return Vector3.zero;
				default:
					return Vector3.zero;
			}
		}

		public static float GetAnimationRotation(Pawn carrier)
		{
			if (carrier == null || !ActiveAnimations.TryGetValue(carrier, out AnimationData data))
			{
				return 0f;
			}

			if (data.Type != CarriedPlayAnimationType.SpinAround)
			{
				return 0f;
			}

			int elapsed = Find.TickManager.TicksGame - data.StartTick;
			float progress = elapsed / 60f;

			return progress * 360f;
		}

		private static Vector3 GetTossUpOffset(float progress)
		{
			float height = Mathf.Sin(progress * Mathf.PI * 4f) * 0.3f;
			return new Vector3(0f, 0f, height);
		}

		public static void ClearAll()
		{
			ActiveAnimations.Clear();
		}

		public static void ClearPawn(Pawn pawn)
		{
			if (pawn != null)
			{
				ActiveAnimations.Remove(pawn);
			}
		}
	}
}
