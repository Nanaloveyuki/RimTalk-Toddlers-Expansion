using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimTalk_ToddlersExpansion.Harmony
{
	public static class Patch_VisitorToddlerBabyFood
	{
		private const int CarryAssignmentDelayTicks = 60;
		private const int CarryAssignmentRetryTicks = 30;
		private const int MaxCarryAssignmentRetries = 8;

		private static readonly HashSet<int> _processedFoodPawnIds = new HashSet<int>();
		private static HashSet<int> _processedPawnIds = new HashSet<int>();
		// 缓存已经处理过背负关系的pawn
		private static HashSet<int> _processedCarryingIds = new HashSet<int>();
		private static readonly List<PendingCarryAssignment> _pendingCarryAssignments = new List<PendingCarryAssignment>();
		private static int _lastCleanupTick = 0;
		private const int CleanupInterval = 60000; // 每小时清理一次缓存

		private sealed class PendingCarryAssignment
		{
			public PendingCarryAssignment(Pawn toddler, int dueTick)
			{
				Toddler = toddler;
				DueTick = dueTick;
				Attempts = 0;
			}

			public Pawn Toddler;
			public int DueTick;
			public int Attempts;
		}

		public static void Init(HarmonyLib.Harmony harmony)
		{
			// Some visitor mods create pawns without using the group generator.
			MethodInfo spawnMethod = AccessTools.Method(typeof(GenSpawn), "Spawn", 
				new[] { typeof(Thing), typeof(IntVec3), typeof(Map), typeof(Rot4), typeof(WipeMode), typeof(bool), typeof(bool) });
			
			if (spawnMethod != null)
			{
				MethodInfo postfix = AccessTools.Method(typeof(Patch_VisitorToddlerBabyFood), nameof(GenSpawn_Spawn_Postfix));
				harmony.Patch(spawnMethod, postfix: new HarmonyMethod(postfix));
				
				if (ToddlersExpansionSettings.ShouldEmitVerboseDebugLogs)
				{
					Log.Message("[RimTalk_ToddlersExpansion] Patched GenSpawn.Spawn for visitor toddler baby food injection");
				}
			}
			else
			{
				Log.Warning("[RimTalk_ToddlersExpansion] Failed to find GenSpawn.Spawn method for patching");
			}
		}

		private static void GenSpawn_Spawn_Postfix(Thing __result, bool respawningAfterLoad)
		{
			if (__result is not Pawn pawn || pawn.RaceProps?.Humanlike != true)
			{
				return;
			}

			// 检查发育阶段 - 快速排除成年人
			DevelopmentalStage stage = pawn.DevelopmentalStage;
			if (stage != DevelopmentalStage.Baby && stage != DevelopmentalStage.Newborn && stage != DevelopmentalStage.Child)
			{
				// 如果不是婴儿/幼儿/儿童阶段，检查Toddlers mod的判断
				if (!ToddlersCompatUtility.IsToddlerOrBaby(pawn))
				{
					return;
				}
			}

			CleanupCacheIfNeeded();
			if (!respawningAfterLoad)
			{
				TrySupplyBabyFoodOnMap(pawn);
			}

			if (_processedPawnIds.Contains(pawn.thingIDNumber))
			{
				return;
			}

			// 检查是否是访客幼儿（包含更多条件检查）
			if (!IsFriendlyYoungVisitor(pawn))
			{
				return;
			}

			// 标记为已处理
			TryNormalizeVisitorTradeSilver(pawn);
			_processedPawnIds.Add(pawn.thingIDNumber);

			bool isToddler = ToddlersCompatUtility.IsToddlerOrBaby(pawn);
			if (isToddler)
			{
				ToddlerPawnGenerationUtility.EnsureToddlerFallbackApparel(pawn, pawn.MapHeld?.Tile ?? -1);
			}
			else
			{
				if (!respawningAfterLoad)
				{
					TryEnsureChildTravelFood(pawn);
				}
				ToddlerPawnGenerationUtility.EnsureChildFallbackApparel(pawn);
			}

			TryApplyNewEnvironmentMood(pawn);

			// 延后处理背负关系（需要等其他成员也spawn完成）
			// 使用延迟调用，在下一帧处理背负关系
			if (isToddler)
			{
				TryScheduleCarryingAssignment(pawn);
			}
		}

		internal static void TrySupplyBabyFoodOnMap(Pawn pawn)
		{
			if (pawn?.RaceProps?.Humanlike != true
				|| !ToddlerFoodSettings.AllowsOnPlayerHome(ToddlerFoodSettings.SupplyFood, pawn)
				|| !ToddlersCompatUtility.IsToddlerOrBaby(pawn)
				|| !_processedFoodPawnIds.Add(pawn.thingIDNumber))
			{
				return;
			}

			ToddlerPawnGenerationUtility.TryTopUpBabyFood(pawn);
		}

		/// <summary>
		/// 尝试安排背负关系分配
		/// 使用延迟机制，确保同一Lord的所有成员都已spawn
		/// </summary>
		private static void TryNormalizeVisitorTradeSilver(Pawn pawn)
		{
			Lord lord = pawn?.GetLord();
			if (lord?.ownedPawns == null || lord.ownedPawns.Count == 0)
			{
				return;
			}

			TravelingPawnInjectionUtility.NormalizeSilverToAdultInventories(lord.ownedPawns);
		}

		private static void TryScheduleCarryingAssignment(Pawn toddler)
		{
			if (toddler == null || _processedCarryingIds.Contains(toddler.thingIDNumber))
			{
				return;
			}

			// 标记为已处理背负关系
			_processedCarryingIds.Add(toddler.thingIDNumber);

			// 如果已经被背着，不需要处理
			if (ToddlerCarryingUtility.IsBeingCarried(toddler))
			{
				return;
			}

			// 在 GenSpawn 调用栈之外处理，避免在 spawn 过程中直接改状态。
			int nowTick = Find.TickManager?.TicksGame ?? 0;
			_pendingCarryAssignments.Add(new PendingCarryAssignment(toddler, nowTick + CarryAssignmentDelayTicks));

			if (ToddlersExpansionSettings.ShouldEmitVerboseDebugLogs)
			{
				Log.Message($"[RimTalk_ToddlersExpansion][CarryDebug] Scheduled carry assignment for {toddler.LabelShort} ({toddler.thingIDNumber}).");
			}
		}

		public static void ProcessPendingCarryingAssignments(int maxAssignmentsPerTick = 3)
		{
			if (_pendingCarryAssignments.Count == 0 || maxAssignmentsPerTick <= 0)
			{
				return;
			}

			int nowTick = Find.TickManager?.TicksGame ?? 0;
			int processed = 0;

			for (int i = _pendingCarryAssignments.Count - 1; i >= 0; i--)
			{
				if (processed >= maxAssignmentsPerTick)
				{
					break;
				}

				PendingCarryAssignment pending = _pendingCarryAssignments[i];
				if (pending == null || pending.DueTick > nowTick)
				{
					continue;
				}

				processed++;
				Pawn toddler = pending.Toddler;
				if (toddler == null || toddler.Dead || toddler.Destroyed)
				{
					_pendingCarryAssignments.RemoveAt(i);
					continue;
				}

				if (!toddler.Spawned || toddler.MapHeld == null)
				{
					if (pending.Attempts >= MaxCarryAssignmentRetries)
					{
						_pendingCarryAssignments.RemoveAt(i);
					}
					else
					{
						pending.Attempts++;
						pending.DueTick = nowTick + CarryAssignmentRetryTicks;
					}
					continue;
				}

				TryAssignCarryingForToddler(toddler);
				bool carried = ToddlerCarryingUtility.IsBeingCarried(toddler);
				if (carried || !IsVisitorToddler(toddler) || pending.Attempts >= MaxCarryAssignmentRetries)
				{
					_pendingCarryAssignments.RemoveAt(i);
				}
				else
				{
					pending.Attempts++;
					pending.DueTick = nowTick + CarryAssignmentRetryTicks;
				}
			}
		}

		/// <summary>
		/// 尝试为单个幼儿分配背负关系
		/// </summary>
		private static void TryAssignCarryingForToddler(Pawn toddler)
		{
			if (toddler == null || toddler.Dead || toddler.Destroyed || !toddler.Spawned)
			{
				return;
			}

			// 如果已经被背着，不需要处理
			if (ToddlerCarryingUtility.IsBeingCarried(toddler))
			{
				return;
			}

			// 检查是否仍然是访客幼儿
			if (!IsVisitorToddler(toddler))
			{
				return;
			}

			// 获取幼儿所属的Lord
			Lord lord = toddler.GetLord();
			if (lord == null)
			{
				if (ToddlersExpansionSettings.ShouldEmitVerboseDebugLogs)
				{
					Log.Warning($"[RimTalk_ToddlersExpansion][CarryDebug] Skip carry assignment for {toddler.LabelShort}: lord is null.");
				}
				return;
			}

			if (lord.CurLordToil == null)
			{
				if (ToddlersExpansionSettings.ShouldEmitVerboseDebugLogs)
				{
					Log.Warning($"[RimTalk_ToddlersExpansion][CarryDebug] Skip carry assignment for {toddler.LabelShort}: lord {lord.loadID} CurLordToil is null.");
				}
				return;
			}

			// 在Lord成员中找到可以背负幼儿的成年人
			List<Pawn> lordMembers = lord.ownedPawns;
			if (lordMembers == null || lordMembers.Count == 0)
			{
				return;
			}

			// 找到一个可用的carrier
			foreach (Pawn potentialCarrier in lordMembers)
			{
				if (potentialCarrier == null || potentialCarrier == toddler)
				{
					continue;
				}

				if (!ToddlerCarryingUtility.IsValidCarrier(potentialCarrier))
				{
					continue;
				}

				if (!potentialCarrier.Spawned || potentialCarrier.MapHeld != toddler.MapHeld)
				{
					if (ToddlersExpansionSettings.ShouldEmitVerboseDebugLogs)
					{
						Log.Message($"[RimTalk_ToddlersExpansion][CarryDebug] Ignore carrier {potentialCarrier.LabelShort} for {toddler.LabelShort}: not spawned or different map.");
					}
					continue;
				}

				// 检查这个carrier是否还有容量
				if (ToddlerCarryingUtility.GetCarriedToddlerCount(potentialCarrier) >= ToddlerCarryingUtility.GetMaxCarryCapacity(potentialCarrier))
				{
					continue;
				}

				// 尝试分配背负关系
				if (ToddlerCarryingUtility.TryMountToddlerAutomatically(potentialCarrier, toddler))
				{
					if (ToddlersExpansionSettings.ShouldEmitVerboseDebugLogs)
					{
						Log.Message($"[RimTalk_ToddlersExpansion] 延后分配背负: {potentialCarrier.Name} 背起访客幼儿 {toddler.Name}");
					}
					return;
				}

				if (ToddlersExpansionSettings.ShouldEmitVerboseDebugLogs)
				{
					Log.Message($"[RimTalk_ToddlersExpansion][CarryDebug] TryMountToddler failed: carrier={potentialCarrier.LabelShort}, toddler={toddler.LabelShort}");
				}
			}

			if (ToddlersExpansionSettings.ShouldEmitVerboseDebugLogs)
			{
				Log.Message($"[RimTalk_ToddlersExpansion] 无法为访客幼儿 {toddler.Name} 找到可用的carrier");
			}
		}

		private static bool IsVisitorToddler(Pawn pawn)
		{
			return IsFriendlyYoungVisitor(pawn) && ToddlersCompatUtility.IsToddlerOrBaby(pawn);
		}

		private static bool IsFriendlyYoungVisitor(Pawn pawn)
		{
			if (pawn == null || pawn.Dead || pawn.Destroyed)
			{
				return false;
			}

			// 必须是幼儿或婴儿
			if (!ToddlersCompatUtility.IsToddlerOrBaby(pawn)
				&& pawn.DevelopmentalStage != DevelopmentalStage.Child)
			{
				return false;
			}

			// 必须是非玩家派系
			Faction faction = pawn.Faction;
			if (faction == null || faction == Faction.OfPlayer)
			{
				return false;
			}

			// 不能是敌对派系
			if (faction.HostileTo(Faction.OfPlayer))
			{
				return false;
			}

			// 不能是囚犯
			if (pawn.IsPrisoner || pawn.IsPrisonerOfColony)
			{
				return false;
			}

			// 必须在玩家的地图上（确保是来访的幼儿，而不是在世界地图上的幼儿）
			Map map = pawn.Map;
			if (map == null || !map.IsPlayerHome)
			{
				return false;
			}

			return true;
		}

		private static void TryEnsureChildTravelFood(Pawn child)
		{
			if (child == null
				|| child.DevelopmentalStage != DevelopmentalStage.Child
				|| ToddlersCompatUtility.IsToddlerOrBaby(child))
			{
				return;
			}

			IEnumerable<Pawn> groupMembers = child.GetLord()?.ownedPawns;
			if (groupMembers == null)
			{
				groupMembers = child.Map?.mapPawns?.AllPawnsSpawned
					.Where(candidate => candidate?.Faction == child.Faction);
			}

			ToddlerPawnGenerationUtility.TryInjectChildTravelFood(child, groupMembers);
		}

		private static void TryApplyNewEnvironmentMood(Pawn pawn)
		{
			if (!ShouldReceiveNewEnvironmentMood(pawn))
			{
				return;
			}

			MemoryThoughtHandler memories = pawn.needs?.mood?.thoughts?.memories;
			ThoughtDef thought = ToddlersExpansionThoughtDefOf.RimTalk_VisitorBabyNewEnvironment;
			if (memories == null || thought == null)
			{
				return;
			}

			memories.RemoveMemoriesOfDef(thought);
			memories.TryGainMemory(thought);
		}

		public static void NotifyPawnFactionChanged(Pawn pawn)
		{
			if (pawn?.needs?.mood?.thoughts?.memories == null)
			{
				return;
			}

			ThoughtDef thought = ToddlersExpansionThoughtDefOf.RimTalk_VisitorBabyNewEnvironment;
			if (thought == null)
			{
				return;
			}

			if (ShouldReceiveNewEnvironmentMood(pawn))
			{
				return;
			}

			pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDef(thought);
		}

		public static void CleanupInvalidNewEnvironmentMoodOnMaps()
		{
			if (Find.Maps == null || Find.Maps.Count == 0)
			{
				return;
			}

			for (int i = 0; i < Find.Maps.Count; i++)
			{
				Map map = Find.Maps[i];
				IReadOnlyList<Pawn> pawns = map?.mapPawns?.AllPawnsSpawned;
				if (pawns == null)
				{
					continue;
				}

				for (int j = 0; j < pawns.Count; j++)
				{
					NotifyPawnFactionChanged(pawns[j]);
				}
			}
		}

		private static bool ShouldReceiveNewEnvironmentMood(Pawn pawn)
		{
			if (!IsVisitorToddler(pawn))
			{
				return false;
			}

			DevelopmentalStage stage = pawn.DevelopmentalStage;
			return stage == DevelopmentalStage.Newborn || ToddlersCompatUtility.IsToddler(pawn);
		}

		private static bool IsVisitorBaby(Pawn pawn)
		{
			if (!IsVisitorToddler(pawn))
			{
				return false;
			}

			DevelopmentalStage stage = pawn.DevelopmentalStage;
			return stage == DevelopmentalStage.Newborn || stage == DevelopmentalStage.Baby;
		}

		private static void CleanupCacheIfNeeded()
		{
			int currentTick = Find.TickManager?.TicksGame ?? 0;
			if (currentTick - _lastCleanupTick < CleanupInterval)
			{
				return;
			}

			_lastCleanupTick = currentTick;
			
			// 清理缓存中不再存在的pawn IDs
			// 为了简单起见，我们只在缓存过大时清空
			if (_processedPawnIds.Count > 1000 || _processedCarryingIds.Count > 1000 || _processedFoodPawnIds.Count > 1000)
			{
				_processedFoodPawnIds.Clear();
				_processedPawnIds.Clear();
				_processedCarryingIds.Clear();
				
				if (ToddlersExpansionSettings.ShouldEmitVerboseDebugLogs)
				{
					Log.Message("[RimTalk_ToddlersExpansion] 清理访客幼儿处理缓存");
				}
			}
		}

		/// <summary>
		/// 在游戏加载或新游戏开始时清理缓存
		/// </summary>
		public static void ClearCache()
		{
			_processedFoodPawnIds.Clear();
			_processedPawnIds.Clear();
			_processedCarryingIds.Clear();
			_pendingCarryAssignments.Clear();
			_lastCleanupTick = 0;
		}

		/// <summary>
		/// 清理无效的背负关系（carrier不存在或已离开地图）
		/// 这可以在GameComponent的tick中定期调用
		/// </summary>
		public static void CleanupInvalidCarryingRelations()
		{
			// 这个功能由ToddlerCarryingTracker.CleanupInvalidEntries处理
			// 这里只是提供一个入口点
		}
	}
}
