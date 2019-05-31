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