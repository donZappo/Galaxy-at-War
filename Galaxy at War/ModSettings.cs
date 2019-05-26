using System.Collections.Generic;
using BattleTech;
using static Core;

public class ModSettings
{
    public int planet_industry_poor = -1;
    public int planet_industry_mining = 1;
    public int planet_industry_rich = 2;
    public int planet_industry_manufacturing = 4;
    public int planet_industry_research = 5;
    public int planet_other_starleague = 6;
    public int planet_other_comstar = 0;

    public int WarFrequency = 6;

    public Dictionary<string, int> ResourceMap = new Dictionary<string, int>
    {
        {"Steiner", 10},
        {"Kurita", 10},
        {"Davion", 10},
        {"Liao", 10},
        {"Marik", 10},
        {"TaurianConcordat", 10},
        {"MagistracyOfCanopus", 10},
        {"AuriganPirates", 10}
    };

    public Dictionary<Faction, string> FactionTags = new Dictionary<Faction, string>
    {
        {Faction.Liao,  "planet_faction_liao"},
        {Faction.Steiner, "planet_faction_steiner" },
        {Faction.Marik, "planet_faction_marik" },
        {Faction.Davion, "planet_faction_davion" },
        {Faction.Kurita, "planet_faction_kurita" },
        {Faction.AuriganDirectorate, "planet_faction_directorate" },
        {Faction.TaurianConcordat, "planet_faction_taurian"},
        {Faction.MagistracyOfCanopus, "planet_faction_magistracy" },
        {Faction.NoFaction, "planet_faction_nofaction" },
        {Faction.Locals, "planet_faction_independent" },
        {Faction.AuriganRestoration, "planet_faction_restoration" }
    };

    public Dictionary<Faction, string> FactionShops = new Dictionary<Faction, string>
    {
        {Faction.Liao,  "itemCollection_minor_Liao"},
        {Faction.Marik, "itemCollection_minor_Marik" },
        {Faction.Davion, "itemCollection_minor_Davion" },
        {Faction.AuriganDirectorate, "itemCollection_minor_AuriganDirectorate" },
        {Faction.TaurianConcordat, "itemCollection_minor_TaurianConcordat"},
        {Faction.MagistracyOfCanopus, "itemCollection_minor_MagistracyOfCanopus" },
        {Faction.NoFaction, "itemCollection_minor_Locals" },
        {Faction.Locals, "itemCollection_minor_Locals" },
        {Faction.AuriganRestoration, "itemCollection_minor_AuriganRestoration" }
    };

    public Dictionary<Faction, string> FactionShopItems = new Dictionary<Faction, string>
    {
        {Faction.Liao,  "itemCollection_faction_Liao"},
        {Faction.Marik, "itemCollection_faction_Marik" },
        {Faction.Davion, "itemCollection_faction_Davion" },
        {Faction.AuriganDirectorate, null },
        {Faction.TaurianConcordat, "itemCollection_faction_TaurianConcordat"},
        {Faction.MagistracyOfCanopus, "itemCollection_faction_MagistracyOfCanopus" },
        {Faction.NoFaction, null },
        {Faction.Locals, null },
        {Faction.AuriganRestoration, null }
    };

    public Dictionary<Faction, List<StarSystem>> AttackTargets = new Dictionary<Faction, List<StarSystem>>
    {
    };

    public Dictionary<Faction, List<StarSystem>> DefenseTargets = new Dictionary<Faction, List<StarSystem>>
    {
    };

    public List<WarFaction> FactionTracker = new List<WarFaction>();

    public bool Debug = true;
    public string modDirectory;
    public int DominantInfluence = 50;
    public int MinorInfluencePool = 50;

    public List<Faction> ExcludedFactions = new List<Faction>()
    {
        Faction.Locals, Faction.Unknown,
        Faction.SelfEmployed, Faction.NoFaction,
    };
}