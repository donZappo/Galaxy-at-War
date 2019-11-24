using System.Collections.Generic;
using System.Linq;
using BattleTech;
using Newtonsoft.Json;
using static Logger;
using Harmony;

// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming

public class WarStatus
{
    public List<SystemStatus> systems = new List<SystemStatus>();
    public List<DeathListTracker> deathListTracker = new List<DeathListTracker>();
    public List<WarFaction> warFactionTracker = new List<WarFaction>();
    public bool JustArrived = true;
    public bool Escalation = false;
    public WorkOrderEntry_Notification EscalationOrder;
    public int EscalationDays = 0;
    public List<string> PrioritySystems = new List<string>();
    public string CurSystem;
    public bool HotBoxTravelling;
    public bool StartGameInitialized = false;
    public bool InitializeAtStart = true;
    public List<string> SystemChangedOwners = new List<string>();
    public List<string> HotBox = new List<string>();
    public List<string> LostSystems = new List<string>();
    public bool GaW_Event_PopUp = false;

    public List<string> HomeContendedStrings = new List<string>();
    public List<string> ContendedStrings = new List<string>();
    public List<string> AbandonedSystems = new List<string>();
    
    public Dictionary<string, float> FullHomeContendedSystems = new Dictionary<string, float>();
    public List<string> HomeContendedSystems = new List<string>();
    public Dictionary<FactionValue, List<string>> ExternalPriorityTargets = new Dictionary<FactionValue, List<string>>();
    public List<string> FullPirateSystems = new List<string>();
    public List<string> PirateHighlight = new List<string>();
    public float PirateFlex = 0.0f;
    public float PirateResources;
    public float TempPRGain;
    public float MinimumPirateResources;
    public float StartingPirateResources;
    public float LastPRGain;

    public WarStatus()
    {
        var sim = UnityGameInstance.BattleTechGame.Simulation;
        CurSystem = sim.CurSystem.Name;
        TempPRGain = 0;
        HotBoxTravelling = false;
        HotBox = new List<string>();
        //initialize all WarFactions, DeathListTrackers, and SystemStatuses
        foreach (var faction in Core.Settings.IncludedFactions)
        {
            warFactionTracker.Add(new WarFaction(faction));
            deathListTracker.Add(new DeathListTracker(faction));
        }

        foreach (var system in sim.StarSystems)
        {
            if (system.OwnerValue == Core.Settings.FactionValues["NoFaction"])
                AbandonedSystems.Add(system.Name);
            var warFaction = warFactionTracker.Find(x => x.faction == system.OwnerValue);
            if (Core.Settings.DefensiveFactions.Contains(warFaction.faction) && Core.Settings.DefendersUseARforDR)
                warFaction.DefensiveResources += Core.GetTotalAttackResources(system);
            else
                warFaction.AttackResources += Core.GetTotalAttackResources(system);

            warFaction.DefensiveResources += Core.GetTotalDefensiveResources(system);
        }
        var MaxAR = warFactionTracker.Select(x => x.AttackResources).Max();
        var MaxDR = warFactionTracker.Select(x => x.DefensiveResources).Max();

        foreach (var faction in Core.Settings.IncludedFactions)
        {
            var warFaction = warFactionTracker.Find(x => x.faction == faction);
            if (Core.Settings.DefensiveFactions.Contains(faction) && Core.Settings.DefendersUseARforDR)
            {
                warFaction.DefensiveResources = MaxAR + MaxDR + Core.Settings.BonusAttackResources[faction] +
                    Core.Settings.BonusDefensiveResources[faction];
                warFaction.AttackResources = 0;
            }
            else
            {
                warFaction.AttackResources = MaxAR + Core.Settings.BonusAttackResources[faction];
                warFaction.DefensiveResources = MaxDR + Core.Settings.BonusDefensiveResources[faction];
            }
        }
        PirateResources = MaxAR * Core.Settings.FractionPirateResources + Core.Settings.BonusPirateResources;
        MinimumPirateResources = PirateResources;
        StartingPirateResources = PirateResources;

        foreach (var system in sim.StarSystems)
        {
            var systemStatus = new SystemStatus(sim, system.Name, system.OwnerValue);
            systems.Add(systemStatus);
            if (system.Tags.Contains("planet_other_pirate"))
            {
                FullPirateSystems.Add(system.Name);
                Galaxy_at_War.PiratesAndLocals.FullPirateListSystems.Add(systemStatus);
            }
        }
    }

    [JsonConstructor]
    public WarStatus(bool fake = false)
    {
    }
}

public class SystemStatus
{
    public string name;
    public FactionValue owner;

    public Dictionary<FactionValue, int> neighborSystems = new Dictionary<FactionValue, int>();
    public Dictionary<FactionValue, float> influenceTracker = new Dictionary<FactionValue, float>();

    internal SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;
    public float TotalResources;
    public bool PriorityDefense = false;
    public bool PriorityAttack = false;
    public List<FactionValue> CurrentlyAttackedBy = new List<FactionValue>();
    public bool Contended = false;
    public int DifficultyRating;
    public bool BonusSalvage = false;
    public bool BonusXP = false;
    public bool BonusCBills = false;
    public float AttackResources;
    public float DefenseResources;
    public float PirateActivity = 0.0f;
    public string CoreSystemID;

    internal StarSystem starSystem => sim.StarSystems.Find(s => s.Name == name);

    [JsonConstructor]
    public SystemStatus()
    {
        // don't want our ctor running at deserialization
    }

    public SystemStatus(SimGameState sim, string systemName, FactionValue faction)
    {
        //  LogDebug("SystemStatus ctor");
        name = systemName;
        owner = faction;
        // warFaction = Core.SystemStatus.warFactionTracker.Find(x => x.faction == owner);
        AttackResources = Core.GetTotalAttackResources(starSystem);
        DefenseResources = Core.GetTotalDefensiveResources(starSystem);
        TotalResources = AttackResources + DefenseResources;
        CoreSystemID = starSystem.Def.CoreSystemID;
        BonusCBills = false;
        BonusSalvage = false;
        BonusXP = false;
        if (starSystem.Tags.Contains("planet_other_pirate"))
            PirateActivity = Core.Settings.StartingPirateActivity;
        FindNeighbors();
        CalculateSystemInfluence();
        InitializeContracts();
    }

    public void FindNeighbors()
    {
        neighborSystems.Clear();
        var neighbors = sim.Starmap.GetAvailableNeighborSystem(starSystem);
        foreach (var neighborSystem in neighbors)
        {
            if (neighborSystems.ContainsKey(neighborSystem.OwnerValue))
                neighborSystems[neighborSystem.OwnerValue] += 1;
            else
                neighborSystems.Add(neighborSystem.OwnerValue, 1);
        }
    }

    // determine starting influence based on neighboring systems
    public void CalculateSystemInfluence()
    {
        influenceTracker.Clear();
        if (owner == Core.Settings.FactionValues["NoFaction"])
            influenceTracker.Add(owner, 100);
        if(owner == Core.Settings.FactionValues["Locals"])
            influenceTracker.Add(owner, 100);

        if (owner != Core.Settings.FactionValues["NoFaction"] && owner != Core.Settings.FactionValues["Locals"])
        {
            influenceTracker.Add(owner, Core.Settings.DominantInfluence);
            int remainingInfluence = Core.Settings.MinorInfluencePool;

            if (!(neighborSystems.Keys.Count == 1 && neighborSystems.Keys.Contains(owner)) && neighborSystems.Keys.Count != 0)
            {
                while (remainingInfluence > 0)
                {
                    foreach (var faction in neighborSystems.Keys)
                    {
                        if (faction != owner)
                        {
                            var influenceDelta = neighborSystems[faction];
                            remainingInfluence -= influenceDelta;
                            if (faction == Core.Settings.FactionValues["NoFaction"] || faction == Core.Settings.FactionValues["'Locals"])
                                continue;
                            if (influenceTracker.ContainsKey(faction))
                                influenceTracker[faction] += influenceDelta;
                            else
                                influenceTracker.Add(faction, influenceDelta);
                        }
                    }
                }
            }
        }
        foreach (var faction in Core.Settings.IncludedFactions)
        {
            if (!influenceTracker.Keys.Contains(faction))
                influenceTracker.Add(faction, 0);
        }

        // need percentages from InfluenceTracker data 
        var totalInfluence = influenceTracker.Values.Sum();
        var tempDict = new Dictionary<FactionValue, float>();
        foreach (var kvp in influenceTracker)
        {
            tempDict[kvp.Key] = kvp.Value / totalInfluence * 100;
        }
        influenceTracker = tempDict;
    }

    public void InitializeContracts()
    {
        var ContractEmployers = starSystem.Def.ContractEmployerIDList;
        var ContractTargets = starSystem.Def.ContractTargetIDList;

        ContractEmployers.Clear();
        ContractTargets.Clear();
        ContractEmployers.Add(owner.Name);

        foreach (FactionValue EF in Core.Settings.DefensiveFactions)
        {
            if (Core.Settings.ImmuneToWar.Contains(EF))
                continue;
            ContractTargets.Add(EF.Name);
        }
        if (!ContractTargets.Contains(owner.Name))
            ContractTargets.Add(owner.Name);

        foreach (var systemNeighbor in neighborSystems.Keys)
        {
            if (Core.Settings.ImmuneToWar.Contains(systemNeighbor))
                continue;
            if (!ContractEmployers.Contains(systemNeighbor.Name) && !Core.Settings.DefensiveFactions.Contains(systemNeighbor))
                ContractEmployers.Add(systemNeighbor.Name);

            if (!ContractTargets.Contains(systemNeighbor.Name) && !Core.Settings.DefensiveFactions.Contains(systemNeighbor))
                ContractTargets.Add(systemNeighbor.Name);
        }
    }
}

public class WarFaction
{
    public FactionValue faction;
    public bool GainedSystem;
    public bool LostSystem;
    public float DaysSinceSystemAttacked;
    public float DaysSinceSystemLost;
    public float AttackResources;
    public float DefensiveResources;
    public int MonthlySystemsChanged;
    public int TotalSystemsChanged;
    public float PirateARLoss;
    public float PirateDRLoss;

    public Dictionary<FactionValue, float> warFactionAttackResources = new Dictionary<FactionValue, float>();
    public Dictionary<FactionValue, List<string>> attackTargets = new Dictionary<FactionValue, List<string>>();
    public List<string> defenseTargets = new List<string>();
    public Dictionary<FactionValue, bool> IncreaseAggression = new Dictionary<FactionValue, bool>();


    [JsonConstructor]
    public WarFaction()
    {
        // deser ctor
    }


    public WarFaction(FactionValue faction)
    {
        LogDebug("WarFaction ctor");
        this.faction = faction;
        GainedSystem = false;
        LostSystem = false;
        DaysSinceSystemAttacked = 0;
        DaysSinceSystemLost = 0;
        MonthlySystemsChanged = 0;
        TotalSystemsChanged = 0;
        PirateARLoss = 0;
        PirateDRLoss = 0;
        foreach (var startfaction in Core.Settings.IncludedFactions)
            IncreaseAggression.Add(startfaction, false);
    }
}

public class DeathListTracker
{
    public FactionValue faction;
    private FactionDef factionDef;
    public Dictionary<FactionValue, float> deathList = new Dictionary<FactionValue, float>();
    public List<FactionValue> AttackedBy = new List<FactionValue>();

    public List<FactionValue> Enemies => deathList.Where(x => x.Value >= 75).Select(x => x.Key).ToList();
    public List<FactionValue> Allies => deathList.Where(x => x.Value <= 25).Select(x => x.Key).ToList();

    // can't serialize these so make them private
    private SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;
    
    [JsonConstructor]
    public DeathListTracker()
    {
        // deser ctor
    }

    public DeathListTracker(FactionValue faction)
    {
        LogDebug("DeathListTracker ctor");
        this.faction = faction;
        factionDef = sim.FactionsDict
            .Where(kvp => kvp.Key == faction)
            .Select(kvp => kvp.Value).First();
        foreach (var def in sim.FactionsDict.Values)
        {
            if (!Core.Settings.IncludedFactions.Contains(def.Faction))
                continue;
            if (factionDef != def && factionDef.Enemies.Contains(def.Faction))
                deathList.Add(def.Faction, Core.Settings.KLValuesEnemies);
            else if (factionDef != def && factionDef.Allies.Contains(def.Faction))
                deathList.Add(def.Faction, Core.Settings.KLValueAllies);
            else if (factionDef != def)
                deathList.Add(def.Faction, Core.Settings.KLValuesNeutral);
        }
    }
}