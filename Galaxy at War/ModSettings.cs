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

    // have to come up with something better than two variables starting with FactionResources
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