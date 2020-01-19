using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using BattleTech.Rendering;
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
    public bool Deployment = false;
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
    public List<string> DeploymentContracts = new List<string>();
    public string DeploymentEmployer = "Marik";
    public double DeploymentInfluenceIncrease = 1.0;
    
    public Dictionary<string, float> FullHomeContendedSystems = new Dictionary<string, float>();
    public List<string> HomeContendedSystems = new List<string>();
    public Dictionary<string, List<string>> ExternalPriorityTargets = new Dictionary<string, List<string>>();
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
        Core.FactionValues = FactionEnumeration.FactionList;
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
            if (system.OwnerValue.Name == "NoFaction" || system.OwnerValue.Name == "AuriganPirates")
                AbandonedSystems.Add(system.Name);
            var warFaction = warFactionTracker.Find(x => x.faction == system.OwnerValue.Name);
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
            var systemStatus = new SystemStatus(sim, system.Name, system.OwnerValue.Name);
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

public class SystemStatus : IComparable
{
    public string name;
    public string owner;

    public Dictionary<string, int> neighborSystems = new Dictionary<string, int>();
    public Dictionary<string, float> influenceTracker = new Dictionary<string, float>();

    internal SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;
    public float TotalResources;
    public bool PriorityDefense = false;
    public bool PriorityAttack = false;
    public List<string> CurrentlyAttackedBy = new List<string>();
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

    public SystemStatus(SimGameState sim, string systemName, string faction)
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
            if (neighborSystems.ContainsKey(neighborSystem.OwnerValue.Name))
                neighborSystems[neighborSystem.OwnerValue.Name] += 1;
            else
                neighborSystems.Add(neighborSystem.OwnerValue.Name, 1);
        }
    }

    // determine starting influence based on neighboring systems
    public void CalculateSystemInfluence()
    {
        influenceTracker.Clear();
        if (owner == "NoFaction")
            influenceTracker.Add("NoFaction", 100);
        if (owner == "Locals")
            influenceTracker.Add(owner, 100);

        if (owner != "NoFaction" && owner != "Locals")
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
                            if (Core.Settings.DefensiveFactions.Contains(faction))
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
        var tempDict = new Dictionary<string, float>();
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
        ContractEmployers.Add(owner);

        foreach (string EF in Core.Settings.DefensiveFactions)
        {
            if (Core.Settings.ImmuneToWar.Contains(EF))
                continue;
            ContractTargets.Add(EF);
        }
        if (!ContractTargets.Contains(owner))
            ContractTargets.Add(owner);

        foreach (var systemNeighbor in neighborSystems.Keys)
        {
            if (Core.Settings.ImmuneToWar.Contains(systemNeighbor))
                continue;
            if (!ContractEmployers.Contains(systemNeighbor) && !Core.Settings.DefensiveFactions.Contains(systemNeighbor))
                ContractEmployers.Add(systemNeighbor);

            if (!ContractTargets.Contains(systemNeighbor) && !Core.Settings.DefensiveFactions.Contains(systemNeighbor))
                ContractTargets.Add(systemNeighbor);
        }
    }

    public int CompareTo(object obj)
    {
        if (!(obj is StarSystem other))
            return 1;
        
        if (starSystem.Name.ToLower()[0] == other.Name.ToLower()[0])
        {
            return 0;
        }

        if (starSystem.Name.ToLower()[0] > other.Name.ToLower()[0])
        {
            return 1;
        }

        if (starSystem.Name.ToLower()[0] < other.Name.ToLower()[0])
        {
            return -1;
        }

        return 1;
    }
}

public class WarFaction
{
    public string faction;
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

    public Dictionary<string, float> warFactionAttackResources = new Dictionary<string, float>();
    public Dictionary<string, List<string>> attackTargets = new Dictionary<string, List<string>>();
    public List<string> defenseTargets = new List<string>();
    public Dictionary<string, bool> IncreaseAggression = new Dictionary<string, bool>();
    public List<string> adjacentFactions = new List<string>();


    [JsonConstructor]
    public WarFaction()
    {
        // deser ctor
    }


    public WarFaction(string faction)
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
    public string faction;
    private FactionDef factionDef;
    public Dictionary<string, float> deathList = new Dictionary<string, float>();
    public List<string> AttackedBy = new List<string>();

    public List<string> Enemies => deathList.Where(x => x.Value >= 75).Select(x => x.Key).ToList();
    public List<string> Allies => deathList.Where(x => x.Value <= 25).Select(x => x.Key).ToList();

    // can't serialize these so make them private
    private SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;
    
    [JsonConstructor]
    public DeathListTracker()
    {
        // deser ctor
    }

    public DeathListTracker(string faction)
    {
        LogDebug("DeathListTracker ctor");

        this.faction = faction;
        factionDef = sim.GetFactionDef(faction);

        foreach (var factionNames in Core.Settings.IncludedFactions)
        {
            var def = sim.GetFactionDef(factionNames);
            if (!Core.Settings.IncludedFactions.Contains(def.FactionValue.Name))
                continue;
            if (factionDef != def && factionDef.Enemies.Contains(def.FactionValue.Name))
                deathList.Add(def.FactionValue.Name, Core.Settings.KLValuesEnemies);
            else if (factionDef != def && factionDef.Allies.Contains(def.FactionValue.Name))
                deathList.Add(def.FactionValue.Name, Core.Settings.KLValueAllies);
            else if (factionDef != def)
                deathList.Add(def.FactionValue.Name, Core.Settings.KLValuesNeutral);
        }
    }
}