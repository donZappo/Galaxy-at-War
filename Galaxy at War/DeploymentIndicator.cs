// ReSharper disable InconsistentNaming
// ReSharper disable UnusedType.Global 
// ReSharper disable StringLiteralTypo 
// ReSharper disable ClassNeverInstantiated.Global

using System;
using System.Linq;
using BattleTech;
using BattleTech.UI;
using BattleTech.UI.TMProWrapper;
using HBS.Extensions;
using UnityEngine;
using UnityEngine.UI;
using static GalaxyatWar.Logger;

namespace GalaxyatWar
{
    public class DeploymentIndicator : MonoBehaviour
    {
        private static GameObject playPauseButton;
        private static GameObject hitBox;
        private static Image image;
        private static LocalizableText text;

        internal void Awake()
        {
            InitGameObject();
            InvokeRepeating(nameof(RefreshIndicator), 0, 1);
            LogDebug("Deployment Indicator constructed.");
        }

        private static void InitGameObject()
        {
            LogDebug("InitGameObject");
            var root = UIManager.Instance.gameObject;
            var cmdCenterButton = root.gameObject.FindFirstChildNamed("uixPrfBttn_SIM_navButton-prime");
            var cmdCenterBgFill = cmdCenterButton.FindFirstChildNamed("bgFill");
            var parent = root.gameObject.FindFirstChildNamed("uixPrfBttn_SIM_timeButton-Element-MANAGED");
            var timeLineText = root.gameObject.FindFirstChildNamed("time_labelText");
            playPauseButton = root.gameObject.FindFirstChildNamed("timeBttn_playPause");
            hitBox = parent.gameObject.FindFirstChildNamed("-HitboxOverlay-");
            image = cmdCenterBgFill.GetComponent<Image>();
            text = timeLineText.GetComponent<LocalizableText>();
        }

        internal void RefreshIndicator()
        {
            try
            {
                if (playPauseButton is null)
                {
                    InitGameObject();
                }

                if (Globals.Sim.CurRoomState != DropshipLocation.SHIP
                    || UIManager.Instance.PopupRoot.gameObject.FindFirstChildNamed("uixPrfScrn_quarterlyReport-screenV2(Clone)").GetComponent<SGCaptainsQuartersStatusScreen>().Visible)
                {
                    return;
                }


                //LogDebug(Globals.Sim.TravelState);
                //LogDebug("HotBox.IsHot " + Globals.WarStatusTracker.HotBox.IsHot(Globals.Sim.CurSystem.Name));
                //LogDebug(1);
                //LogDebug("Globals.Sim.SelectedContract is not null " + (Globals.Sim.SelectedContract is not null));
                //LogDebug(2);
                //LogDebug("SelectedContract.TargetSystemID " + (Globals.Sim.starDict[Globals.Sim.CurSystem.Contract] == Globals.Sim.CurSystem));
                //LogDebug(3);
                //LogDebug("Globals.Sim.starDict[Globals.Sim.SelectedContract.TargetSystemID] == Globals.Sim.CurSystem " + (Globals.Sim.starDict[Globals.Sim.SelectedContract?.TargetSystemID] == Globals.Sim.CurSystem));
                //LogDebug(4);
                //LogDebug("EscalationDays " + Globals.WarStatusTracker.EscalationDays);

                if (Globals.Sim.TravelState is SimGameTravelStatus.IN_SYSTEM
                    && Globals.WarStatusTracker.HotBox.IsHot(Globals.Sim.CurSystem.Name)
                    && Globals.WarStatusTracker.EscalationDays == 0)
                {
                    text.SetText("Deployment Required");
                    image.color = new Color(0.5f, 0, 0, 0.863f);
                    playPauseButton.SetActive(false);
                    hitBox.SetActive(false);
                }
                else
                {
                    image.color = new Color(0, 0, 0, 0.863f);
                    playPauseButton.SetActive(true);
                    hitBox.SetActive(true);
                }
            }
            catch (Exception ex)
            {
                Error(ex);
            }
        }
    }
}
