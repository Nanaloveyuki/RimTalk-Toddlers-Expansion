using RimWorld;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Toddlers
{
    public class MidnightSnackGameComponent : GameComponent
    {
        private readonly Game game;
        private int ticksUntilNextCheck;

        public MidnightSnackGameComponent(Game game)
        {
            this.game = game;
        }

        public override void GameComponentTick()
        {
            int tick = game.tickManager.TicksGame;
            if (tick < ticksUntilNextCheck)
                return;

            ticksUntilNextCheck = tick + GenDate.TicksPerHour;

            foreach (Map map in game.Maps)
            {
                if (IsSnackHour(GenLocalDate.HourOfDay(map)))
                {
                    QueueSnacks(map);
                }
            }
        }

        private static void QueueSnacks(Map map)
        {
            foreach (Pawn pawn in map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer))
            {
                if (pawn.jobs?.jobQueue == null || pawn.CurJob != null || pawn.Downed || pawn.Dead || !pawn.Awake()
                    || (pawn.DevelopmentalStage != DevelopmentalStage.Child && !ToddlersCompatUtility.IsToddler(pawn)))
                    continue;

                var jobGiver = new JobGiver_MidnightSnack();
                var thinkResult = jobGiver.TryIssueJobPackage(pawn, default);
                if (thinkResult.Job != null)
                {
                    pawn.jobs.jobQueue.EnqueueFirst(thinkResult.Job);
                }
            }
        }

        private static bool IsSnackHour(int hour)
        {
            return hour >= 0 && hour <= 3 || hour >= 12 && hour <= 15;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksUntilNextCheck, "ticksUntilNextCheck");
        }
    }
}
