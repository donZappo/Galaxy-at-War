using System;
using BattleTech;
using static GalaxyatWar.Globals;

namespace GalaxyatWar
{
    public static class AdvanceToTask
    {
        private static WorkOrderEntry advancingTo;
        private static float oldDayElapseTimeNormal;

        public static void StartAdvancing(WorkOrderEntry entry)
        {
            if (Sim.CurRoomState != DropshipLocation.SHIP)
                return;

            advancingTo = entry;
            Sim.SetTimeMoving(true);

            // set the elapseTime variable so that the days pass faster
            if (Math.Abs(Sim.Constants.Time.DayElapseTimeNormal - Settings.AdvanceToTaskTime) > 0.01)
            {
                oldDayElapseTimeNormal = Sim.Constants.Time.DayElapseTimeNormal;
                Sim.Constants.Time.DayElapseTimeNormal = Settings.AdvanceToTaskTime;
            }
        }

        public static void StopAdvancing()
        {
            if (advancingTo == null)
                return;

            advancingTo = null;

            Sim.Constants.Time.DayElapseTimeNormal = oldDayElapseTimeNormal;
            Sim.SetTimeMoving(false);
        }

        public static void OnDayAdvance()
        {
            if (advancingTo == null)
                return;

            var activeItems = TaskTimelineWidget.ActiveItems;

            // if timeline doesn't contain advancingTo or advancingTo is over
            if (!activeItems.ContainsKey(advancingTo) || advancingTo.IsCostPaid())
                StopAdvancing();
        }
    }
}
