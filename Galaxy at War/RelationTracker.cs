using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using Newtonsoft.Json;
using static Logger;

// ReSharper disable CheckNamespace
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming

//public class RelationTracker
//{
//    // we want to know how any given faction feels about another one
//    public List<DeathListTracker> factions = new List<DeathListTracker>();
//
//    [JsonConstructor]
//    public RelationTracker()
//    {
//        // NO SOUP FOR YOU
//        LogDebug("Empty RelationTracker ctor");
//    }
//
//    public RelationTracker(SimGameState sim)
//    {
//        // we instantiate the tracker with all the factions, adding them to the list
//        // set values based on enemy/neutral/ally FactionDef property
//        // count systems because we're here during WarStatus initialized after WarStatus instantiation
//
//        LogDebug(">>> Constructing RelationTracker");
//        try
//        {
//            foreach (var kvp in sim.FactionsDict)
//            {
//                if (Core.Settings.ExcludedFactions.Contains(kvp.Key)) continue;
//                if (kvp.Value == null) continue;
//                if (factions.All(x => x.faction != kvp.Key))
//                    factions.Add(new DeathListTracker(kvp.Key));
//            }
//        }
//        catch (Exception ex)
//        {
//            Error(ex);
//        }
//    }
//}

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

//public class ResourceTacker
//{
//    public ResourceTacker(Faction faction, Dictionary<string, float> resources = null)
//    {
//        this.faction = faction;
//        this.resources = resources;
//    }
//
//    public Faction faction;
//    public Dictionary<string, float> resources;
//}