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
    internal SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;

    public static Dictionary<Faction, float> FindWarFactionResources(Faction faction) =>
        Core.WarStatus.warFactionTracker.Find(x => x.faction == faction).warFactionAttackResources;

    public WarStatus()
    {
        var sim = UnityGameInstance.BattleTechGame.Simulation;

        //initialize all WarFactions, DeathListTrackers, and SystemStatuses
        foreach (var faction in Core.Settings.IncludedFactions)
        {
            warFactionTracker.Add(new WarFaction(faction, Core.Settings.AttackResourceMap[faction], Core.Settings.DefensiveResourceMap[faction]));
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
        foreach (var system in sim.StarSystems)
        {
            systems.Add(new SystemStatus(sim, system.Name, system.Owner));
        }
    }
    [JsonConstructor]
    WarStatus (bool fake = false)
    {

    }
}
public class SystemStatus
{
    public string name;
    public Faction owner;

    internal Dictionary<Faction, int> neighborSystems = new Dictionary<Faction, int>();
    public Dictionary<Faction, float> influenceTracker = new Dictionary<Faction, float>();

    //internal WarFaction warFaction;
    internal StarSystem starSystem => sim.StarSystems.Find(s => s.Name == name);

    internal SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;
    [JsonConstructor
    ]
    public SystemStatus()
    {
        // don't want our ctor running at deserialization
    }

    public SystemStatus(SimGameState sim, string systemName, Faction faction)
    {
      //  LogDebug("SystemStatus ctor");
        name = systemName;
        owner = starSystem.Owner;
        owner = faction;
       // warFaction = Core.SystemStatus.warFactionTracker.Find(x => x.faction == owner);

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

        if (!(neighborSystems.Keys.Count() == 1 && neighborSystems.Keys.Contains(owner)))
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

    public void CalculateAttackTargets()
    {
        LogDebug("Calculate Potential Attack Targets");
        
        LogDebug("Can Attack:");
        if (starSystem == null)
        {
            LogDebug("PPPPPPPPPOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOO");
            return;
        }
        foreach (var neighborSystem in sim.Starmap.GetAvailableNeighborSystem(starSystem))
        {
            var warFac = Core.WarStatus.warFactionTracker.Find(x => x.faction == starSystem.Owner);
            if (warFac == null)
            {
                LogDebug("Didn't find warFaction for " + starSystem.Owner);
                return;
            }

            if (neighborSystem.Owner != starSystem.Owner && !warFac.attackTargets.ContainsKey(neighborSystem.Owner))
            {
                var tempList = new List<StarSystem> {neighborSystem};
                warFac.attackTargets.Add(neighborSystem.Owner, tempList);
                LogDebug("\t" + neighborSystem.Name + ": " + neighborSystem.Owner);
            }
            else if ((neighborSystem.Owner != starSystem.Owner) && warFac.attackTargets.ContainsKey(neighborSystem.Owner) &&
                     !warFac.attackTargets[neighborSystem.Owner].Contains(neighborSystem))
            {
                warFac.attackTargets[neighborSystem.Owner].Add(neighborSystem);
                LogDebug("\t" + neighborSystem.Name + ": " + neighborSystem.Owner);
            }
        }

        //using (var writer = new StreamWriter("Mods\\GalaxyAtWar\\" + SaveHandling.fileName))
        //    writer.Write(JsonConvert.SerializeObject(Globals.attackTargets));
    }

    public void CalculateDefenseTargets()
    {
        LogDebug("Calculate Potential Defendable Systems");
        LogDebug(starSystem.Name);
        LogDebug("Needs defense:");
        
        foreach (var neighborSystem in sim.Starmap.GetAvailableNeighborSystem(starSystem))
        {
            LogDebug("A");
            var warFac = Core.WarStatus.warFactionTracker.Find(x => x.faction == starSystem.Owner);
            LogDebug("B");
            if (warFac == null)
            {
                LogDebug("Didn't find warFaction for " + starSystem.Owner);
                return;
            }
            LogDebug("C");
            if ((neighborSystem.Owner != starSystem.Owner) && !warFac.defenseTargets.Contains(starSystem))
            {
                LogDebug("D");
                warFac.defenseTargets.Add(starSystem);
                LogDebug("\t" + starSystem.Name + ": " + starSystem.Owner);
            }
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

    public Dictionary<Faction, float> warFactionAttackResources = new Dictionary<Faction, float>();
    internal Dictionary<Faction, List<StarSystem>> attackTargets = new Dictionary<Faction, List<StarSystem>>();
    internal List<StarSystem> defenseTargets = new List<StarSystem>();

    //internal SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;

    public WarFaction()
    {
        // deser ctor
    }

    public WarFaction(Faction faction, float AttackResources, float DefensiveResources)
    {
        LogDebug("WarFaction ctor");
        this.faction = faction;
        this.AttackResources = AttackResources;
        this.DefensiveResources = DefensiveResources;
        GainedSystem = false;
        LostSystem = false;
        DaysSinceSystemAttacked = 0;
        DaysSinceSystemLost = 0;


        //foreach (var kvp in sim.FactionsDict)
        //{
        //    if (!Core.Settings.IncludedFactions.Contains(kvp.Key)) continue;
        //    if (kvp.Value == null) continue;
        //    if (Core.WarStatus.deathListTracker.All(x => x.faction != kvp.Key))
        //        Core.WarStatus.deathListTracker.Add(new DeathListTracker(kvp.Key));
        //}
    }
}

public class DeathListTracker
{
    public Faction faction;
    private FactionDef factionDef;
    public Dictionary<Faction, float> deathList = new Dictionary<Faction, float>();
    public List<Faction> AttackedBy = new List<Faction>();

    // can't serialize these so make them private
    private SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;

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