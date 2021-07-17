using System;
using BattleTech;
using BattleTech.UI;
using Harmony;
using UnityEngine;
using static GalaxyatWar.Logger;
// ReSharper disable InconsistentNaming

namespace GalaxyatWar
{
    public class Misc
    {
        [HarmonyPatch(typeof(TaskTimelineWidget), "OnTaskDetailsClicked")]
        public static class TaskTimelineWidgetOnTaskDetailsClickedPatch
        {
            public static bool Prefix(TaskManagementElement element)
            {
                if (Globals.WarStatusTracker == null || Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete"))
                    return true;

                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    AdvanceToTask.StartAdvancing(element.Entry);
                    return false;
                }

                return true;
            }
        }
    }
}
