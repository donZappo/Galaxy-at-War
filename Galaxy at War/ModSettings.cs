using System.Collections.Generic;
using BattleTech;
using UnityEngine;

public class ModSettings
{
    //attacking resources
    public float planet_industry_poor = 1;
    public float planet_industry_mining = 2;
    public float planet_industry_rich = 3;
    public float planet_industry_manufacturing = 4;
    public float planet_industry_research = 5;
    public float planet_other_starleague = 6;

    //defending resources
    public float planet_industry_agriculture = 3;
    public float planet_industry_aquaculture = 3;
    public float planet_other_capital = 7;
    public float planet_other_megacity = 5;
    public float planet_pop_large = 4;
    public float planet_pop_medium = 3;
    public float planet_pop_none = 1;
    public float planet_pop_small = 2;
    public float planet_other_hub = 6;
    public float planet_other_comstar = 0;

    public List<Faction> ImmuneToWar = new List<Faction>();

    public Dictionary<Faction, int> BonusAttackResources = new Dictionary<Faction, int>();

    public Dictionary<Faction, int> BonusDefensiveResources = new Dictionary<Faction, int>();

    public Dictionary<Faction, string> FactionTags = new Dictionary<Faction, string>();

    public Dictionary<Faction, string> FactionShops = new Dictionary<Faction, string>();

    public Dictionary<Faction, string> FactionShopItems = new Dictionary<Faction, string>();

    public int WarFrequency = 6;
    public bool Debug = false;

    public string modDirectory;
    public int DominantInfluence = 75;
    public int MinorInfluencePool = 25;
    public float GlobalDefenseFactor = 1.25f;
    public int AResourceAdjustmentPerCycle = 1;
    public int DResourceAdjustmentPerCycle = 0;
    public int ResourceRandomizer = 4;
    public float DifficultyFactor = 1f;
    public float TakeoverThreshold = 10f;
    public float APRPush = 1f;
    public int APRPushRandomizer = 4;
    public float KLValueAllies = 25f;
    public float KLValuesNeutral = 50f;
    public float KLValuesEnemies = 100f;
    public bool DefendersUseARforDR = true;
    public float PriorityHatred = 50;
    public int InternalHotSpots = 4;
    public int ExternalHotSpots = 4;
    public int EscalationDays = 30;
    public float LogoScalar = 0.75f;
    public float LogoMaxSize = 10;
    public float BonusXPFactor = 0.2f;
    public float BonusCbillsFactor = 0.2f;
    public bool ISMCompatibility = true;
    public float ResourceScale = 1.0f;
    public bool UseSubsetOfSystems = false;
    public float SubSetFraction = 0.20f;
    
    public List<Faction> DefensiveFactions = new List<Faction>();

    public List<Faction> IncludedFactions = new List<Faction>();

    public Dictionary<Faction, string> FactionNames = new Dictionary<Faction, string>();

    public Dictionary<Faction, string> LogoNames = new Dictionary<Faction, string>();
}