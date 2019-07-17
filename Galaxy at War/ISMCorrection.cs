using System;
using System.Collections.Generic;
using BattleTech;
using BattleTech.UI;
using Harmony;
using System.Linq;

namespace Galaxy_at_War
{
    class ISMCorrection
    {
        public static void CorrectFactionStores(SystemStatus system)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            var ssDict = sim.StarSystemDictionary;
            var starSystemDef = ssDict[system.CoreSystemID].Def;
           
            try
            {
                starSystemDef.SystemShopItems.Clear();
                starSystemDef.FactionShopItems.Clear();
                starSystemDef.BlackMarketShopItems.Clear();
            }
            catch
            {
            }
            if (Core.WarStatus.AbandonedSystems.Contains(system.name))
                return;

            List<string> BlackMarketShop = new List<string>();
            BlackMarketShop.Add("itemCollection_faction_AuriganPirates");
            if (starSystemDef.Tags.Any(x => x.StartsWith("planet_other_factionhq")))
            {
                Traverse.Create(starSystemDef).Property("FactionShopOwner").SetValue(starSystemDef.Owner);
                if (starSystemDef.FactionShopItems != null)
                    starSystemDef.FactionShopItems.Clear();
                string FactionStoreName = "itemCollection_faction_" + starSystemDef.Owner.ToString();
                List<string> FactionShopList = new List<string>();
                FactionShopList.Add(FactionStoreName);
                Traverse.Create(starSystemDef).Property("FactionShopItems").SetValue(FactionShopList);
            }
            if (starSystemDef.Tags.Contains("planet_other_blackmarket"))
                Traverse.Create(starSystemDef).Property("BlackMarketShopItems").SetValue(BlackMarketShop);

            List<string> SystemStores = new List<string>();

            if (!starSystemDef.Tags.Contains("planet_other_empty") && !starSystemDef.Tags.Contains("planet_pop_none"))
            {
                if (starSystemDef.Tags.Contains("planet_industry_research"))
                    SystemStores.Add("itemCollection_shop_research");
                if (starSystemDef.Tags.Contains("planet_industry_rich"))
                    SystemStores.Add("itemCollection_shop_industrial");
                if (starSystemDef.Tags.Contains("planet_other_starleague"))
                    SystemStores.Add("itemCollection_shop_starleague");
                if (starSystemDef.Tags.Contains("planet_other_battlefield"))
                    SystemStores.Add("itemCollection_shop_battlefield");
                if (starSystemDef.Tags.Contains("planet_progress_1") || starSystemDef.Tags.Any(x => x.StartsWith("planet_progress_2")))
                    SystemStores.Add("itemCollection_shop_progression_med");
                if (starSystemDef.Owner != Faction.NoFaction)
                {
                    if (starSystemDef.Tags.Contains("planet_pop_small") && starSystemDef.Tags.Contains("planet_pop_medium"))
                    {
                        string FactionMinorStoreName = "itemCollection_minor_" + starSystemDef.Owner.ToString();
                        SystemStores.Add(FactionMinorStoreName);
                    }
                    if (starSystemDef.Tags.Contains("planet_pop_large") && starSystemDef.Tags.Contains("planet_other_megacity"))
                    {
                        string FactionMajorStoreName = "itemCollection_major_" + starSystemDef.Owner.ToString();
                        SystemStores.Add(FactionMajorStoreName);
                    }
                }
                if (starSystemDef.Description.Name == "Itrom")
                    SystemStores.Add("itemCollection_MechParts_RestoItrom");
                if (starSystemDef.Description.Name == "Panzyr")
                    SystemStores.Add("itemCollection_MechParts_RestoPanzyr");
                if (starSystemDef.Description.Name == "Smithon")
                    SystemStores.Add("itemCollection_MechParts_RestoSmithon");
                if (starSystemDef.Description.Name == "Tyrion")
                    SystemStores.Add("itemCollection_MechParts_RestoTyrlon");
                if (starSystemDef.Description.Name == "Weldry")
                    SystemStores.Add("itemCollection_MechParts_RestoWeldry");
            }
            Traverse.Create(starSystemDef).Property("SystemShopItems").SetValue(SystemStores);
            try
            {
                Shop.RefreshType refreshShop = Shop.RefreshType.ForceRefresh;
                system.starSystem.SystemShop.Rehydrate(sim, system.starSystem, starSystemDef.SystemShopItems, refreshShop, Shop.ShopType.System);
            }
            catch { }
        }
    }
}
