using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
	internal static class ToddlerMutualPlayUtility
	{
		internal static bool TryFindPlayCell(Pawn pawn, Pawn partner, out IntVec3 cell)
		{
			cell = IntVec3.Invalid;
			if (pawn.Map == null || partner?.Map != pawn.Map)
			{
				return false;
			}

			int closest = int.MaxValue;
			foreach (IntVec3 candidate in GenAdj.CellsAdjacent8Way(partner))
			{
				int distance = pawn.Position.DistanceToSquared(candidate);
				if (distance < closest && CanUsePlayCell(pawn, partner, candidate))
				{
					cell = candidate;
					closest = distance;
				}
			}

			return cell.IsValid;
		}

		internal static bool CanUsePlayCell(Pawn pawn, Pawn partner, IntVec3 cell)
		{
			Map map = pawn.Map;
			return cell.InBounds(map) && cell != partner.Position
				&& cell.Standable(map) && !cell.IsForbidden(pawn)
				&& ReachabilityImmediate.CanReachImmediate(cell, partner, map, PathEndMode.Touch, pawn)
				&& pawn.CanReach(cell, PathEndMode.OnCell, Danger.Some);
		}
	}
}
