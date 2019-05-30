using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using static Logger;

// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming

public class RelationTracker
{
    // we want to know how any given faction feels about another one
    public List<KillListTracker> factions = new List<KillListTracker>();

    public RelationTracker(SimGameState sim)
    {
        // we instantiate the tracker with all the factions, adding them to the list
        // set values based on enemy/neutral/ally FactionDef property
        try
        {
            foreach (var kvp in sim.FactionsDict)
            {
                if (Core.Settings.ExcludedFactions.Contains(kvp.Key)) continue;
                if (kvp.Value != null)
                    factions.Add(new KillListTracker(kvp.Key));
            }
        }
        catch (Exception ex)
        {
            Error(ex);
        }
    }
}

public class KillListTracker
{
    public Faction faction;
    public Dictionary<Faction, float> killList = new Dictionary<Faction, float>();
    public List<Faction> AttackedBy = new List<Faction>();

    // can't serialize these so make them private
    private SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;
    private FactionDef factionDef;

    public KillListTracker(Faction faction)
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
                killList.Add(def.Faction, Core.Settings.KLValuesEnemies);
            else if (def.Allies.Contains(faction))
                killList.Add(def.Faction, Core.Settings.KLValueAllies);
            else
                killList.Add(def.Faction, Core.Settings.KLValuesNeutral);
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

    public WarFaction(Faction faction, float resources, float DefensiveResources)
    {
        this.faction = faction;
        this.resources = resources;
        this.DefensiveResources = DefensiveResources;
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