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
    public static Dictionary<Faction, List<StarSystem>> ExternalPriorityTargets
        = new Dictionary<Faction, List<StarSystem>>();
    public bool JustArrived = true;
    public bool Escalation = false;
    public WorkOrderEntry_Notification EscalationOrder;
    public int EscalationDays = 0;
    public List<string> PrioritySystems = new List<string>();
    public string CurSystem;
    public bool HotBoxTravelling;
    public bool StartGameInitialized;

    public WarStatus()
    {
        var sim = UnityGameInstance.BattleTechGame.Simulation;
        CurSystem = sim.CurSystem.Name;
        StartGameInitialized = false;
        HotBoxTravelling = false;
        //initialize all WarFactions, DeathListTrackers, and SystemStatuses
        foreach (var faction in Core.Settings.IncludedFactions)
        {
            warFactionTracker.Add(new WarFaction(faction));
            deathListTracker.Add(new DeathListTracker(faction));
        }

        foreach (var system in sim.StarSystems)
        {
            var warFaction = warFactionTracker.Find(x => x.faction == system.Owner);
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


        foreach (var system in sim.StarSystems)
        {
            systems.Add(new SystemStatus(sim, system.Name, system.Owner));
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
    public Faction owner;

    public Dictionary<Faction, int> neighborSystems = new Dictionary<Faction, int>();
    public Dictionary<Faction, float> influenceTracker = new Dictionary<Faction, float>();

    internal SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;
    public float TotalResources;
    public bool HotBox = false;
    public bool PriorityDefense = false;
    public bool PriorityAttack = false;
    public List<Faction> CurrentlyAttackedBy = new List<Faction>();
    public bool Contended = false;
    public int DifficultyRating;
    public bool BonusSalvage = false;
    public bool BonusXP = false;
    public bool BonusCBills = false;

    internal StarSystem starSystem => sim.StarSystems.Find(s => s.Name == name);

    [JsonConstructor]
    public SystemStatus()
    {
        // don't want our ctor running at deserialization
    }

    public SystemStatus(SimGameState sim, string systemName, Faction faction)
    {
        //  LogDebug("SystemStatus ctor");
        name = systemName;
        owner = faction;
        // warFaction = Core.SystemStatus.warFactionTracker.Find(x => x.faction == owner);
        TotalResources = Core.GetTotalAttackResources(starSystem) + Core.GetTotalDefensiveResources(starSystem);
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
            if (neighborSystems.ContainsKey(neighborSystem.Owner))
                neighborSystems[neighborSystem.Owner] += 1;
            else
                neighborSystems.Add(neighborSystem.Owner, 1);
        }
    }

    // determine starting influence based on neighboring systems
    public void CalculateSystemInfluence()
    {
        influenceTracker.Clear();
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
                        if (influenceTracker.ContainsKey(faction))
                            influenceTracker[faction] += influenceDelta;
                        else
                            influenceTracker.Add(faction, influenceDelta);
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
        var tempDict = new Dictionary<Faction, float>();
        foreach (var kvp in influenceTracker)
        {
            tempDict[kvp.Key] = kvp.Value / totalInfluence * 100;
        }
        influenceTracker = tempDict;
    }

    public void InitializeContracts()
    {
        var ContractEmployers = starSystem.Def.ContractEmployers;
        var ContractTargets = starSystem.Def.ContractTargets;

        ContractEmployers.Clear();
        ContractTargets.Clear();
        ContractEmployers.Add(owner);
        ContractTargets.Add(owner);

        foreach (var systemNeighbor in neighborSystems.Keys)
        {
            if (!ContractEmployers.Contains(systemNeighbor) && !Core.Settings.DefensiveFactions.Contains(systemNeighbor))
                ContractEmployers.Add(systemNeighbor);

            if (!ContractTargets.Contains(systemNeighbor) && !Core.Settings.DefensiveFactions.Contains(systemNeighbor))
                ContractTargets.Add(systemNeighbor);
        }

        if (ContractTargets.Count() == 1)
        {
            ContractTargets.Clear();
            foreach (Faction EF in sim.FactionsDict[owner].Enemies)
                ContractTargets.Add(EF);
        }
    }
}

public class WarFaction
{
    public Faction faction;
    public bool GainedSystem;
    public bool LostSystem;
    public float DaysSinceSystemAttacked;
    public float DaysSinceSystemLost;
    public float AttackResources;
    public float DefensiveResources;
    public int MonthlySystemsChanged;
    public int TotalSystemsChanged;

    //public List<string> PriorityList = new List<string>();
    public Dictionary<Faction, float> warFactionAttackResources = new Dictionary<Faction, float>();
    internal Dictionary<Faction, List<StarSystem>> attackTargets = new Dictionary<Faction, List<StarSystem>>();
    internal List<StarSystem> defenseTargets = new List<StarSystem>();

    //internal SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;

    [JsonConstructor]
    public WarFaction()
    {
        // deser ctor
    }


    public WarFaction(Faction faction)
    {
        LogDebug("WarFaction ctor");
        this.faction = faction;
        GainedSystem = false;
        LostSystem = false;
        DaysSinceSystemAttacked = 0;
        DaysSinceSystemLost = 0;
        MonthlySystemsChanged = 0;
        TotalSystemsChanged = 0;
    }
}

public class DeathListTracker
{
    public Faction faction;
    private FactionDef factionDef;
    public Dictionary<Faction, float> deathList = new Dictionary<Faction, float>();
    public List<Faction> AttackedBy = new List<Faction>();

    public List<Faction> Enemies => deathList.Where(x => x.Value >= 75).Select(x => x.Key).ToList();
    public List<Faction> Allies => deathList.Where(x => x.Value <= 25).Select(x => x.Key).ToList();

    // can't serialize these so make them private
    private SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;
    
    [JsonConstructor]
    public DeathListTracker()
    {
        // deser ctor
    }

    public DeathListTracker(Faction faction)
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