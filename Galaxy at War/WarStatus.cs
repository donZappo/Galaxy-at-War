using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using Newtonsoft.Json;
using static Logger;

// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming

namespace GalaxyAtWar
{
    public class WarStatus
    {
        public List<SystemStatus> systems = new List<SystemStatus>();
        public static RelationTracker relationTracker = new RelationTracker(UnityGameInstance.BattleTechGame.Simulation);
        public List<WarFaction> factionTracker = new List<WarFaction>();
        public Dictionary<Faction, Dictionary<Faction, float>> attackResources = new Dictionary<Faction, Dictionary<Faction, float>>();
    }

    public class SystemStatus
    {
        public string name;
        public Dictionary<Faction, float> influenceTracker = new Dictionary<Faction, float>();
        public Faction owner = Faction.Unknown;

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
            try
            {
                StaticMethods.CalculateNeighbours(sim, systemName);
            }
            catch (Exception ex)
            {
                Error(ex);
            }

            StaticMethods.DistributeInfluence(influenceTracker, Globals.neighborSystems, owner, name);
            StaticMethods.CalculateAttackTargets(sim, name);
            StaticMethods.CalculateDefenseTargets(sim, name);
        }
    }
}