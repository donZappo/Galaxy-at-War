using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using Newtonsoft.Json;
using static Logger;

// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming

public class WarStatus
{
    public List<SystemStatus> systems = new List<SystemStatus>();

    public List<DeathListTracker> deathListTracker = new List<DeathListTracker>();

    //public static RelationTracker relationTracker = new RelationTracker(UnityGameInstance.BattleTechGame.Simulation);
    public List<WarFaction> warFactionTracker = new List<WarFaction>();
    internal SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;
    
    public static Dictionary<Faction, float> FindWarFactionResources(Faction faction) =>
        Core.WarStatus.warFactionTracker.Find(x => x.faction == faction).warFactionAttackResources;
}

public class SystemStatus
{
    public string name;
    public Dictionary<Faction, float> influenceTracker = new Dictionary<Faction, float>();
    public Faction owner = Faction.NoFaction;

    [JsonConstructor]
    public SystemStatus()
    {
        // don't want our ctor running at deserialization
    }

    public SystemStatus(SimGameState sim, string systemName)
    {
        Log($"new SystemStatus: {systemName}");
        name = systemName;
        owner = sim.StarSystems.First(s => s.Name == name).Owner;

        Globals.neighborSystems.Clear();

        //StaticMethods.CalculateNeighbours(sim, systemName);
        //StaticMethods.DistributeInfluence(influenceTracker, Globals.neighborSystems, owner, name);
        //StaticMethods.CalculateAttackTargets(sim, name);
        //StaticMethods.CalculateDefenseTargets(sim, name);
    }
}

public class DeathListTracker
{
    public Faction faction;
    public Dictionary<Faction, float> deathList = new Dictionary<Faction, float>();
    public List<Faction> AttackedBy = new List<Faction>();

    // can't serialize these so make them private
    private SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;
    private FactionDef factionDef;

    public DeathListTracker(Faction faction)
    {
        this.faction = faction;
        factionDef = sim.FactionsDict
            .Where(kvp => kvp.Key == faction)
            .Select(kvp => kvp.Value).First();

        foreach (var def in sim.FactionsDict.Values)
        {
            // necessary to skip factions here?  it does fire
            if (Core.Settings.ExcludedFactions.Contains(def.Faction))
                continue;
            if (def.Enemies.Contains(faction))
                deathList.Add(def.Faction, Core.Settings.KLValuesEnemies);
            else if (def.Allies.Contains(faction))
                deathList.Add(def.Faction, Core.Settings.KLValueAllies);
            else
                deathList.Add(def.Faction, Core.Settings.KLValuesNeutral);
        }
    }
}

public class WarFaction
{
    public float DaysSinceSystemAttacked;
    public float DaysSinceSystemLost;
    public float DefensiveResources;
    public Dictionary<Faction, float> warFactionAttackResources;
    public float resources;

    public Faction faction;

    //public List<DeathListTracker> deathListTracker = new List<DeathListTracker>();
    internal SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;

    public WarFaction(Faction faction, float resources, float DefensiveResources)
    {
        this.faction = faction;
        this.resources = resources;
        this.DefensiveResources = DefensiveResources;
        // get rid of a faction tracker DeathList for itself
        //var ownFactionListEntry = deathListTracker.Find(x => faction == x.faction);

        foreach (var kvp in sim.FactionsDict)
        {
            if (Core.Settings.ExcludedFactions.Contains(kvp.Key)) continue;
            if (kvp.Value == null) continue;
            if (Core.WarStatus.deathListTracker.All(x => x.faction != kvp.Key))
                Core.WarStatus.deathListTracker.Add(new DeathListTracker(kvp.Key));
        }
    }
}