using System.Linq;
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
    public class DeploymentIndicator
    {
        private long counter;
        private readonly GameObject playPauseButton;
        private readonly GameObject hitBox;
        private readonly Image image;
        private readonly LocalizableText text;

        internal DeploymentIndicator()
        {
            // this makes it retry (if these are null during sim/combat transitions, I think)
            var root = UIManager.Instance.gameObject;
            if (root == null)
            {
                return;
            }

            var cmdCenterButton = root.gameObject.FindFirstChildNamed("uixPrfBttn_SIM_navButton-prime");
            if (cmdCenterButton == null)
            {
                return;
            }

            var cmdCenterBgFill = cmdCenterButton.FindFirstChildNamed("bgFill");
            var parent = root.gameObject.FindFirstChildNamed("uixPrfBttn_SIM_timeButton-Element-MANAGED");
            var timeLineText = root.gameObject.FindFirstChildNamed("time_labelText");
            playPauseButton = root.gameObject.FindFirstChildNamed("timeBttn_playPause");
            hitBox = parent.gameObject.FindFirstChildNamed("-HitboxOverlay-");
            image = cmdCenterBgFill.GetComponent<Image>();
            text = timeLineText.GetComponent<LocalizableText>();
            FileLog.Log("Deployment Indicator constructed.");
        }

        internal void ShowDeploymentIndicator()
        {
            if (Time.time > counter)
            {
                counter++;
                // avoiding expensive SetActive calls 
                if (playPauseButton == null)
                {
                    Mod.DeploymentIndicator = new DeploymentIndicator();
                }
                else if (playPauseButton.activeSelf &&
                         Mod.Globals.War.EscalationDays == 0)
                {
                    text.text = "Deployment Required";
                    image.color = new Color(0.5f, 0, 0, 0.863f);
                    playPauseButton.SetActive(false);
                    hitBox.SetActive(false);
                }
                else if (!playPauseButton.activeSelf &&
                         Mod.Globals.War.EscalationDays > 0)
                {
                    image.color = new Color(0, 0, 0, 0.863f);
                    playPauseButton.SetActive(true);
                    hitBox.SetActive(true);
                }
            }
        }
    }
}
