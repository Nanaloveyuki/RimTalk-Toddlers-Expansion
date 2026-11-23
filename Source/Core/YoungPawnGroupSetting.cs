using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Core
{
	internal enum YoungPawnGroup
	{
		Prisoner,
		Slave,
		Colonist,
		NonColonist
	}

	internal static class YoungPawnIdentity
	{
		public static YoungPawnGroup GetGroup(Pawn pawn)
		{
			if (pawn?.IsPrisoner == true)
			{
				return YoungPawnGroup.Prisoner;
			}
			if (pawn?.IsSlave == true)
			{
				return YoungPawnGroup.Slave;
			}

			// IsColonist changes while a slave is in a mental state.
			return pawn?.Faction?.IsPlayer == true ? YoungPawnGroup.Colonist : YoungPawnGroup.NonColonist;
		}
	}

	internal sealed class YoungPawnGroupSetting(bool prisoner, bool slave, bool colonist, bool nonColonist)
	{
		private readonly bool[] defaults = [prisoner, slave, colonist, nonColonist];
		private readonly bool[] enabled = [prisoner, slave, colonist, nonColonist];

		public bool this[YoungPawnGroup group]
		{
			get => enabled[(int)group];
			set => enabled[(int)group] = value;
		}

		public bool Allows(Pawn pawn) => pawn != null && this[YoungPawnIdentity.GetGroup(pawn)];

		public void ExposeData(string prefix)
		{
			foreach (YoungPawnGroup group in System.Enum.GetValues<YoungPawnGroup>())
			{
				int index = (int)group;
				Scribe_Values.Look(ref enabled[index], prefix + group, defaults[index]);
			}
		}
	}
}
