using System.Collections.Generic;
using BattleTech;

public class ModSettings
{
    //attacking resources
    public int planet_industry_poor = 1;
    public int planet_industry_mining = 2;
    public int planet_industry_rich = 3;
    public int planet_industry_manufacturing = 4;
    public int planet_industry_research = 5;
    public int planet_other_starleague = 6;

    //defending resources
    public int planet_industry_agriculture = 3;
    public int planet_industry_aquaculture = 3;
    public int planet_other_capital = 7;
    public int planet_other_megacity = 5;
    public int planet_pop_large = 4;
    public int planet_pop_medium = 3;
    public int planet_pop_none = 1;
    public int planet_pop_small = 2;
    public int planet_other_hub = 6;
    public int planet_other_comstar = 0;

    public int WarFrequency = 3;

    public Dictionary<Faction, int> AttackResourceMap = new Dictionary<Faction, int>
    {
        {Faction.Davion, 0},
        {Faction.Liao, 0},
        {Faction.Marik, 0},
        {Faction.TaurianConcordat, 0},
        {Faction.MagistracyOfCanopus, 0},
        {Faction.Locals, 0},
        {Faction.NoFaction, 0},
        {Faction.AuriganRestoration, 0}
    };

    public Dictionary<Faction, int> DefensiveResourceMap = new Dictionary<Faction, int>
    {
        {Faction.Davion, 0},
        {Faction.Liao, 0},
        {Faction.Marik, 0},
        {Faction.TaurianConcordat, 0},
        {Faction.MagistracyOfCanopus, 0},
        {Faction.Locals, 0},
        {Faction.NoFaction, 0},
        {Faction.AuriganRestoration, 0}
    };

    public Dictionary<Faction, string> FactionTags = new Dictionary<Faction, string>
    {
        {Faction.Liao, "planet_faction_liao"},
        {Faction.Steiner, "planet_faction_steiner"},
        {Faction.Marik, "planet_faction_marik"},
        {Faction.Davion, "planet_faction_davion"},
        {Faction.Kurita, "planet_faction_kurita"},
        {Faction.AuriganDirectorate, "planet_faction_directorate"},
        {Faction.TaurianConcordat, "planet_faction_taurian"},
        {Faction.MagistracyOfCanopus, "planet_faction_magistracy"},
        {Faction.NoFaction, "planet_faction_nofaction"},
        {Faction.Locals, "planet_faction_independent"},
        {Faction.AuriganRestoration, "planet_faction_restoration"}
    };

    public Dictionary<Faction, string> FactionShops = new Dictionary<Faction, string>
    {
        {Faction.Liao, "itemCollection_minor_Liao"},
        {Faction.Marik, "itemCollection_minor_Marik"},
        {Faction.Davion, "itemCollection_minor_Davion"},
        {Faction.AuriganDirectorate, "itemCollection_minor_AuriganDirectorate"},
        {Faction.TaurianConcordat, "itemCollection_minor_TaurianConcordat"},
        {Faction.MagistracyOfCanopus, "itemCollection_minor_MagistracyOfCanopus"},
        {Faction.NoFaction, "itemCollection_minor_Locals"},
        {Faction.Locals, "itemCollection_minor_Locals"},
        {Faction.AuriganRestoration, "itemCollection_minor_AuriganRestoration"}
    };

    public Dictionary<Faction, string> FactionShopItems = new Dictionary<Faction, string>
    {
        {Faction.Liao, "itemCollection_faction_Liao"},
        {Faction.Marik, "itemCollection_faction_Marik"},
        {Faction.Davion, "itemCollection_faction_Davion"},
        {Faction.AuriganDirectorate, null},
        {Faction.TaurianConcordat, "itemCollection_faction_TaurianConcordat"},
        {Faction.MagistracyOfCanopus, "itemCollection_faction_MagistracyOfCanopus"},
        {Faction.NoFaction, null},
        {Faction.Locals, null},
        {Faction.AuriganRestoration, null}
    };

    public bool Debug = true;
    public string modDirectory;
    public int DominantInfluence = 75;
    public int MinorInfluencePool = 25;
    public float GlobalDefenseFactor = 1.15f;
    public int ResourceAdjustmentPerCycle = 1;
    public int ResourceRandomizer = 4;
    public float DifficultyFactor = 1f;
    public float TakeoverThreshold = 20f;
    public float APRPush = 1f;
    public int APRPushRandomizer = 4;
    public float KLValueAllies = 25f;
    public float KLValuesNeutral = 50f;
    public float KLValuesEnemies = 100f;
    public bool DefendersUseARforDR = true;

    public List<Faction> DefensiveFactions = new List<Faction>()
    {
        Faction.Locals,
        Faction.NoFaction,
    };

    public List<Faction> IncludedFactions = new List<Faction>()
    {
        Faction.AuriganRestoration,
        Faction.Davion,
        Faction.Liao,
        Faction.MagistracyOfCanopus,
        Faction.Marik,
        Faction.TaurianConcordat,
        Faction.Locals,
        Faction.NoFaction
    };

    public Dictionary<Faction, string> FactionNames = new Dictionary<Faction, string>()
    {
        {Faction.AuriganRestoration, "The Arana Restoration"},
        {Faction.Davion, "The Federated Suns"},
        {Faction.Liao, "The Capellan Confederation"},
        {Faction.MagistracyOfCanopus, "The Magistracy of Canopus"},
        {Faction.Marik, "The Free Worlds League"},
        {Faction.TaurianConcordat, "The Taurian Concordat"},
        {Faction.Locals, "Local Factions"},
        {Faction.NoFaction, "No Owner"}
    };
}