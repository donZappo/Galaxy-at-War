using BattleTech;
using BattleTech.UI;
using BattleTech.UI.TMProWrapper;
using Harmony;
using HBS.Extensions;
using UnityEngine;
using UnityEngine.UI;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedType.Global 
// ReSharper disable StringLiteralTypo 
// ReSharper disable ClassNeverInstantiated.Global

namespace GalaxyatWar
{
    public class DeploymentIndicator : MonoBehaviour
    {
        private GameObject playPauseButton;
        private GameObject hitBox;
        private Image image;
        private LocalizableText text;

        internal DeploymentIndicator()
        {
        }

        private void OnEnable()
        {
            var root = UIManager.Instance.gameObject;
            var cmdCenterButton = root.gameObject.FindFirstChildNamed("uixPrfBttn_SIM_navButton-prime");
            var cmdCenterBgFill = cmdCenterButton.FindFirstChildNamed("bgFill");
            var parent = root.gameObject.FindFirstChildNamed("uixPrfBttn_SIM_timeButton-Element-MANAGED");
            var timeLineText = root.gameObject.FindFirstChildNamed("time_labelText");
            playPauseButton = root.gameObject.FindFirstChildNamed("timeBttn_playPause");
            hitBox = parent.gameObject.FindFirstChildNamed("-HitboxOverlay-");
            image = cmdCenterBgFill.GetComponent<Image>();
            text = timeLineText.GetComponent<LocalizableText>();
            Logger.LogDebug("Deployment Indicator constructed.");
            InvokeRepeating(nameof(RefreshIndicator), 0, 3);
        }

        private void RefreshIndicator()
        {
            Logger.LogDebug($"EscalationDays " + Globals.WarStatusTracker.EscalationDays);
            Logger.LogDebug($"Deployment " + Globals.WarStatusTracker.Deployment);
            Logger.LogDebug($"EscalationOrder.GetRemainingCost() " + Globals.WarStatusTracker.EscalationOrder.Description + " " + Globals.WarStatusTracker.EscalationOrder.GetRemainingCost());
           
            Globals.WarStatusTracker.HotBox.Do(h => Logger.LogDebug(h.Name));
            if (Globals.Sim.TravelState is SimGameTravelStatus.IN_SYSTEM
                && Globals.WarStatusTracker.HotBox.Contains(Globals.Sim.CurSystem.FindSystemStatus())
                && Globals.Sim.starDict[Globals.Sim.ActiveTravelContract.TargetSystemID] == Globals.Sim.CurSystem
                || Globals.WarStatusTracker.EscalationOrder.GetRemainingCost() == 0)
            {
                text.text = "Deployment Required";
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
    }

    internal static class Extensions
    {
        // surely SetActive checks this shit itself!?
        static void SetActiveIfNeeded(this GameObject gameObject, bool enabled)
        {
            switch (enabled)
            {
                case true:
                    if (!gameObject.activeInHierarchy)
                    {
                        gameObject.SetActive(true);
                    }

                    break;
                case false:
                    if (gameObject.activeInHierarchy)
                    {
                        gameObject.SetActive(false);
                    }

                    break;
            }
        }
    }
}
