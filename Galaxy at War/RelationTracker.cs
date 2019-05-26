using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using BattleTech;
using Harmony;
using static Logger;

// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming

public class RelationTracker
{
    // we want to know how any given faction feels about another one
    // each faction will have X opinions
    // we want to be able to lookup a faction and get all its opinions back
    public List<FactionTracker> Factions = new List<FactionTracker>();

    public RelationTracker(SimGameState sim)
    {
        // we instantiate the tracker with all the factions, adding them to the list
        // set values based on enemy/neutral/ally FactionDef property

        foreach (var kvp in sim.FactionsDict)
        {
            if (Core.Settings.ExcludedFactions.Contains(kvp.Key)) continue;
            if (kvp.Value != null)
                Factions.Add(new FactionTracker(kvp.Key));
        }

        Log("FACTIONS");
        foreach (var faction in Factions)
        {
            foreach (var kvp in faction.killList)
            {
                Log($"{kvp.Key}, {kvp.Value}");
            }
        }
    }

    public class FactionTracker
    {
        private SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;
        public Faction faction;
        private FactionDef factionDef;
        public Dictionary<Faction, int> killList = new Dictionary<Faction, int>();

        public FactionTracker(Faction faction)
        {
            this.faction = faction;
            factionDef = sim.FactionsDict
                .Where(kvp => kvp.Key == faction)
                .Select(kvp => kvp.Value).First();

            // KillList init
            foreach (var factionDef in sim.FactionsDict.Values)
            {
                if (Core.Settings.ExcludedFactions.Contains(factionDef.Faction)) continue;
                if (factionDef.Enemies.Contains(factionDef.Faction))
                {
                    killList.Add(factionDef.Faction, 75);
                }
                else if (factionDef.Allies.Contains(factionDef.Faction))
                {
                    killList.Add(factionDef.Faction, 25);
                }
                else
                {
                    killList.Add(factionDef.Faction, 50);
                }
            }
        }
    }

    //public Dictionary<Faction, int> KillList = new Dictionary<Faction, int>
    //{
    //    {Faction.Liao, 0},
    //    {Faction.Steiner, 0},
    //    {Faction.Marik, 0},
    //    {Faction.Davion, 0},
    //    {Faction.Kurita, 0},
    //    {Faction.AuriganDirectorate, 0},
    //    {Faction.TaurianConcordat, 0},
    //    {Faction.MagistracyOfCanopus, 0},
    //    {Faction.NoFaction, 0},
    //    {Faction.Locals, 0},
    //    {Faction.AuriganRestoration, 0}
    //};
}

//public Dictionary<Faction, Dictionary<Faction, int>> RelationshipAttitudes = new Dictionary<Faction, Dictionary<Faction, int>>
//{
//    {Faction.Liao, RelationshipValues},
//    {Faction.Steiner, RelationshipValues},
//    {Faction.Marik, RelationshipValues},
//    {Faction.Davion, RelationshipValues},
//    {Faction.Kurita, RelationshipValues},
//    {Faction.AuriganDirectorate, RelationshipValues},
//    {Faction.TaurianConcordat, RelationshipValues},
//    {Faction.MagistracyOfCanopus, RelationshipValues},
//    {Faction.NoFaction, RelationshipValues},
//    {Faction.Locals, RelationshipValues},
//    {Faction.AuriganRestoration, RelationshipValues}
//};