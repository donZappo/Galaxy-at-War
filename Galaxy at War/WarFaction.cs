using System.Collections.Generic;
using Newtonsoft.Json;

namespace GalaxyatWar
{
    public class WarFaction
    {
        public string FactionName;
        public bool GainedSystem;
        public bool LostSystem;
        public float DaysSinceSystemAttacked;
        public float DaysSinceSystemLost;
        public int MonthlySystemsChanged;
        public int TotalSystemsChanged;
        public float PirateARLoss;
        public float PirateDRLoss;
        public float AR_Against_Pirates = 0;
        public float DR_Against_Pirates = 0;
        public bool ComstarSupported = false;
        public float AttackResources;
        public float TotalBonusAttackResources = 0;
        public float DefensiveResources;
        public float TotalBonusDefensiveResources = 0;
        public readonly Dictionary<string, float> WarFactionAttackResources = new();
        public readonly Dictionary<string, List<SystemStatus>> AttackTargets = new();
        public readonly List<SystemStatus> DefenseTargets = new();
        public readonly Dictionary<string, bool> IncreaseAggression = new();
        public readonly List<string> AdjacentFactions = new();
        private DeathListTracker deathListTracker;

        internal DeathListTracker DeathListTracker
        {
            get => deathListTracker ??= Globals.WarStatusTracker.DeathListTracker.Find(x => x.Faction == FactionName);
            set => deathListTracker = value;
        }

        [JsonConstructor]
        public WarFaction()
        {
            // deser ctor
        }

        public WarFaction(string factionName)
        {
            Logger.LogDebug("WarFaction ctor: " + factionName);
            FactionName = factionName;
            GainedSystem = false;
            LostSystem = false;
            DaysSinceSystemAttacked = 0;
            DaysSinceSystemLost = 0;
            MonthlySystemsChanged = 0;
            TotalSystemsChanged = 0;
            PirateARLoss = 0;
            PirateDRLoss = 0;
            foreach (var startFaction in Globals.IncludedFactions)
                IncreaseAggression.Add(startFaction, false);
        }
    }
}
