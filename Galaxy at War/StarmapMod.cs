using System;
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
using UnityEngine.UI;
using static Logger;
using BattleTech.UI.TMProWrapper;

// ReSharper disable InconsistentNaming

public class StarmapMod
{
    //internal static GameObject textPanel;
    //internal static TextMeshProUGUI panelText;
    internal static SGEventPanel eventPanel;

    [HarmonyPatch(typeof(TooltipPrefab_Planet), "SetData")]
    public static class TooltipPrefab_Planet_SetData_Patch
    {
        public static void Prefix(TooltipPrefab_Planet __instance, object data, ref string __state)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                return;

            var starSystem = (StarSystem)data;
            if (starSystem == null)
            {
                return;
            }

            __state = starSystem.Def.Description.Details;
            var factionString = BuildInfluenceString(starSystem);
            Traverse.Create(starSystem.Def.Description).Property("Details").SetValue(factionString.ToString());
        }

        public static void Postfix(TooltipPrefab_Planet __instance, object data, string __state)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                return;

            var starSystem = (StarSystem)data;
            if (starSystem == null)
            {
                return;
            }

            Traverse.Create(starSystem.Def.Description).Property("Details").SetValue(__state);
        }
    }

    internal static void SetupRelationPanel()
    {
        eventPanel = LazySingletonBehavior<UIManager>.Instance.CreatePopupModule<SGEventPanel>("");
        eventPanel.gameObject.SetActive(true);
        GameObject.Find("uixPrfPanl_spotIllustration_750-MANAGED").SetActive(false);

        UpdatePanelText();

        try
        {
            GameObject.Find("event_ResponseOptions").SetActive(false);
            GameObject.Find("label_chevron").SetActive(false);
        }
        catch
        {
        }

        var expander = GameObject.Find("ExpanderContainer");
        var rect = expander.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.1f);
        rect.anchorMax = new Vector2(0.5f, 0.9f);
        var viewport = GameObject.Find("ExpandingContentGoesHere");
        var vpRect = viewport.GetComponent<RectTransform>();
        vpRect.anchorMin = new Vector2(0.5f, 0.1f);
        vpRect.anchorMax = new Vector2(0.5f, 0.9f);
        var event_OverallLayout = GameObject.Find("event_OverallLayout");
        var vertLayout = event_OverallLayout.GetComponent<VerticalLayoutGroup>() as HorizontalOrVerticalLayoutGroup;
        vertLayout.childForceExpandHeight = true;
        vertLayout.childControlHeight = true;
        var vertRect = vertLayout.GetComponent<RectTransform>();
        vertRect.anchorMin = new Vector2(0.5f, 0.1f);
        vertRect.anchorMax = new Vector2(0.5f, 0.9f);
        vertLayout.SetLayoutVertical();
        eventPanel.gameObject.SetActive(false);
        LogDebug("RelationPanel created");
    }

    private static string BuildRelationString()
    {
        var sb = new StringBuilder();
        foreach (var tracker in Core.WarStatus.deathListTracker.Where(x => !Core.Settings.DefensiveFactions.Contains(x.faction)))
        {
            var warFaction = Core.WarStatus.warFactionTracker.Find(x => x.faction == tracker.faction);
            sb.AppendLine($"<b><u>{Core.Settings.FactionNames[tracker.faction]}</b></u>\n");
            sb.AppendLine("Attack Resources: " + warFaction.AttackResources.ToString("0") + 
                " || Defense Resources: " + warFaction.DefensiveResources.ToString("0") 
                + " || Change in Systems: " + warFaction.TotalSystemsChanged + "\n");
            sb.AppendLine("Resources Lost To Piracy: " + (warFaction.PirateARLoss + warFaction.PirateDRLoss).ToString("0") +"\n\n");
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

    internal static void UpdatePanelText()
    {
        var tmps = eventPanel.gameObject.GetComponentsInChildren<TextMeshProUGUI>();
        foreach (var tm in tmps)
        {
            switch (tm.name)
            {
                case "title_week-day":
                    tm.text = UnityGameInstance.BattleTechGame.Simulation.CurrentDate.ToLongDateString();
                    break;
                case "event_titleText":
                    tm.text = "Relationship Summary";
                    tm.alignment = TextAlignmentOptions.Center;
                    break;
                case "descriptionText":
                    tm.text = BuildRelationString();
                    tm.alignment = TextAlignmentOptions.Center;
                    break;
                case "label_Text":
                    tm.gameObject.SetActive(false);
                    break;
            }
        }
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
                    eventPanel.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(eventPanel);
                    
                }
                catch
                {
                    SetupRelationPanel();
                    eventPanel.gameObject.SetActive(true);
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
    [HarmonyPatch(new[] { typeof(StarSystemNode) })]
    public static class StarmapRenderer_GetSystemRenderer_Patch
    {
        public static void Prefix()
        {
        }

        public static void Postfix(StarmapRenderer __instance, ref StarmapSystemRenderer __result)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                return;

            if (Core.WarStatus != null)
            {
                var visitedStarSystems = Traverse.Create(sim).Field("VisitedStarSystems").GetValue<List<string>>();
                var wasVisited = visitedStarSystems.Contains(__result.name);
                if (Core.WarStatus.HomeContendedStrings.Contains(__result.name))
                    HighlightSystem(__result, wasVisited, Color.magenta, true);
                else if (Core.WarStatus.LostSystems.Contains(__result.name))
                    HighlightSystem(__result, wasVisited, Color.yellow, false);
                else if (Core.WarStatus.PirateHighlight.Contains(__result.name))
                    HighlightSystem(__result, wasVisited, Color.red, false);
                else if (__result.systemColor == Color.magenta || __result.systemColor == Color.yellow)
                    MakeSystemNormal(__result, wasVisited);
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
            void SetFont(TextMeshProUGUI mesh, TMP_FontAsset font)
            {
                Traverse.Create(mesh).Field("m_fontAsset").SetValue(font);
                Traverse.Create(mesh).Field("m_baseFont").SetValue(font);
                Traverse.Create(mesh).Method("LoadFontAsset").GetValue();
                Traverse.Create(mesh).Field("m_havePropertiesChanged").SetValue(true);
                Traverse.Create(mesh).Field("m_isCalculateSizeRequired").SetValue(true);
                Traverse.Create(mesh).Field("m_isInputParsingRequired").SetValue(true);
                Traverse.Create(mesh).Method("SetVerticesDirty").GetValue();
                Traverse.Create(mesh).Method("SetLayoutDirty").GetValue();
            }

            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (Core.WarStatus == null || (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete")))
                return;
            // set font in the most roundabout way ever
            var fonts = Resources.FindObjectsOfTypeAll(typeof(TMP_FontAsset));
            foreach (var o in fonts)
            {
                var font = (TMP_FontAsset)o;
                if (font.name == "UnitedSansSemiExt-Light")
                {
                    SetFont(___LabelField, font);
                    SetFont(___NameField, font);
                }
            }
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
            Traverse.Create(__result).Field("selectedScale").SetValue(10f);
            Traverse.Create(__result).Field("deselectedScale").SetValue(8f);
        }
        else
        {
            Traverse.Create(__result).Field("selectedScale").SetValue(4f);
            Traverse.Create(__result).Field("deselectedScale").SetValue(4f);
        }
        
    }

    private static void MakeSystemNormal(StarmapSystemRenderer __result, bool wasVisited)
    {
        __result.Init(__result.system, __result.systemColor, __result.CanTravel, wasVisited);
        __result.transform.localScale = new Vector3(1, 1, 1);
        Traverse.Create(__result).Field("selectedScale").SetValue(6f);
        Traverse.Create(__result).Field("deselectedScale").SetValue(4f);
        __result.starOuter.gameObject.SetActive(wasVisited);
    }







    [HarmonyPatch(typeof(SGNavigationScreen), "CreateSystemCallout")]
    public static class SGNavigationScreen_CreateSystemCallout_Patch
    {
        // keep for reference
        //public static bool Prefix(SGNavigationScreen __instance, List<SGNavStarSystemCallout> ___AllCallouts, ref SGNavStarSystemCallout __result)
        //{
        //    if (__instance == null)
        //        LogCritical("NULL");
        //
        //    var flyoutContainer = Traverse.Create(__instance).Field("FlyoutContainer").GetValue<Transform>();
        //
        //    GameObject gameObject = UnityGameInstance.BattleTechGame.DataManager
        //        .PooledInstantiate("uixPrfIndc_NAV_locationInfoCalloutV2-Element",
        //            BattleTechResourceType.UIModulePrefabs, new Vector3?(), new Quaternion?(), flyoutContainer);
        //    gameObject.transform.localScale = Vector3.one;
        //
        //    SGNavStarSystemCallout component = gameObject.GetComponent<SGNavStarSystemCallout>();
        //
        //    ___AllCallouts.Add(component);
        //    __result = component;
        //    
        //    GameObject testObject;
        //    TextMeshProUGUI objectText;
        //    testObject = new GameObject("Test Object");
        //    testObject.AddComponent<RectTransform>().sizeDelta = new Vector2(200, 200);
        //    var rectangle = testObject.GetComponent<RectTransform>();
        //    objectText = testObject.AddComponent<TextMeshProUGUI>();
        //    objectText.text = "POOOOOOOOOOOOOOOOOOOOOOP";
        //
        //    testObject.transform.SetParent(__instance.CachedTransform);
        //
        //    rectangle.anchorMin = new Vector2(0.5f, 1);
        //
        //    rectangle.anchorMax = new Vector2(0.5f, 1);
        //    rectangle.anchoredPosition = new Vector3(0, -75, 0);
        //    testObject.SetActive(true);
        //    
        //    return false;
        //}
        //}

        //[HarmonyPatch(typeof(SGNavigationScreen), "Init")]
        //[HarmonyPatch(new[] { typeof(SimGameState), typeof(SGRoomController_Navigation) })]
        //public static class SGNavigationScreen_Init_Patch
        //{
        //    internal static GameObject testObject;
        //    internal static TextMeshProUGUI objectText;

        //    public static void Prefix(SGNavigationScreen __instance, SimGameState simGame)
        //    {
        //        if (!Core.WarStatus.StartGameInitialize)
        //        {
        //            Galaxy_at_War.HotSpots.ProcessHotSpots();
        //            Core.WarStatus.StartGameInitialize = true;
        //        }
        //    }
        //}
    }
}

    //public static void ConfigurePopup(SGNavigationScreen navScreen)
    //{
        // keep for reference
        //textPanel = new GameObject("Faction Relationships");
        //textPanel.AddComponent(typeof(ScrollRect));
        //var scrollRect = textPanel.GetComponent(typeof(ScrollRect));
        //
        //scrollRect.transform.SetParent(navScreen.transform);
        //
        //scrollRect.gameObject.AddComponent<RectTransform>().sizeDelta = new Vector2(200, 200);
        //var rectangle = scrollRect.GetComponent<RectTransform>();
        //panelText = scrollRect.gameObject.AddComponent<TextMeshProUGUI>();
        //
        //// set font in the most roundabout way ever
        //var fonts = Resources.FindObjectsOfTypeAll(typeof(TMP_FontAsset));
        //foreach (var o in fonts)
        //{
        //    var font = (TMP_FontAsset) o;
        //    if (font.name == "UnitedSansSemiExt-Light")
        //        panelText.SetFont(font);
        //}
        //
        //textPanel.transform.SetParent(navScreen.transform);
        //rectangle.anchorMin = new Vector2(0.28f, 1);
        //rectangle.anchorMax = new Vector2(0.28f, 1);
        //rectangle.anchoredPosition = new Vector3(0, -200, 0);

        //textPanel.SetActive(true);
        //textPanel.AddComponent<Canvas>();
        //textPanel.AddComponent<CanvasRenderer>();
        //var canvas = textPanel.GetComponent<CanvasRenderer>();
        //var canvasRenderer = textPanel.GetComponent<CanvasRenderer>();
        //canvas.transform.SetParent(navScreen.transform);
        //canvasRenderer.transform.SetParent(navScreen.transform);
        //canvasRenderer.SetColor(Color.red);
        //var texture = new Texture2D(1, 1);
        //texture.SetPixel(1, 1, Color.red);
        //canvasRenderer.SetTexture(texture);