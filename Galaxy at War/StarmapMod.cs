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
            var starSystem = (StarSystem) data;
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
        eventPanel = LazySingletonBehavior<UIManager>.Instance.CreatePopupModule<SGEventPanel>("");
        eventPanel.gameObject.SetActive(true);
        GameObject.Find("uixPrfPanl_spotIllustration_750-MANAGED").SetActive(false);

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
            sb.AppendLine($"<b><u>{Core.Settings.FactionNames[tracker.faction]}</b></u>\n");
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
        }

        sb.AppendLine();
        return sb.ToString();
    }

    [HarmonyPatch(typeof(SimGameState), "Update")]
    public static class FactionPopup_Patch
    {
        public static void Postfix(SimGameState __instance)
        {
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.F))
                eventPanel.gameObject.SetActive(!eventPanel.gameObject.activeInHierarchy);
        }
    }

    private static string BuildInfluenceString(StarSystem starSystem)
    {
        var factionString = new StringBuilder();
        factionString.AppendLine(starSystem.Name);
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
        factionString.AppendLine("\n\nTotal System Resources: " + tracker.TotalResources);
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

    [HarmonyPatch(typeof(StarmapRenderer), "GetSystemRenderer")]
    [HarmonyPatch(new[] {typeof(StarSystemNode)})]
    public static class StarmapRenderer_GetSystemRenderer_Patch
    {
        public static void Postfix(StarmapRenderer __instance, ref StarmapSystemRenderer __result)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            //Galaxy_at_War.HotSpots.ProcessHotSpots();

            var visitedStarSystems = Traverse.Create(sim).Field("VisitedStarSystems").GetValue<List<string>>();
            var wasVisited = visitedStarSystems.Contains(__result.name);
            if (Galaxy_at_War.HotSpots.HomeContendedStrings.Contains(__result.name))
                MakeSystemPurple(__result, wasVisited);
            else if (__result.systemColor == Color.magenta)
                MakeSystemNormal(__result, wasVisited);
        }
    }

    [HarmonyPatch(typeof(StarmapRenderer), "RefreshSystems")]
    public static class StarmapRenderer_RefreshSystems_Patch
    {
        public static void Postfix(StarmapRenderer __instance)
        {
            if (!Core.Settings.ISMCompatibility)
                Galaxy_at_War.DynamicLogos.PlaceAndScaleLogos(Core.Settings.LogoNames, __instance);
        }
    }

    [HarmonyPatch(typeof(SGNavStarSystemCallout), "Init", typeof(StarmapRenderer), typeof(StarSystem))]
    public static class SGNavStarSystemCallout_Init_Patch
    {
        public static void Prefix(SGNavStarSystemCallout __instance, TextMeshProUGUI ___LabelField, TextMeshProUGUI ___NameField)
        {
            // set font in the most roundabout way ever
            var fonts = Resources.FindObjectsOfTypeAll(typeof(TMP_FontAsset));
            foreach (var o in fonts)
            {
                var font = (TMP_FontAsset) o;
                if (font.name == "UnitedSansSemiExt-Light")
                    ___LabelField.SetFont(font);
                ___NameField.SetFont(font);
            }
        }
    }

    private static void MakeSystemPurple(StarmapSystemRenderer __result, bool wasVisited)
    {
        var blackMarketIsActive = __result.blackMarketObj.gameObject.activeInHierarchy;
        var fpAvailableIsActive = __result.flashpointAvailableObj.gameObject.activeInHierarchy;
        var fpActiveIsActive = __result.flashpointActiveObj.gameObject.activeInHierarchy;
        __result.Init(__result.system, Color.magenta, __result.CanTravel, wasVisited);
        if (fpAvailableIsActive)
            __result.flashpointAvailableObj.SetActive(true);
        if (fpActiveIsActive)
            __result.flashpointActiveObj.SetActive(true);
        if (blackMarketIsActive)
            __result.blackMarketObj.gameObject.SetActive(true);

        Traverse.Create(__result).Field("selectedScale").SetValue(10f);
        Traverse.Create(__result).Field("deselectedScale").SetValue(8f);
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
        //[HarmonyPatch(new[] {typeof(SimGameState), typeof(SGRoomController_Navigation)})]
        //public static class SGNavigationScreen_Init_Patch
        //{
        //    internal static GameObject testObject;
        //    internal static TextMeshProUGUI objectText;
        //
        //    public static void Prefix(SGNavigationScreen __instance, SimGameState simGame)
        //    {
        //        testObject = new GameObject("Test Object");
        //        testObject.AddComponent<RectTransform>().sizeDelta = new Vector2(200, 200);
        //        objectText = testObject.AddComponent<TextMeshProUGUI>();
        //        objectText.text = "Some text";
        //        testObject.transform.SetParent(__instance.transform);
        //        testObject.SetActive(true);
        //    }
    }

    public static void ConfigurePopup(SGNavigationScreen navScreen)
    {
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
    }
}