using System.Collections.Generic;
using BattleTech;

// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming

public class RelationTracker
{
    // we want to know how any given faction feels about another one
    // each faction will have X opinions
    // we want to be able to lookup a faction and get all its opinions back
    //public SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;
    public List<Dictionary<Faction, int>> Factions = new List<Dictionary<Faction, int>>();

    public RelationTracker()
    {
        // we instantiate the tracker with all the factions, adding them to the list
        foreach (var faction in UnityGameInstance.BattleTechGame.Simulation.FactionsDict.Keys)
        {
            Factions.Add(new Dictionary<Faction, int>(RelationshipValues));
        }
    }

    public Dictionary<Faction, int> RelationshipValues = new Dictionary<Faction, int>
    {
        {Faction.Liao, 0},
        {Faction.Steiner, 0},
        {Faction.Marik, 0},
        {Faction.Davion, 0},
        {Faction.Kurita, 0},
        {Faction.AuriganDirectorate, 0},
        {Faction.TaurianConcordat, 0},
        {Faction.MagistracyOfCanopus, 0},
        {Faction.NoFaction, 0},
        {Faction.Locals, 0},
        {Faction.AuriganRestoration, 0}
    };
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