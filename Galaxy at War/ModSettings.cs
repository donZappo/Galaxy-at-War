using System.Collections.Generic;
using BattleTech;


public class ModSettings
{
    public static int planet_industry_poor = -1;
    public static int planet_industry_mining = 1;
    public static int planet_industry_rich = 2;
    public static int planet_industry_manufacturing = 4;
    public static int planet_industry_research = 5;
    public static int planet_other_starleague = 6;
    public static int planet_other_comstar = 0;

    public static Dictionary<string, int> FactionResources = new Dictionary<string, int>()
    {
        {"Steiner", 10 },
        {"Kurita", 10 },
        {"Davion", 10 },
        {"Liao", 10 },
        {"Marik", 10 },
        {"TaurianConcordat", 10 },
        {"MagistracyOfCanopus", 10 },
        {"AuriganPirates", 10 }
    };

    public static List<Core.FactionResources> FactionResourcesHolder = new List<Core.FactionResources>();

    public bool Debug = true;
    public string ModDirectory;
}