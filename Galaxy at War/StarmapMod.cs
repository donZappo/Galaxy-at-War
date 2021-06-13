using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using BattleTech;
using BattleTech.UI;
using BattleTech.UI.Tooltips;
using Harmony;
using HBS.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static GalaxyatWar.Logger;

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
            public static void Prefix(object data, ref string __state)
            {
                if (Globals.WarStatusTracker == null || Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete"))
                    return;

                if (data is StarSystem starSystem)
                {
                    __state = starSystem.Def.Description.Details;
                    if (Globals.Settings.ImmuneToWar.Contains(starSystem.OwnerValue.Name))
                    {
                        return;
                    }

                    var factionString = BuildInfluenceString(starSystem);
                    starSystem.Def.Description.Details = factionString;
                }
            }

            public static void Postfix(object data, string __state)
            {
                if (Globals.WarStatusTracker == null || Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete"))
                    return;

                if (data is StarSystem starSystem)
                {
                    starSystem.Def.Description.Details = __state;
                }
            }
        }

        private static void SetupRelationPanel()
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

                var results_TextllLayout = (RectTransform) go.FindFirstChildNamed("results_TextllLayout").transform;
                results_TextllLayout.sizeDelta = new Vector2(750, 900);

                // jebus there is a space after "Viewport"
                var viewport = go.GetComponentsInChildren<RectTransform>().First(x => x.name == "Viewport ");
                viewport.sizeDelta = new Vector2(0, 500);

                foreach (var tmpText in eventPanel.gameObject.GetComponentsInChildren<TMP_Text>(true))
                {
                    switch (tmpText.name)
                    {
                        case "title_week-day":
                            tmpText.text = Globals.Sim.CurrentDate.ToLongDateString();
                            break;
                        case "event_titleText":
                            tmpText.text = "Relationship Summary";
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
                Error(ex);
            }
        }

        private static string BuildRelationString()
        {
            StringBuilder sb = new();
            sb.AppendLine("<line-height=125%>");
            foreach (var tracker in Globals.WarStatusTracker.DeathListTracker.Where(x => !Globals.Settings.DefensiveFactions.Contains(x.Faction)))
            {
                if (!Globals.Settings.FactionNames.ContainsKey(tracker.Faction) || Globals.Settings.HyadesNeverControl.Contains(tracker.Faction)
                                                                                || Globals.WarStatusTracker.InactiveTHRFactions.Contains(tracker.Faction))
                {
                    LogDebug($"faction {tracker.Faction} doesn't exist in Mod.Globals.Settings.FactionNames, skipping...");
                    continue;
                }

                var warFaction = Globals.WarStatusTracker.WarFactionTracker.Find(x => x.FactionName == tracker.Faction);
                sb.AppendLine($"<b><u>{Globals.Settings.FactionNames[tracker.Faction]}</b></u>\n");
                if (tracker.Faction == Globals.WarStatusTracker.ComstarAlly)
                {
                    sb.AppendLine("<b>***" + Globals.Settings.GaW_Police + " Supported Faction***</b>");
                    sb.AppendLine("Attack Resources: " + (warFaction.AttackResources + Globals.Settings.GaW_Police_ARBonus).ToString("0") +
                                  " || Defense Resources: " + (warFaction.DefensiveResources + Globals.Settings.GaW_Police_DRBonus).ToString("0")
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
                    if (!Globals.Settings.FactionNames.ContainsKey(enemy) || Globals.Settings.HyadesNeverControl.Contains(enemy)
                                                                          || Globals.WarStatusTracker.InactiveTHRFactions.Contains(enemy))
                    {
                        LogDebug("Mod.Globals.Settings.FactionNames doesn't have " + enemy + " skipping...");
                        continue;
                    }

                    sb.AppendLine($"{Globals.Settings.FactionNames[enemy],-20}");
                }

                sb.AppendLine();

                if (tracker.Allies.Count > 0)
                    sb.AppendLine("<u>Allies</u>");
                foreach (var ally in tracker.Allies)
                {
                    if (!Globals.Settings.FactionNames.ContainsKey(ally) || Globals.Settings.HyadesNeverControl.Contains(ally)
                                                                         || Globals.WarStatusTracker.InactiveTHRFactions.Contains(ally))
                    {
                        LogDebug("Mod.Globals.Settings.FactionNames doesn't have " + ally + " skipping...");
                        continue;
                    }

                    sb.AppendLine($"{Globals.Settings.FactionNames[ally],-20}");
                }

                sb.AppendLine();
                sb.AppendLine();
            }

            sb.AppendLine("</line-height>");
            sb.AppendLine();
            LogDebug("BuildRelationString");

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
                if (Globals.WarStatusTracker == null || Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete"))
                {
                    return;
                }

                Globals.DeploymentIndicator?.ShowDeploymentIndicator();
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
                        Error(ex);
                    }
                }
            }
        }

        private static string BuildInfluenceString(StarSystem starSystem)
        {
            var factionString = new StringBuilder();
            if (Globals.WarStatusTracker.FlashpointSystems.Contains(starSystem.Name))
            {
                factionString.AppendLine("<b>" + starSystem.Name + "     ***System Immune to War***</b>");
                return factionString.ToString();
            }

            if (starSystem.OwnerValue.Name == Globals.WarStatusTracker.ComstarAlly)
                factionString.AppendLine("<b>" + starSystem.Name + "     ***" + Globals.Settings.GaW_Police + " Supported System***</b>");
            else if (Globals.WarStatusTracker.AbandonedSystems.Contains(starSystem.Name))
                factionString.AppendLine("<b>" + starSystem.Name + "     ***Abandoned***</b>");
            else
                factionString.AppendLine("<b>" + starSystem.Name + "</b>");

            var SubString = "(";
            if (Globals.WarStatusTracker.HomeContestedStrings.Contains(starSystem.Name))
                SubString += "*Valuable Target*";
            if (Globals.WarStatusTracker.LostSystems.Contains(starSystem.Name))
                SubString += " *Owner Changed*";
            if (Globals.WarStatusTracker.PirateHighlight.Contains(starSystem.Name))
                SubString += " *ARRRRRGH!*";
            SubString += ")";

            if (SubString.Length > 2)
                SubString += "\n";
            else
                SubString = "";
            factionString.AppendLine(SubString);

            var tracker = Globals.WarStatusTracker.Systems.FirstOrDefault(x => x.StarSystem == starSystem);
            if (tracker is null)
            {
                LogDebug($"{starSystem} is not in Globals.WarStatusTracker.systems");
                factionString.AppendLine("Error finding SystemStatus.");
                return factionString.ToString();
            }

            foreach (var influence in tracker.InfluenceTracker.OrderByDescending(x => x.Value))
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

                factionString.AppendLine($"{number,-15}{Globals.Settings.FactionNames[influence.Key]}");
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
            public static void Postfix(StarmapSystemRenderer __result)
            {
                try
                {
                    if (Globals.WarStatusTracker == null || Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete"))
                        return;

                    //Make sure that Flashpoint systems have priority display.
                    var flashpoints = Globals.Sim.AvailableFlashpoints;
                    var isFlashpoint = flashpoints.Any(x => x.CurSystem == __result.system.System);
                    if (ReferenceEquals(FactionEnumeration.GetFactionByName("NoFaction"), __result.system.System.OwnerValue)
                        || ReferenceEquals(FactionEnumeration.GetFactionByName("Locals"), __result.system.System.OwnerValue))
                    {
                        __result.Init(__result.system, Color.white, __result.CanTravel, Globals.Sim.VisitedStarSystems.Contains(__result.name));
                    }

                    if (!isFlashpoint)
                    {
                        var VisitedStarSystems = Globals.Sim.VisitedStarSystems;
                        var wasVisited = VisitedStarSystems.Contains(__result.name);

                        if (Globals.WarStatusTracker.HomeContestedStrings.Contains(__result.name))
                            HighlightSystem(__result, wasVisited, Color.magenta, true);
                        else if (Globals.WarStatusTracker.LostSystems.Contains(__result.name))
                            HighlightSystem(__result, wasVisited, Color.yellow, false);
                        else if (Globals.WarStatusTracker.PirateHighlight.Contains(__result.name))
                            HighlightSystem(__result, wasVisited, Color.red, false);
                        else if (__result.systemColor == Color.magenta || __result.systemColor == Color.yellow)
                            MakeSystemNormal(__result, wasVisited);
                    }
                }
                catch (Exception ex)
                {
                    Error(ex);
                }
            }
        }

        [HarmonyPatch(typeof(StarmapScreen), "RefreshStarmap")]
        public static class StarmapScreen_RefreshStarmap__Patch
        {
            public static void Prefix()
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (Globals.WarStatusTracker == null || sim.IsCampaign && !sim.CompanyTags.Contains("story_complete"))
                    return;

                if (Globals.WarStatusTracker != null && !Globals.WarStatusTracker.StartGameInitialized)
                {
                    LogDebug($"Refreshing contracts at RefreshStarmap. ({Globals.Sim.CurSystem.Name})");
                    var cmdCenter = UnityGameInstance.BattleTechGame.Simulation.RoomManager.CmdCenterRoom;
                    sim.CurSystem.GenerateInitialContracts(() => cmdCenter.OnContractsFetched());
                    Globals.WarStatusTracker.StartGameInitialized = true;
                }
            }
        }

        [HarmonyPatch(typeof(StarmapRenderer), "RefreshSystems")]
        public static class StarmapRendererRefreshSystemsPatch
        {
            public static void Postfix(StarmapRenderer __instance)
            {
                if (Globals.WarStatusTracker == null || Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete"))
                    return;

                if (!Globals.Settings.ExpandedMap)
                    DynamicLogos.PlaceAndScaleLogos(Globals.Settings.LogoNames, __instance);
            }
        }

        [HarmonyPatch(typeof(SGNavStarSystemCallout), "Init", typeof(StarmapRenderer), typeof(StarSystem))]
        public static class SGNavStarSystemCalloutInitPatch
        {
            public static void Prefix(SGNavStarSystemCallout __instance, TextMeshProUGUI ___LabelField, TextMeshProUGUI ___NameField)
            {
                if (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete"))
                    return;

                void SetFont(TextMeshProUGUI mesh, TMP_FontAsset font)
                {
                    mesh.m_fontAsset = font;
                    mesh.LoadFontAsset();
                    mesh.m_havePropertiesChanged = true;
                    mesh.m_isCalculateSizeRequired = true;
                    mesh.m_isInputParsingRequired = true;
                    mesh.SetVerticesDirty();
                    mesh.SetLayoutDirty();
                }

                if (Globals.WarStatusTracker == null || Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete"))
                    return;

                SetFont(___LabelField, Globals.Font);
                SetFont(___NameField, Globals.Font);
            }
        }

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
                __result.selectedScale = 10;
                __result.deselectedScale = 8;
            }
            else
            {
                __result.selectedScale = 4;
                __result.deselectedScale = 4;
            }
        }

        private static void MakeSystemNormal(StarmapSystemRenderer __result, bool wasVisited)
        {
            __result.Init(__result.system, __result.systemColor, __result.CanTravel, wasVisited);
            __result.transform.localScale = Vector3.one;
            __result.selectedScale = 6;
            __result.deselectedScale = 4;
            __result.starOuter.gameObject.SetActive(wasVisited);
        }
    }
}
