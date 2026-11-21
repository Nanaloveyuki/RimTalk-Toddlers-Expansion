using System.Collections.Generic;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.YayoAnimation
{
	public sealed class YayoAnimationSafeFallbackComponent : GameComponent
	{
		private const int SyncIntervalTicks = 15;
		private const int CurrentMapScanIntervalTicks = 90;
		private const int FullRescanIntervalTicks = 600;
		private int _nextSyncTick;
		private int _nextCurrentMapScanTick;
		private int _nextFullRescanTick;
		private readonly List<Pawn> _trackedPawnsBuffer = new List<Pawn>(64);

		public YayoAnimationSafeFallbackComponent(Game game)
		{
		}

		public override void StartedNewGame()
		{
			base.StartedNewGame();
			_nextSyncTick = 0;
			_nextCurrentMapScanTick = 0;
			_nextFullRescanTick = 0;
			YayoAnimationCompatUtility.ClearTrackedSafeFallbackPawns();
		}

		public override void LoadedGame()
		{
			base.LoadedGame();
			_nextSyncTick = 0;
			_nextCurrentMapScanTick = 0;
			_nextFullRescanTick = 0;
			YayoAnimationCompatUtility.ClearTrackedSafeFallbackPawns();
		}

		public override void GameComponentTick()
		{
			base.GameComponentTick();

			if (!YayoAnimationCompatUtility.IsYayoAnimationLoaded || Find.TickManager == null)
			{
				return;
			}

			int now = Find.TickManager.TicksGame;
			if (now < _nextSyncTick)
			{
				return;
			}

			_nextSyncTick = now + SyncIntervalTicks;
			bool fullRescan = now >= _nextFullRescanTick;
			if (fullRescan)
			{
				_nextFullRescanTick = now + FullRescanIntervalTicks;
				_nextCurrentMapScanTick = now + CurrentMapScanIntervalTicks;
				RescanAllMapsForCandidates();
			}
			else if (now >= _nextCurrentMapScanTick)
			{
				_nextCurrentMapScanTick = now + CurrentMapScanIntervalTicks;
				ScanMapCandidates(Find.CurrentMap);
			}

			SyncTrackedPawns(fullRescan);
		}

		private void SyncTrackedPawns(bool fullRescan)
		{
			Map currentMap = Find.CurrentMap;
			YayoAnimationCompatUtility.CopyTrackedSafeFallbackPawnsTo(_trackedPawnsBuffer);
			for (int i = 0; i < _trackedPawnsBuffer.Count; i++)
			{
				Pawn pawn = _trackedPawnsBuffer[i];
				Map pawnMap = pawn?.Map;
				bool onCurrentMap = pawnMap != null && pawnMap == currentMap;
				bool keepTracked = onCurrentMap || fullRescan
					? YayoAnimationCompatUtility.ShouldKeepSafeFallbackPawnTracked(pawn)
					: YayoAnimationCompatUtility.IsRelevantSmallPawnCandidate(pawn);
				if (!keepTracked)
				{
					YayoAnimationCompatUtility.UntrackSafeFallbackPawn(pawn);
				}
				else if (onCurrentMap)
				{
					YayoAnimationCompatUtility.SyncSafeNativePlayAnimation(pawn);
				}
			}

			_trackedPawnsBuffer.Clear();
		}

		private static void RescanAllMapsForCandidates()
		{
			if (Find.Maps == null)
			{
				return;
			}

			for (int i = 0; i < Find.Maps.Count; i++)
			{
				ScanMapCandidates(Find.Maps[i]);
			}
		}

		private static void ScanMapCandidates(Map map)
		{
			var pawns = map?.mapPawns?.AllPawnsSpawned;
			if (pawns == null)
			{
				return;
			}

			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn pawn = pawns[i];
				if (!YayoAnimationCompatUtility.ShouldKeepSafeFallbackPawnTracked(pawn))
				{
					continue;
				}

				YayoAnimationCompatUtility.TrackSafeFallbackPawn(pawn);
			}
		}
	}
}
