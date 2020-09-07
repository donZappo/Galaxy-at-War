using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BattleTech;
using BattleTech.UI;
using BattleTech.UI.TMProWrapper;
using BattleTech.UI.Tooltips;
using Harmony;
using HBS.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static GalaxyatWar.Logger;
using static GalaxyatWar.Globals;
// ReSharper disable StringLiteralTypo
// ReSharper disable UnusedMember.Global
// ReSharper disable ClassNeverInstantiated.Global    
// ReSharper disable UnusedType.Global
// ReSharper disable InconsistentNaming

namespace GalaxyatWar
{
    public class StarmapMod
    {
        internal static SGEventPanel eventPanel;
        private static TMP_Text descriptionText; 

        [HarmonyPatch(typeof(TooltipPrefab_Planet), "SetData")]
        public static class TooltipPrefab_PlanetSetDataPatch
        {
            public static void Prefix(LocalizableText ___Description, object data, ref string __state)
            {
                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                    return;

                var starSystem = (StarSystem) data;
                if (starSystem == null)
                {
                    return;
                }

                __state = starSystem.Def.Description.Details;
                var factionString = BuildInfluenceString(starSystem);
                Traverse.Create(starSystem.Def.Description).Property("Details").SetValue(factionString);
            }

            public static void Postfix(object data, string __state)
            {
                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                    return;

                var starSystem = (StarSystem) data;
                if (starSystem == null)
                {
                    return;
                }

                Traverse.Create(starSystem.Def.Description).Property("Details").SetValue(__state);
            }
        }

        internal static void SetupRelationPanel()
        {
            try
            {
                LogDebug("SetupRelationPanel");
                var dm = UIManager.Instance.dataManager;
                var prefabName = UIManager.Instance.GetPrefabName<SGEventPanel>("");
                var uiModule = (UIModule) dm.PooledInstantiate(
                        prefabName, BattleTechResourceType.UIModulePrefabs, null, null)
                    .GetComponent(typeof(SGEventPanel));
                uiModule.SetPrefabName("GaW RelationPanel");
                UIManager.Instance.AddModule(uiModule, UIManager.Instance.popupNode, -1, false);
                eventPanel = (SGEventPanel) uiModule;
                eventPanel.gameObject.SetActive(true);

                var go = eventPanel.gameObject.FindFirstChildNamed("Representation");
                go.FindFirstChildNamed("event_ResponseOptions").SetActive(false);
                go.FindFirstChildNamed("label_chevron").SetActive(false);
                go.FindFirstChildNamed("uixPrfPanl_spotIllustration_750-MANAGED").SetActive(false);
                go.FindFirstChildNamed("event_TopBar").SetActive(false);
                go.FindFirstChildNamed("T_brackets_cap").SetActive(false);
                go.FindFirstChildNamed("event_ResponseOptions").SetActive(false);
                go.FindFirstChildNamed("results_buttonContainer").SetActive(false);
                go.FindFirstChildNamed("choiceCrumb").SetActive(false);
                go.FindFirstChildNamed("resultTagsContent").SetActive(false);
                go.FindFirstChildNamed("B_brackets_results").SetActive(false);
                go.FindFirstChildNamed("label_Text").SetActive(false);

                var event_OverallLayoutVlg = go.FindFirstChildNamed("event_OverallLayout").GetComponent<VerticalLayoutGroup>();
                event_OverallLayoutVlg.childControlHeight = true;
                event_OverallLayoutVlg.childForceExpandHeight = true;

                var event_OverallLayout = (RectTransform) go.FindFirstChildNamed("event_OverallLayout").transform;
                event_OverallLayout.sizeDelta = new Vector2(750, 580);

                var results_TextllLayout =(RectTransform) go.FindFirstChildNamed("results_TextllLayout").transform;
                results_TextllLayout.sizeDelta = new Vector2(750, 900);

                // jebus there is a space after "Viewport"
                var viewport = go.GetComponentsInChildren<RectTransform>().First(x => x.name == "Viewport ");
                viewport.sizeDelta = new Vector2(0, 500);
            
                foreach (var tmpText in eventPanel.gameObject.GetComponentsInChildren<TMP_Text>(true))
                {
                    switch (tmpText.name)
                    {
                        case "title_week-day": tmpText.text = UnityGameInstance.BattleTechGame.Simulation.CurrentDate.ToLongDateString();
                            break;
                        case "event_titleText": tmpText.text = "Relationship Summary";
                            tmpText.alignment = TextAlignmentOptions.Center;
                            break;
                        case "descriptionText":
                            descriptionText = tmpText;
                            tmpText.text = BuildRelationString();
                            tmpText.alignment = TextAlignmentOptions.Center;
                            break;
                    }
                }
            
                eventPanel.gameObject.SetActive(false);
                LogDebug("RelationPanel created");
            }
            catch (Exception ex)
            {
                LogDebug(ex);
            }
        }

        private static string BuildRelationString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<line-height=125%>");
            foreach (var tracker in WarStatusTracker.DeathListTrackers.Where(x => !Settings.DefensiveFactions.Contains(x.faction)))
            {
                if (!Settings.FactionNames.ContainsKey(tracker.faction) || Settings.HyadesNeverControl.Contains(tracker.faction) 
                                                                            || WarStatusTracker.InactiveTHRFactions.Contains(tracker.faction))
                {
                    LogDebug($"faction {tracker.faction} doesn't exist in Mod.Settings.FactionNames, skipping...");
                    continue;
                }
                var warFaction = WarStatusTracker.warFactionTracker.Find(x => x.faction == tracker.faction);
                sb.AppendLine($"<b><u>{Settings.FactionNames[tracker.faction]}</b></u>\n");
                if (tracker.faction == WarStatusTracker.ComstarAlly)
                {
                    sb.AppendLine("<b>***" + Settings.GaW_Police + " Supported Faction***</b>");
                    sb.AppendLine("Attack Resources: " + (warFaction.AttackResources + Settings.GaW_Police_ARBonus).ToString("0") +
                                  " || Defense Resources: " + (warFaction.DefensiveResources + Settings.GaW_Police_DRBonus).ToString("0")
                                  + " || Change in Systems: " + warFaction.TotalSystemsChanged + "\n");
                }
                else
                {
                    sb.AppendLine("Attack Resources: " + warFaction.AttackResources.ToString("0") +
                                  " || Defense Resources: " + warFaction.DefensiveResources.ToString("0")
                                  + " || Change in Systems: " + warFaction.TotalSystemsChanged + "\n");
                }
                sb.AppendLine("Resources Lost To Piracy: " + (warFaction.PirateARLoss + warFaction.PirateDRLoss).ToString("0") + "\n\n");
                if (tracker.Enemies.Count > 0)
                    sb.AppendLine("<u>Enemies</u>");
                foreach (var enemy in tracker.Enemies)
                {
                    if (!Settings.FactionNames.ContainsKey(enemy) || Settings.HyadesNeverControl.Contains(enemy)
                                                                      || WarStatusTracker.InactiveTHRFactions.Contains(enemy))
                    {
                        LogDebug("Mod.Settings.FactionNames doesn't have " + enemy + " skipping...");
                        continue;
                    }
                    sb.AppendLine($"{Settings.FactionNames[enemy],-20}");
                }

                sb.AppendLine();

                if (tracker.Allies.Count > 0)
                    sb.AppendLine("<u>Allies</u>");
                foreach (var ally in tracker.Allies)
                {
                    if (!Settings.FactionNames.ContainsKey(ally) || Settings.HyadesNeverControl.Contains(ally)
                                                                     || WarStatusTracker.InactiveTHRFactions.Contains(ally))
                    {
                        LogDebug("Mod.Settings.FactionNames doesn't have " + ally + " skipping...");
                        continue;
                    }
                    sb.AppendLine($"{Settings.FactionNames[ally],-20}");
                }
                sb.AppendLine();
                sb.AppendLine();
            }

            sb.AppendLine("</line-height>");
            sb.AppendLine();
            LogDebug("RelationString");
        
            // bug? in TMPro shits the bed on a long string with underlines
            if (AppDomain.CurrentDomain.GetAssemblies().Any(x => x.FullName.Contains("InnerSphereMap")))
            {
                sb.Replace("<u>", "");
                sb.Replace("</u>", "");
            }
            return sb.ToString();
        }
       
        internal static void UpdatePanelText() => descriptionText.text = BuildRelationString();

        [HarmonyPatch(typeof(SimGameState), "Update")]
        public static class SimGameStateUpdatePatch
        {
            public static void Postfix(SimGameState __instance)
            {
                if (WarStatusTracker == null || Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete"))
                {
                    return;
                }

                if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.R))
                {
                    try
                    {
                        if (eventPanel == null)
                        {
                            SetupRelationPanel();
                        }

                        eventPanel.gameObject.SetActive(!eventPanel.gameObject.activeSelf);
                        if (eventPanel.gameObject.activeSelf)
                        {
                            UpdatePanelText();
                        }
    
                        LogDebug("Event Panel " + eventPanel.gameObject.activeSelf);
                    }
                    catch (Exception ex)
                    {
                        LogDebug(ex);
                    }
                }
            }
        }

        private static string BuildInfluenceString(StarSystem starSystem)
        {
            var factionString = new StringBuilder();
            if (WarStatusTracker.FlashpointSystems.Contains(starSystem.Name) || Settings.ImmuneToWar.Contains(starSystem.OwnerValue.Name))
            {
                factionString.AppendLine("<b>" + starSystem.Name + "     ***System Immune to War***</b>");
                return factionString.ToString();
            }

            if (starSystem.OwnerValue.Name == WarStatusTracker.ComstarAlly)
                factionString.AppendLine("<b>" + starSystem.Name + "     ***" + Settings.GaW_Police + " Supported System***</b>");
            else if (WarStatusTracker.AbandonedSystems.Contains(starSystem.Name))
                factionString.AppendLine("<b>" + starSystem.Name + "     ***Abandoned***</b>");
            else
                factionString.AppendLine("<b>" + starSystem.Name + "</b>");

            var SubString = "(";
            if (WarStatusTracker.HomeContendedStrings.Contains(starSystem.Name))
                SubString += "*Valuable Target*";
            if (WarStatusTracker.LostSystems.Contains(starSystem.Name))
                SubString += " *Owner Changed*";
            if (WarStatusTracker.PirateHighlight.Contains(starSystem.Name))
                SubString += " *ARRRRRGH!*";
            SubString += ")";

            if (SubString.Length > 2)
                SubString += "\n";
            else
                SubString = "";
            factionString.AppendLine(SubString);

            var tracker = WarStatusTracker.SystemStatuses.Find(x => x.starSystem == starSystem);
            foreach (var influence in tracker.influenceTracker.OrderByDescending(x => x.Value))
            {
                string number;
                if (influence.Value < 1)
                    continue;
                if (Math.Abs(influence.Value - 100) < 0.999)
                    number = "100%";
                //else if (influence.Value < 1)
                //    number = "< 1%";
                else if (influence.Value > 99)
                    number = "> 99%";
                else
                    number = $"{influence.Value:#.0}%";

                factionString.AppendLine($"{number,-15}{Settings.FactionNames[influence.Key]}");
            }

            factionString.AppendLine($"\nPirate Activity: {tracker.PirateActivity:#0.0}%");
            factionString.AppendLine("\n\nAttack Resources: " + ((100 - tracker.PirateActivity) * tracker.AttackResources / 100).ToString("0.0") +
                                     "  Defense Resources: " + ((100 - tracker.PirateActivity) * tracker.DefenseResources / 100).ToString("0.0"));
            var BonusString = "Escalation Bonuses:";
            if (tracker.BonusCBills)
                BonusString = BonusString + "\n\t20% Bonus C-Bills per Mission";
            if (tracker.BonusXP)
                BonusString = BonusString + "\n\t20% Bonus XP per Mission";
            if (tracker.BonusSalvage)
                BonusString = BonusString + "\n\t+1 Priority Salvage per Mission";
            factionString.AppendLine("\n\n" + BonusString);
            return factionString.ToString();
        }

        [HarmonyPatch(typeof(StarmapRenderer), "GetSystemRenderer")]
        [HarmonyPatch(new[] {typeof(StarSystemNode)})]
        public static class StarmapRendererGetSystemRendererPatch
        {
            public static void Prefix()
            {
            }

            public static void Postfix(StarmapSystemRenderer __result)
            {
                try
                {
                    if (WarStatusTracker == null || Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete"))
                        return;

                    //Make sure that Flashpoint systems have priority display.
                    var flashpoints = Sim.AvailableFlashpoints;
                    var isFlashpoint = flashpoints.Any(x => x.CurSystem.Name == __result.name);

                    //Mod.timer.Restart();
                    if (WarStatusTracker != null && !isFlashpoint)
                    {
                        var VisitedStarSystems = (List<string>)Traverse.Create(Sim).Field("VisitedStarSystems").GetValue();
                        var wasVisited = VisitedStarSystems.Contains(__result.name);

                        if (WarStatusTracker.HomeContendedStrings.Contains(__result.name))
                            HighlightSystem(__result, wasVisited, Color.magenta, true);
                        else if (WarStatusTracker.LostSystems.Contains(__result.name))
                            HighlightSystem(__result, wasVisited, Color.yellow, false);
                        else if (WarStatusTracker.PirateHighlight.Contains(__result.name))
                            HighlightSystem(__result, wasVisited, Color.red, false);
                        else if (__result.systemColor == Color.magenta || __result.systemColor == Color.yellow)
                            MakeSystemNormal(__result, wasVisited);
                    }

                    //LogDebug(Mod.timer.ElapsedTicks);
                }
                catch (Exception ex)
                {
                    LogDebug(ex.ToString());
                }
            }
        }

        [HarmonyPatch(typeof(StarmapRenderer), "RefreshSystems")]
        public static class StarmapRendererRefreshSystemsPatch
        {
            public static void Postfix(StarmapRenderer __instance)
            {
                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                    return;

                if (!Settings.ExpandedMap)
                    DynamicLogos.PlaceAndScaleLogos(Settings.LogoNames, __instance);
            }
        }

        [HarmonyPatch(typeof(SGNavStarSystemCallout), "Init", typeof(StarmapRenderer), typeof(StarSystem))]
        public static class SGNavStarSystemCalloutInitPatch
        {
            public static void Prefix(SGNavStarSystemCallout __instance, TextMeshProUGUI ___LabelField, TextMeshProUGUI ___NameField)
            {
                if (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete"))
                    return;

                void SetFont(TextMeshProUGUI mesh, TMP_FontAsset font)
                {
                    T.Restart();
                    Traverse.Create(mesh).Field("m_fontAsset").SetValue(font);
                    Traverse.Create(mesh).Field("m_baseFont").SetValue(font);
                    Traverse.Create(mesh).Method("LoadFontAsset").GetValue();
                    Traverse.Create(mesh).Field("m_havePropertiesChanged").SetValue(true);
                    Traverse.Create(mesh).Field("m_isCalculateSizeRequired").SetValue(true);
                    Traverse.Create(mesh).Field("m_isInputParsingRequired").SetValue(true);
                    Traverse.Create(mesh).Method("SetVerticesDirty").GetValue();
                    Traverse.Create(mesh).Method("SetLayoutDirty").GetValue();
                    LogDebug("SetFont " + T.Elapsed);
                }

                if (WarStatusTracker == null || (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete")))
                    return;
                // set font in the most roundabout way ever
                var fonts = Resources.FindObjectsOfTypeAll(typeof(TMP_FontAsset));
                foreach (var o in fonts)
                {
                    var font = (TMP_FontAsset) o;
                    if (font.name == "UnitedSansSemiExt-Light")
                    {
                        SetFont(___LabelField, font);
                        SetFont(___NameField, font);
                    }
                }
            }
        }

        private static readonly AccessTools.FieldRef<StarmapSystemRenderer, float> selectedScale =
            AccessTools.FieldRefAccess<StarmapSystemRenderer, float>("selectedScale");

        private static readonly AccessTools.FieldRef<StarmapSystemRenderer, float> deselectedScale =
            AccessTools.FieldRefAccess<StarmapSystemRenderer, float>("deselectedScale");

        private static void HighlightSystem(StarmapSystemRenderer __result, bool wasVisited, Color color, bool resize)
        {
            var blackMarketIsActive = __result.blackMarketObj.gameObject.activeInHierarchy;
            var fpAvailableIsActive = __result.flashpointAvailableObj.gameObject.activeInHierarchy;
            var fpActiveIsActive = __result.flashpointActiveObj.gameObject.activeInHierarchy;
            __result.Init(__result.system, color, __result.CanTravel, wasVisited);
            if (fpAvailableIsActive)
                __result.flashpointAvailableObj.SetActive(true);
            if (fpActiveIsActive)
                __result.flashpointActiveObj.SetActive(true);
            if (blackMarketIsActive)
                __result.blackMarketObj.gameObject.SetActive(true);
            if (resize)
            {
                selectedScale(__result) = 10;
                deselectedScale(__result) = 8;
            }
            else
            {
                selectedScale(__result) = 4;
                deselectedScale(__result) = 4;
            }
        }

        private static void MakeSystemNormal(StarmapSystemRenderer __result, bool wasVisited)
        {
            __result.Init(__result.system, __result.systemColor, __result.CanTravel, wasVisited);
            __result.transform.localScale = Vector3.one;
            selectedScale(__result) = 6;
            deselectedScale(__result) = 4;
            __result.starOuter.gameObject.SetActive(wasVisited);
        }
    }
}
