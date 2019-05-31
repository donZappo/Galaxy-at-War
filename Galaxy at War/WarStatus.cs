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
    public List<WarFaction> factionTracker = new List<WarFaction>();
    public Dictionary<Faction, Dictionary<Faction, float>> attackResources = new Dictionary<Faction, Dictionary<Faction, float>>();
    internal SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;
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