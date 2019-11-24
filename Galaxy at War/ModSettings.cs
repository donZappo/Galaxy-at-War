using System;
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

    public List<FactionValue> ImmuneToWar = new List<FactionValue>();

    public Dictionary<FactionValue, int> BonusAttackResources = new Dictionary<FactionValue, int>();

    public Dictionary<FactionValue, int> BonusDefensiveResources = new Dictionary<FactionValue, int>();

    public Dictionary<FactionValue, string> FactionTags = new Dictionary<FactionValue, string>();

    public Dictionary<FactionValue, string> FactionShops = new Dictionary<FactionValue, string>();

    public Dictionary<FactionValue, string> FactionShopItems = new Dictionary<FactionValue, string>();

    public int WarFrequency = 10;
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
    public float KLValueAllies = 10f;
    public float KLValuesNeutral = 50f;
    public float KLValuesEnemies = 90f;
    public bool DefendersUseARforDR = true;
    public float PriorityHatred = 40;
    public int InternalHotSpots = 4;
    public int ExternalHotSpots = 4;
    public int EscalationDays = 30;
    public float LogoScalar = 0.75f;
    public float LogoMaxSize = 10;
    public float BonusXPFactor = 0.2f;
    public float BonusCbillsFactor = 0.2f;
    public bool ISMCompatibility = true;
    public float ResourceScale = 0.5f;
    public bool UseSubsetOfSystems = true;
    public float SubSetFraction = 1.0f;
    public float StartingPirateActivity = 5.0f;
    public float BonusPirateResources = 0.0f;
    public float FractionPirateResources = 1.0f;
    public float PirateSystemFlagValue = 25.0f;
    public double ResourceSpread = 0.25;
    public float AdvanceToTaskTime = 0.25f;
    public bool ChangeDifficulty = true;

    public List<FactionValue> DefensiveFactions = new List<FactionValue>();

    public List<FactionValue> IncludedFactions = new List<FactionValue>();

    public Dictionary<FactionValue, string> FactionNames = new Dictionary<FactionValue, string>();

    public Dictionary<FactionValue, string> LogoNames = new Dictionary<FactionValue, string>();
    
    public Dictionary<string, FactionValue> FactionValues = new Dictionary<string, FactionValue>();
}