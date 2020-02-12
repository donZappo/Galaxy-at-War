using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BattleTech;
using BattleTech.UI;
using BattleTech.UI.Tooltips;
using Harmony;
using HBS;
using TMPro;
using UnityEngine;

using static Logger;
using BattleTech.UI.TMProWrapper;
using HBS.Extensions;
using UnityEngine.UI;

// ReSharper disable InconsistentNaming

public class StarmapMod
{
    internal static SGEventPanel eventPanel;
    internal static TMP_FontAsset font;
    internal static SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;

    //[HarmonyPatch(typeof(UnityGameInstance), "Awake")]
    //public static class UnityGameInstance_Awake_Patch
    //{
    //    public static void Postfix()
    //    {
    //        try
    //        {
    //            var prefab = AssetBundle.LoadFromFile(@"Mods\GalaxyAtWar\firacode");
    //            var asset = (GameObject) prefab.LoadAsset("fira");
    //            var tmp = asset.FindFirstChildNamed("regular").GetComponent<TextMeshPro>();
    //            var boldFont = asset.FindFirstChildNamed("bold").GetComponent<TextMeshPro>().font;
    //            font.fontWeights[7].regularTypeface = boldFont;
    //            font = tmp.font;
    //        }
    //        catch (Exception ex)
    //        {
    //            LogDebug(ex.ToString());
    //        }
    //    }
    //}

    [HarmonyPatch(typeof(TooltipPrefab_Planet), "SetData")]
    public static class TooltipPrefab_Planet_SetData_Patch
    {
        public static void Prefix(LocalizableText ___Description, object data, ref string __state)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                return;

            var starSystem = (StarSystem) data;
            if (starSystem == null)
            {
                return;
            }

            //var tmp = ___Description.GetComponent<TextMeshProUGUI>();
            //tmp.font = font;
            //tmp.fontSize = 10f;

            __state = starSystem.Def.Description.Details;
            var factionString = BuildInfluenceString(starSystem);
            Traverse.Create(starSystem.Def.Description).Property("Details").SetValue(factionString.ToString());
        }

        public static void Postfix(TooltipPrefab_Planet __instance, object data, string __state)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
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
            eventPanel = LazySingletonBehavior<UIManager>.Instance.CreatePopupModule<SGEventPanel>();
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

            //eventPanel.gameObject.GetComponentsInChildren<TextMeshProUGUI>(true).Do(LogDebug);
            var textMeshes = eventPanel.gameObject.GetComponentsInChildren<TextMeshProUGUI>(true);
            textMeshes.First(x => x.name =="title_week-day").text =
                UnityGameInstance.BattleTechGame.Simulation.CurrentDate.ToLongDateString();
            textMeshes.First(x => x.name == "descriptionText").alignment = TextAlignmentOptions.Center;
            var title = textMeshes.First(x => x.name == "event_titleText");
            title.text = "Relationship Summary";
            title.alignment = TextAlignmentOptions.Center;
            
            var event_OverallLayoutVlg = go.FindFirstChildNamed("event_OverallLayout").GetComponent<VerticalLayoutGroup>();
            event_OverallLayoutVlg.childControlHeight = true;
            event_OverallLayoutVlg.childForceExpandHeight = true;

            var event_OverallLayout = go.FindFirstChildNamed("event_OverallLayout").GetComponent<RectTransform>();
            event_OverallLayout.sizeDelta = new Vector2(750, 580);

            var results_TextllLayout = go.FindFirstChildNamed("results_TextllLayout").GetComponent<RectTransform>();
            results_TextllLayout.sizeDelta = new Vector2(750, 900);

            // jebus there is a space after "Viewport"
            var viewport = go.GetComponentsInChildren<RectTransform>().First(x => x.name == "Viewport ");
            viewport.sizeDelta = new Vector2(0, 900);
            eventPanel.gameObject.SetActive(false);
            LogDebug("RelationPanel created");
        }
        catch (Exception ex)
        {
            LogDebug(ex.ToString());
        }
    }

    internal static string RelationString 
    {
        get
        {
            var sb = new StringBuilder();
            foreach (var tracker in Core.WarStatus.deathListTracker.Where(x => !Core.Settings.DefensiveFactions.Contains(x.faction)))
            {
                var warFaction = Core.WarStatus.warFactionTracker.Find(x => x.faction == tracker.faction);
                sb.AppendLine($"<b><u>{Core.Settings.FactionNames[tracker.faction]}</b></u>\n");
                sb.AppendLine("Attack Resources: " + warFaction.AttackResources.ToString("0") +
                              " || Defense Resources: " + warFaction.DefensiveResources.ToString("0")
                              + " || Change in Systems: " + warFaction.TotalSystemsChanged + "\n");
                sb.AppendLine("Resources Lost To Piracy: " + (warFaction.PirateARLoss + warFaction.PirateDRLoss).ToString("0") + "\n\n");
                if (tracker.Enemies.Count > 0)
                    sb.AppendLine($"<u>Enemies</u>");
                foreach (var enemy in tracker.Enemies)
                    sb.AppendLine($"{Core.Settings.FactionNames[enemy],-20}");
                sb.AppendLine();

                if (tracker.Allies.Count > 0)
                    sb.AppendLine($"<u>Allies</u>");
                foreach (var ally in tracker.Allies)
                    sb.AppendLine($"{Core.Settings.FactionNames[ally],-20}");
                sb.AppendLine();
                sb.AppendLine();
            }

            sb.AppendLine();
            return sb.ToString();
        }
    }

    internal static void UpdateRelationString()
    {
        LogDebug("UpdateRelationString");
        eventPanel.gameObject.GetComponents<TextMeshProUGUI>()
            .First(x => x.name == "descriptionText")
            .text = "Some text";
    }

    [HarmonyPatch(typeof(SimGameState), "Update")]
    public static class FactionPopup_Patch
    {
        public static void Postfix(SimGameState __instance)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                return;

            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.R))
            {
                try
                {
                    if (eventPanel == null)
                    {
                        // panel is created and left inactive
                        SetupRelationPanel();
                    }
                    
                    eventPanel.gameObject.SetActive(!eventPanel.gameObject.activeSelf);
                    if (eventPanel.gameObject.activeSelf)
                    {
                        UpdateRelationString();
                    }

                    LogDebug("Event Panel " + eventPanel.gameObject.activeSelf);
                }
                catch(Exception ex)
                {
                    LogDebug(ex.ToString());
                }
            }
        }
    }

    private static string BuildInfluenceString(StarSystem starSystem)
    {
        var factionString = new StringBuilder();
        if (Core.WarStatus.AbandonedSystems.Contains(starSystem.Name))
            factionString.AppendLine("<b>" + starSystem.Name + "     ***Abandoned***</b>");
        else
            factionString.AppendLine("<b>" + starSystem.Name + "</b>");

        string SubString = "(";
        if (Core.WarStatus.HomeContendedStrings.Contains(starSystem.Name))
            SubString += "*Valuable Target*";
        if (Core.WarStatus.LostSystems.Contains(starSystem.Name))
            SubString += " *Owner Changed*";
        if (Core.WarStatus.PirateHighlight.Contains(starSystem.Name))
            SubString += " *ARRRRRGH!*";
        SubString += ")";

        if (SubString.Length > 2)
            SubString += "\n";
        else
            SubString = "";
        factionString.AppendLine(SubString);

        var tracker = Core.WarStatus.systems.Find(x => x.name == starSystem.Name);
        foreach (var influence in tracker.influenceTracker.OrderByDescending(x => x.Value))
        {
            string number;
            if (influence.Value <= float.Epsilon)
                continue;
            if (Math.Abs(influence.Value - 100) < 0.999)
                number = "100%";
            else if (influence.Value < 1)
                number = "< 1%";
            else if (influence.Value > 99)
                number = "> 99%";
            else
                number = $"{influence.Value:#.0}%";

            factionString.AppendLine($"{number,-15}{Core.Settings.FactionNames[influence.Key]}");
        }

        factionString.AppendLine($"\nPirate Activity: {tracker.PirateActivity:#0.0}%");
        factionString.AppendLine("\n\nAttack Resources: " + ((100 - tracker.PirateActivity) * tracker.AttackResources / 100).ToString("0.0") +
                                 "  Defense Resources: " + ((100 - tracker.PirateActivity) * tracker.DefenseResources / 100).ToString("0.0"));
        string BonusString = "Escalation Bonuses:";
        if (tracker.BonusCBills)
            BonusString = BonusString + "\n\t20% Bonus C-Bills per Mission";
        if (tracker.BonusXP)
            BonusString = BonusString + "\n\t20% Bonus XP per Mission";
        if (tracker.BonusSalvage)
            BonusString = BonusString + "\n\t+1 Priority Salvage per Mission";
        factionString.AppendLine("\n\n" + BonusString);
        return factionString.ToString();
    }

    [HarmonyPatch(typeof(StarmapScreen), "RenderStarmap")]
    public static class StarmapScreen_RenderStarmap_Patch
    {
        public static void Prefix()
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                return;

            //if (!Core.WarStatus.StartGameInitialized)
            //{
            //    var sim = UnityGameInstance.BattleTechGame.Simulation;
            //    Galaxy_at_War.HotSpots.ProcessHotSpots();
            //    var cmdCenter = UnityGameInstance.BattleTechGame.Simulation.RoomManager.CmdCenterRoom;
            //    sim.CurSystem.GenerateInitialContracts(() => Traverse.Create(cmdCenter).Method("OnContractsFetched"));
            //    Core.WarStatus.StartGameInitialized = true;
            //}
        }
    }

    [HarmonyPatch(typeof(StarmapRenderer), "GetSystemRenderer")]
    [HarmonyPatch(new[] {typeof(StarSystemNode)})]
    public static class StarmapRenderer_GetSystemRenderer_Patch
    {
        public static void Prefix()
        {
        }

        public static void Postfix(StarmapRenderer __instance, StarmapSystemRenderer __result)
        {
            try
            {
                if (Core.WarStatus == null || sim.IsCampaign && !sim.CompanyTags.Contains("story_complete"))
                    return;

                //Core.timer.Restart();
                if (Core.WarStatus != null)
                {
                    var wasVisited = sim.VisitedStarSystems.Contains(__result.name);

                    if (Core.WarStatus.HomeContendedStrings.Contains(__result.name))
                        HighlightSystem(__result, wasVisited, Color.magenta, true);
                    else if (Core.WarStatus.LostSystems.Contains(__result.name))
                        HighlightSystem(__result, wasVisited, Color.yellow, false);
                    else if (Core.WarStatus.PirateHighlight.Contains(__result.name))
                        HighlightSystem(__result, wasVisited, Color.red, false);
                    else if (__result.systemColor == Color.magenta || __result.systemColor == Color.yellow)
                        MakeSystemNormal(__result, wasVisited);
                }
                
                //LogDebug(Core.timer.ElapsedTicks);
            }
            catch (Exception ex)
            {
                LogDebug(ex.ToString());
            }
        }
    }

    [HarmonyPatch(typeof(StarmapScreen), "RefreshStarmap")]
    public static class StarmapScreen_RefreshStarmap__Patch
    {
        public static void Prefix(StarmapRenderer __instance)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                return;

            if (Core.WarStatus != null && !Core.WarStatus.StartGameInitialized)
            {
                Core.NeedsProcessing = true;
                var cmdCenter = UnityGameInstance.BattleTechGame.Simulation.RoomManager.CmdCenterRoom;
                sim.CurSystem.GenerateInitialContracts(() => Traverse.Create(cmdCenter).Method("OnContractsFetched"));
                Core.WarStatus.StartGameInitialized = true;
                Core.NeedsProcessing = false;
            }
        }
    }

    [HarmonyPatch(typeof(StarmapRenderer), "RefreshSystems")]
    public static class StarmapRenderer_RefreshSystems_Patch
    {
        public static void Postfix(StarmapRenderer __instance)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                return;

            if (!Core.Settings.ISMCompatibility)
                Galaxy_at_War.DynamicLogos.PlaceAndScaleLogos(Core.Settings.LogoNames, __instance);
        }
    }

    [HarmonyPatch(typeof(SGNavStarSystemCallout), "Init", typeof(StarmapRenderer), typeof(StarSystem))]
    public static class SGNavStarSystemCallout_Init_Patch
    {
        public static void Prefix(SGNavStarSystemCallout __instance, TextMeshProUGUI ___LabelField, TextMeshProUGUI ___NameField)
        {
            if (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete"))
                return;

            void SetFont(TextMeshProUGUI mesh, TMP_FontAsset font)
            {
                Core.timer.Restart();
                Traverse.Create(mesh).Field("m_fontAsset").SetValue(font);
                Traverse.Create(mesh).Field("m_baseFont").SetValue(font);
                Traverse.Create(mesh).Method("LoadFontAsset").GetValue();
                Traverse.Create(mesh).Field("m_havePropertiesChanged").SetValue(true);
                Traverse.Create(mesh).Field("m_isCalculateSizeRequired").SetValue(true);
                Traverse.Create(mesh).Field("m_isInputParsingRequired").SetValue(true);
                Traverse.Create(mesh).Method("SetVerticesDirty").GetValue();
                Traverse.Create(mesh).Method("SetLayoutDirty").GetValue();
                LogDebug("SetFont " + Core.timer.Elapsed);
            }

            if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
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