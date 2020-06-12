using System;
using System.Collections.Generic;
using BattleTech;
using BattleTech.Framework;

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
    public int MaxContracts = 4;
    public bool SetMaxContracts = true;

    public List<string> ImmuneToWar = new List<string>();

    public Dictionary<string, int> BonusAttackResources = new Dictionary<string, int>();

    public Dictionary<string, int> BonusDefensiveResources = new Dictionary<string, int>();

    public Dictionary<string, int> BonusAttackResources_ISM = new Dictionary<string, int>();

    public Dictionary<string, int> BonusDefensiveResources_ISM = new Dictionary<string, int>();

    public Dictionary<string, string> FactionTags = new Dictionary<string, string>();

    public Dictionary<string, string> FactionShops = new Dictionary<string, string>();

    public Dictionary<string, string> FactionShopItems = new Dictionary<string, string>();

    public int WarFrequency = 10;
    public bool Debug = false;
    public bool AggressiveToggle = false;

    public string modDirectory;
    public int DominantInfluence = 75;
    public int MinorInfluencePool = 25;
    public float GlobalDefenseFactor = 1.5f;
    public int AResourceAdjustmentPerCycle = 1;
    public int DResourceAdjustmentPerCycle = 1;
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
    public bool ExpandedMap = true;
    public float ResourceScale = 0.5f;
    public bool UseSubsetOfSystems = true;
    public float SubSetFraction = 1.0f;
    public float StartingPirateActivity = 5.0f;
    public float BonusPirateResources = 0.0f;
    public float FractionPirateResources = 1.0f;
    public float PirateSystemFlagValue = 25.0f;

    public float StartingPirateActivity_ISM = 5.0f;
    public float BonusPirateResources_ISM = 0.0f;
    public float FractionPirateResources_ISM = 0.1f;
    public float PirateSystemFlagValue_ISM = 25.0f;


    public double ResourceSpread = 0.25;
    public float AdvanceToTaskTime = 0.25f;
    public bool ChangeDifficulty = true;
    public bool ResetMap = false;
    public bool LongWarTesting = false;
    public int DeploymentMinDays = 2;
    public int DeploymentMaxDays = 6;
    public float DeploymentContracts = 2.0f;
    public double DeploymentRerollBound = 0.33;
    public double DeploymentEscalationFactor = 1.0;
    public double InfluenceDivisor = 1.25;
    public string DeploymentReward_01 = "";
    public string DeploymentReward_02 = "";
    public string DeploymentReward_03 = "";
    public string DeploymentReward_04 = "";
    public string DeploymentReward_05 = "";
    public string DeploymentReward_06 = "";
    public string GaW_Police = "ComStar";
    public bool GaW_PoliceSupport = false;
    public int GaW_Police_ARBonus = 200;
    public int GaW_Police_DRBonus = 200;
    public int GaW_Police_SupportTime = 3;

    public float MinimumResourceFactor = 0.001f;
    public float MaximumResourceFactor = 0.003f;
    public double SystemDefenseCutoff = 0.05;

    public List<string> DefensiveFactions = new List<string>();

    public List<string> IncludedFactions = new List<string>();
    public List<string> IncludedFactions_ISM = new List<string>();

    public Dictionary<string, string> FactionNames = new Dictionary<string, string>();

    public Dictionary<string, string> LogoNames = new Dictionary<string, string>();

    public Dictionary<string, double> ContractImpact = new Dictionary<string, double>();
    public List<string> NoOffensiveContracts = new List<string>();

    public bool HyadesRimCompatible = false;
    public List<string> HyadesPirates = new List<string>();
    public List<string> HyadesFlashpointSystems = new List<string>();
    public List<string> HyadesAppearingPirates = new List<string>();
    public Dictionary<string, List<string>> FactionsAlwaysAllies = new Dictionary<string, List<string>>();
    public List<string> HyadesEmployersOnly = new List<string>();
    public List<string> HyadesTargetsOnly = new List<string>();
    public List<string> HyadesNeverControl = new List<string>();
    public Dictionary<string, string> FlashpointReleaseSystems = new Dictionary<string, string>();
}