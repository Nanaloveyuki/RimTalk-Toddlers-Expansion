using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimTalk_ToddlersExpansion.UI;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_ToddlerCarrying
	{
		public static void Init(HarmonyLib.Harmony harmony)
		{
			PropertyInfo drawPosProp = AccessTools.Property(typeof(Pawn), "DrawPos");
			if (drawPosProp != null)
			{
				MethodInfo getter = drawPosProp.GetGetMethod();
				if (getter != null)
				{
					harmony.Patch(getter, postfix: new HarmonyMethod(typeof(Patch_ToddlerCarrying), nameof(DrawPos_Postfix)));
				}
			}

			MethodInfo despawnMethod = AccessTools.Method(typeof(Pawn), "DeSpawn");
			if (despawnMethod != null)
			{
				harmony.Patch(despawnMethod, prefix: new HarmonyMethod(typeof(Patch_ToddlerCarrying), nameof(Pawn_DeSpawn_Prefix)));
			}

			MethodInfo spawnSetupMethod = AccessTools.Method(typeof(Pawn), "SpawnSetup", new[] { typeof(Map), typeof(bool) });
			if (spawnSetupMethod != null)
			{
				harmony.Patch(spawnSetupMethod, postfix: new HarmonyMethod(typeof(Patch_ToddlerCarrying), nameof(Pawn_SpawnSetup_Postfix)));
			}

			MethodInfo exitMapMethod = AccessTools.Method(typeof(Pawn), "ExitMap", new[] { typeof(bool), typeof(Rot4) });
			if (exitMapMethod != null)
			{
				harmony.Patch(exitMapMethod, prefix: new HarmonyMethod(typeof(Patch_ToddlerCarrying), nameof(Pawn_ExitMap_Prefix)));
			}

			MethodInfo setFactionMethod = AccessTools.Method(typeof(Pawn), nameof(Pawn.SetFaction), new[] { typeof(Faction), typeof(Pawn) });
			if (setFactionMethod != null)
			{
				harmony.Patch(setFactionMethod,
					prefix: new HarmonyMethod(typeof(Patch_ToddlerCarrying), nameof(Pawn_SetFaction_Prefix)),
					postfix: new HarmonyMethod(typeof(Patch_ToddlerCarrying), nameof(Pawn_SetFaction_Postfix)));
			}

			MethodInfo setGuestStatusMethod = AccessTools.Method(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.SetGuestStatus), new[] { typeof(Faction), typeof(GuestStatus) });
			if (setGuestStatusMethod != null)
			{
				harmony.Patch(setGuestStatusMethod, postfix: new HarmonyMethod(typeof(Patch_ToddlerCarrying), nameof(PawnGuestTracker_SetGuestStatus_Postfix)));
			}

			MethodInfo killMethod = AccessTools.Method(typeof(Pawn), "Kill");
			if (killMethod != null)
			{
				harmony.Patch(killMethod, prefix: new HarmonyMethod(typeof(Patch_ToddlerCarrying), nameof(Pawn_Kill_Prefix)));
			}

			MethodInfo makeDownedMethod = AccessTools.Method(typeof(Pawn_HealthTracker), "MakeDowned");
			if (makeDownedMethod != null)
			{
				harmony.Patch(makeDownedMethod, postfix: new HarmonyMethod(typeof(Patch_ToddlerCarrying), nameof(HealthTracker_MakeDowned_Postfix)));
			}

			MethodInfo getReportMethod = AccessTools.Method(typeof(JobDriver), "GetReport");
			if (getReportMethod != null)
			{
				harmony.Patch(getReportMethod, postfix: new HarmonyMethod(typeof(Patch_ToddlerCarrying), nameof(JobDriver_GetReport_Postfix)));
			}

			MethodInfo getGizmosMethod = AccessTools.Method(typeof(Pawn), "GetGizmos");
			if (getGizmosMethod != null)
			{
				harmony.Patch(getGizmosMethod, postfix: new HarmonyMethod(typeof(Patch_ToddlerCarrying), nameof(Pawn_GetGizmos_Postfix)));
			}

			MethodInfo bodyAngleMethod = AccessTools.Method(typeof(PawnRenderer), "BodyAngle");
			if (bodyAngleMethod != null)
			{
				harmony.Patch(bodyAngleMethod, postfix: new HarmonyMethod(typeof(Patch_ToddlerCarrying), nameof(BodyAngle_Postfix)));
			}

			MethodInfo layingFacingMethod = AccessTools.Method(typeof(PawnRenderer), "LayingFacing");
			if (layingFacingMethod != null)
			{
				harmony.Patch(layingFacingMethod, postfix: new HarmonyMethod(typeof(Patch_ToddlerCarrying), nameof(LayingFacing_Postfix)));
			}

			PropertyInfo carriedByProp = AccessTools.Property(typeof(Pawn), "CarriedBy");
			if (carriedByProp != null)
			{
				MethodInfo getter = carriedByProp.GetGetMethod();
				if (getter != null)
				{
					harmony.Patch(getter, postfix: new HarmonyMethod(typeof(Patch_ToddlerCarrying), nameof(CarriedBy_Postfix)));
				}
			}

			Type childcareUtilityType = AccessTools.TypeByName("RimWorld.ChildcareUtility");
			if (childcareUtilityType != null)
			{
				MethodInfo findUnsafeBabyMethod = AccessTools.Method(childcareUtilityType, "FindUnsafeBaby");
				if (findUnsafeBabyMethod != null)
				{
					harmony.Patch(findUnsafeBabyMethod, postfix: new HarmonyMethod(typeof(Patch_ToddlerCarrying), nameof(FindUnsafeBaby_Postfix)));
				}
			}

			Log.Message("[RimTalk_ToddlersExpansion] Toddler carrying patches initialized");
		}

		private static void DrawPos_Postfix(Pawn __instance, ref Vector3 __result)
		{
			if (__instance == null)
			{
				return;
			}

			Pawn carrier = ToddlerCarryingUtility.GetCarrier(__instance);
			if (carrier == null || !carrier.Spawned)
			{
				return;
			}

			Vector3 carrierPos = carrier.DrawPos;
			Rot4 carrierRotation = carrier.Rotation;
			Vector3 offset = GetScaledCarryOffset(carrier, carrierRotation);
			Vector3 animOffset = CarriedPlayAnimationTracker.GetAnimationOffset(carrier, __instance);

			// The renderer adds the toddler's life-stage offset after reading DrawPos.
			Vector3 bodyAnchorOffset = carrier.ageTracker.CurLifeStage.bodyDrawOffset
				- __instance.ageTracker.CurLifeStage.bodyDrawOffset;
			__result = carrierPos + bodyAnchorOffset + offset + animOffset;
		}

		private static Vector3 GetScaledCarryOffset(Pawn carrier, Rot4 carrierRotation)
		{
			Vector3 baseOffset = ToddlerCarryingUtility.GetCarryOffset(carrierRotation);
			if (carrier == null)
			{
				return baseOffset;
			}

			// Use body size as a height proxy. Keep the lateral hand offset fixed for shorter carriers.
			float heightScale = Mathf.Clamp(Mathf.Max(0.1f, carrier.BodySize), 0.55f, 1.35f);
			return new Vector3(baseOffset.x, baseOffset.y * heightScale, baseOffset.z * heightScale);
		}

		private static void Pawn_Tick_Postfix(Pawn __instance)
		{
			if (__instance == null || !__instance.Spawned)
			{
				return;
			}

			Pawn carrier = ToddlerCarryingUtility.GetCarrier(__instance);
			bool isBeingCarried = carrier != null;
			if (isBeingCarried && !ToddlerCarryingUtility.IsCarryRelationStillValid(carrier, __instance))
			{
				ToddlerCarryingUtility.DismountToddler(__instance);
				carrier = null;
				isBeingCarried = false;
			}

			if (isBeingCarried)
			{
				ToddlerCarryProtectionUtility.SetCarryProtectionActive(__instance, true);
			}
			else if (ToddlerCarryProtectionUtility.HasCarryProtection(__instance))
			{
				ToddlerCarryProtectionUtility.SetCarryProtectionActive(__instance, false);
			}

			if (carrier != null && carrier.Spawned && carrier.Map == __instance.Map)
			{
				if (__instance.Position != carrier.Position)
				{
					try
					{
						__instance.Position = carrier.Position;
					}
					catch
					{
					}
				}
			}

			if (ToddlerCarryingTracker.TryGetCarriedToddlersNoAlloc(__instance, out List<Pawn> toddlers))
			{
				for (int i = toddlers.Count - 1; i >= 0; i--)
				{
					Pawn toddler = toddlers[i];
					if (toddler == null || toddler.Dead || toddler.Destroyed || !toddler.Spawned)
					{
						ToddlerCarryingUtility.DismountToddler(toddler);
						continue;
					}

					if (!ToddlerCarryingUtility.IsCarryRelationStillValid(__instance, toddler))
					{
						ToddlerCarryingUtility.DismountToddler(toddler);
						continue;
					}

					if (toddler.Map != __instance.Map)
					{
						ToddlerCarryingUtility.DismountToddler(toddler);
					}
				}
			}
		}
		private static void Pawn_DeSpawn_Prefix(Pawn __instance)
		{
			if (__instance == null)
			{
				return;
			}

			ToddlerCarryDesireUtility.NotifyPawnDespawned(__instance);
			ToddlerSelfBathGameComponent.NotifyPawnDespawned(__instance);
			TryExitCarriedToddlersWithCarrier(__instance);
			ToddlerCarryingUtility.ClearAllCarryingRelations(__instance);
		}

		private static void Pawn_SpawnSetup_Postfix(Pawn __instance, Map map, bool respawningAfterLoad)
		{
			ToddlerCarryDesireUtility.NotifyPawnSpawned(__instance);
			ToddlerSelfBathGameComponent.NotifyPawnSpawned(__instance);
			LanguageLearningBootstrapComponent.NotifyPawnSpawned(__instance);
		}

		private static void Pawn_ExitMap_Prefix(Pawn __instance, bool allowedToJoinOrCreateCaravan, Rot4 exitDir)
		{
			if (__instance == null)
			{
				return;
			}

			int id = __instance.thingIDNumber;
			if (!_exitHandling.Add(id))
			{
				return;
			}

			try
			{
				TryExitCarriedToddlersOnExitMap(__instance, exitDir);
			}
			finally
			{
				_exitHandling.Remove(id);
			}
		}

		private static void Pawn_SetFaction_Prefix(Pawn __instance, ref Faction __state)
		{
			__state = __instance?.Faction;
		}

		private static void Pawn_SetFaction_Postfix(Pawn __instance, Faction newFaction, Faction __state)
		{
			if (__instance == null || __state == newFaction)
			{
				return;
			}

			ToddlerCarryDesireUtility.NotifyPawnFactionChanged(__instance);
			ToddlerSelfBathGameComponent.NotifyPawnFactionChanged(__instance);
			Patch_VisitorToddlerBabyFood.NotifyPawnFactionChanged(__instance);
			DropInvalidCarryRelations(__instance);
		}

		private static void PawnGuestTracker_SetGuestStatus_Postfix(Pawn ___pawn)
		{
			// Capturing a pawn can change its host while keeping its faction.
			ToddlerSelfBathGameComponent.NotifyPawnEligibilityChanged(___pawn);
		}

		private static void Pawn_Kill_Prefix(Pawn __instance)
		{
			if (__instance == null)
			{
				return;
			}

			ToddlerCarryDesireUtility.NotifyPawnKilled(__instance);
			ToddlerSelfBathGameComponent.NotifyPawnKilled(__instance);
			ToddlerCarryingUtility.ClearAllCarryingRelations(__instance);
		}

		private static void DropInvalidCarryRelations(Pawn pawn)
		{
			if (pawn == null)
			{
				return;
			}

			if (ToddlerCarryingTracker.TryGetCarriedToddlersNoAlloc(pawn, out List<Pawn> carriedToddlers))
			{
				for (int i = carriedToddlers.Count - 1; i >= 0; i--)
				{
					Pawn toddler = carriedToddlers[i];
					if (toddler == null || !ToddlerCarryingUtility.IsCarryRelationStillValid(pawn, toddler))
					{
						ToddlerCarryingUtility.DismountToddler(toddler);
					}
				}
			}

			if (!ToddlerCarryingUtility.IsBeingCarried(pawn))
			{
				return;
			}

			Pawn carrier = ToddlerCarryingUtility.GetCarrier(pawn);
			if (!ToddlerCarryingUtility.IsCarryRelationStillValid(carrier, pawn))
			{
				ToddlerCarryingUtility.DismountToddler(pawn);
			}
		}

		private static void TryExitCarriedToddlersOnExitMap(Pawn carrier, Rot4 exitDir)
		{
			if (carrier == null || !carrier.Spawned || !ToddlerCarryingUtility.IsCarryingToddler(carrier))
			{
				return;
			}

			ToddlerCarryingUtility.CopyCarriedToddlersTo(carrier, _exitMapCarryBuffer);
			for (int i = 0; i < _exitMapCarryBuffer.Count; i++)
			{
				Pawn toddler = _exitMapCarryBuffer[i];
				if (toddler == null || toddler.Dead || toddler.Destroyed || !toddler.Spawned || toddler.Map != carrier.Map)
				{
					continue;
				}

				if (!ToddlersCompatUtility.IsToddlerOrBaby(toddler) && toddler.DevelopmentalStage != DevelopmentalStage.Child)
				{
					continue;
				}

				if (!ToddlerCarryingUtility.IsCarryRelationStillValid(carrier, toddler))
				{
					ToddlerCarryingUtility.DismountToddler(toddler);
					continue;
				}

				if (TryExitMapImmediately(toddler, exitDir))
				{
					continue;
				}

				ToddlerCarryingUtility.DismountToddler(toddler);
				TryQueueExitMapJob(toddler);
			}
			_exitMapCarryBuffer.Clear();
		}

		private static void TryExitCarriedToddlersWithCarrier(Pawn carrier)
		{
			if (carrier == null || !carrier.Spawned || !ToddlerCarryingUtility.IsCarryingToddler(carrier))
			{
				return;
			}

			if (carrier.Faction == Faction.OfPlayer)
			{
				return;
			}

			if (!PawnUtility.IsExitingMap(carrier))
			{
				return;
			}

			Lord lord = carrier.GetLord();
			bool inExitToil = lord?.CurLordToil is LordToil_ExitMap ||
			                  lord?.CurLordToil?.GetType().Name == "LordToil_ExitMapAndEscortCarriers";
			if (!inExitToil)
			{
				return;
			}

			ToddlerCarryingUtility.CopyCarriedToddlersTo(carrier, _exitWithCarrierBuffer);
			for (int i = 0; i < _exitWithCarrierBuffer.Count; i++)
			{
				Pawn toddler = _exitWithCarrierBuffer[i];
				if (toddler == null || toddler.Dead || toddler.Destroyed || !toddler.Spawned || toddler.Map != carrier.Map)
				{
					continue;
				}

				if (!ToddlersCompatUtility.IsToddlerOrBaby(toddler) && toddler.DevelopmentalStage != DevelopmentalStage.Child)
				{
					continue;
				}

				if (!ToddlerCarryingUtility.IsCarryRelationStillValid(carrier, toddler))
				{
					ToddlerCarryingUtility.DismountToddler(toddler);
					continue;
				}

				if (!TryExitMapImmediately(toddler, carrier.Rotation))
				{
					TryQueueExitMapJob(toddler);
				}
			}
			_exitWithCarrierBuffer.Clear();
		}

		private static bool TryExitMapImmediately(Pawn pawn, Rot4 facing)
		{
			if (_pawnExitMapMethod == null || pawn == null)
			{
				return false;
			}

			try
			{
				_pawnExitMapMethod.Invoke(pawn, new object[] { false, facing });
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static void TryQueueExitMapJob(Pawn pawn)
		{
			if (pawn == null || pawn.Dead || pawn.Destroyed || !pawn.Spawned)
			{
				return;
			}

			if (pawn.Downed
				|| HealthAIUtility.ShouldSeekMedicalRest(pawn)
				|| pawn.health?.capacities?.CapableOf(PawnCapacityDefOf.Moving) != true)
			{
				return;
			}

			if (PawnUtility.IsExitingMap(pawn) || pawn.jobs?.curJob?.exitMapOnArrival == true)
			{
				return;
			}

			if (!RCellFinder.TryFindBestExitSpot(pawn, out IntVec3 spot, TraverseMode.ByPawn, canBash: false))
			{
				return;
			}

			Job job = JobMaker.MakeJob(JobDefOf.Goto, spot);
			job.exitMapOnArrival = true;
			job.locomotionUrgency = LocomotionUrgency.Jog;
			pawn.jobs?.StartJob(job, JobCondition.InterruptForced);
		}

		private static void HealthTracker_MakeDowned_Postfix(Pawn_HealthTracker __instance)
		{
			if (__instance == null)
			{
				return;
			}

			Pawn pawn = _healthTrackerPawnField?.GetValue(__instance) as Pawn;
			if (pawn == null)
			{
				return;
			}

			ToddlerCarryingUtility.ClearAllCarryingRelations(pawn);
		}

		private static readonly FieldInfo _healthTrackerPawnField = AccessTools.Field(typeof(Pawn_HealthTracker), "pawn");
		private static readonly MethodInfo _pawnExitMapMethod = AccessTools.Method(typeof(Pawn), "ExitMap", new[] { typeof(bool), typeof(Rot4) });
		private static readonly FieldInfo _pawnRendererPawnField = AccessTools.Field(typeof(PawnRenderer), "pawn");
		private static readonly HashSet<int> _exitHandling = new HashSet<int>();
		private static readonly List<Pawn> _exitMapCarryBuffer = new List<Pawn>(4);
		private static readonly List<Pawn> _exitWithCarrierBuffer = new List<Pawn>(4);

		private static bool RenderPawnAt_Prefix(PawnRenderer __instance, Vector3 drawLoc, Rot4? rotOverride, bool neverAimWeapon)
		{
			Pawn pawn = GetPawnFromRenderer(__instance);
			if (pawn == null)
			{
				return true;
			}

			Pawn carrier = ToddlerCarryingUtility.GetCarrier(pawn);
			if (carrier == null)
			{
				return true;
			}

			return true;
		}

		private static Pawn GetPawnFromRenderer(PawnRenderer renderer)
		{
			if (renderer == null || _pawnRendererPawnField == null)
			{
				return null;
			}

			return _pawnRendererPawnField.GetValue(renderer) as Pawn;
		}

		private static void JobDriver_GetReport_Postfix(JobDriver __instance, ref string __result)
		{
			if (__instance == null || string.IsNullOrEmpty(__result))
			{
				return;
			}

			Pawn pawn = __instance.pawn;
			if (pawn == null)
			{
				return;
			}

			if (!ToddlerCarryingUtility.IsCarryingToddler(pawn))
			{
				return;
			}

			if (!ToddlerCarryingUtility.TryGetCarriedToddlersNoAlloc(pawn, out List<Pawn> carriedToddlers) || carriedToddlers.Count == 0)
			{
				return;
			}

			string toddlerNames = carriedToddlers[0].LabelShort;
			for (int i = 1; i < carriedToddlers.Count; i++)
			{
				toddlerNames += ", " + carriedToddlers[i].LabelShort;
			}
			string carryingText = "RimTalk_CarryingToddler".Translate(toddlerNames);

			__result = $"{__result} - {carryingText}";
		}

		private static IEnumerable<Gizmo> Pawn_GetGizmos_Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
		{
			foreach (Gizmo gizmo in __result)
			{
				yield return gizmo;
			}

			if (__instance != null && ToddlerCarryingUtility.IsCarryingToddler(__instance))
			{
				foreach (Gizmo playGizmo in Gizmo_CarriedPlay.GetCarriedPlayGizmos(__instance))
				{
					yield return playGizmo;
				}
			}
		}

		private static void BodyAngle_Postfix(PawnRenderer __instance, ref float __result)
		{
			Pawn pawn = GetPawnFromRenderer(__instance);
			if (pawn == null)
			{
				return;
			}

			if (!pawn.DevelopmentalStage.Baby())
			{
				return;
			}

			if (!ToddlerCarryingUtility.IsBeingCarried(pawn))
			{
				return;
			}

			// Vanilla draws downed babies lying down even while they are held upright.
			__result = 0f;
		}

		private static void LayingFacing_Postfix(PawnRenderer __instance, ref Rot4 __result)
		{
			Pawn pawn = GetPawnFromRenderer(__instance);
			if (pawn == null)
			{
				return;
			}

			if (!pawn.DevelopmentalStage.Baby())
			{
				return;
			}

			Pawn carrier = ToddlerCarryingUtility.GetCarrier(pawn);
			if (carrier == null)
			{
				return;
			}

			__result = carrier.Rotation;
		}

		// Vanilla uses CarriedBy for draw order. Give the vanilla carry tracker precedence.
		private static void CarriedBy_Postfix(Pawn __instance, ref Pawn __result)
		{
			if (__result != null)
			{
				return;
			}

			Pawn carrier = ToddlerCarryingUtility.GetCarrier(__instance);
			if (carrier != null)
			{
				__result = carrier;
			}
		}

		// A baby already held by an adult can skip the bring-to-safety job.
		private static void FindUnsafeBaby_Postfix(Pawn mom, AutofeedMode priorityLevel, ref Pawn __result)
		{
			if (__result == null)
			{
				return;
			}

			if (ToddlerCarryingUtility.IsBeingCarried(__result))
			{
				__result = null;
			}
		}
	}
}
