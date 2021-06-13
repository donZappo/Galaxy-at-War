using System;
using BattleTech;
using BattleTech.UI;
using Harmony;
using UnityEngine;
using static GalaxyatWar.Logger;

namespace GalaxyatWar
{
    public class MoveToAnotherMod
    {
        // this belongs in a different mod
        [HarmonyPatch(typeof(Shop), "OnItemCollectionRetrieved")]
        public class ShopOnItemCollectionRetrievedHackFixPatch
        {
            public static bool Prefix(Shop __instance, ItemCollectionDef def)
            {
                if (def == null)
                {
                    LogDebug($"{__instance.system.Name} has invalid ItemCollectionDef.");
                }

                return def != null;
            }
        }

        // TODO move to another mod
        [HarmonyPatch(typeof(ListElementController_InventoryWeapon_NotListView), "RefreshQuantity")]
        public static class Bug_Tracing_Fix
        {
            static bool Prefix(ListElementController_InventoryWeapon_NotListView __instance, InventoryItemElement_NotListView theWidget)
            {
                try
                {
                    if (__instance.quantity == -2147483648)
                    {
                        theWidget.qtyElement.SetActive(false);
                        return false;
                    }

                    theWidget.qtyElement.SetActive(true);
                    theWidget.quantityValue.SetText("{0}", __instance.quantity);
                    theWidget.quantityValueColor.SetUIColor(__instance.quantity > 0 || __instance.quantity == int.MinValue ? UIColor.White : UIColor.Red);
                    return false;
                }
                catch (Exception e)
                {
                    LogDebug("*****Exception thrown with ListElementController_InventoryWeapon_NotListView");
                    LogDebug($"theWidget null: {theWidget == null}");
                    LogDebug($"theWidget.qtyElement null: {theWidget.qtyElement == null}");
                    LogDebug($"theWidget.quantityValue null: {theWidget.quantityValue == null}");
                    LogDebug($"theWidget.quantityValueColor null: {theWidget.quantityValueColor == null}");
                    if (theWidget.itemName != null)
                    {
                        LogDebug("theWidget.itemName");
                        LogDebug(theWidget.itemName.ToString());
                    }

                    if (__instance.GetName() != null)
                    {
                        LogDebug("__instance.GetName");
                        LogDebug(__instance.GetName());
                    }

                    Error(e);
                    return false;
                }
            }
        }


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
