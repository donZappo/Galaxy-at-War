using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BattleTech;
using Newtonsoft.Json;

namespace GalaxyatWar
{
    public class WarFaction
    {
        public readonly string FactionName;
        internal readonly FactionValue Faction;
        public bool GainedSystem;
        public bool LostSystem;
        public float DaysSinceSystemAttacked;
        public float DaysSinceSystemLost;
        private float attackResources;
        private float defensiveResources;
        public int MonthlySystemsChanged;
        public int TotalSystemsChanged;
        public float PirateARLoss;
        public float PirateDRLoss;
        public float AR_Against_Pirates = 0;
        public float DR_Against_Pirates = 0;
        public bool ComstarSupported = false;

        public float AttackResources
        {
            get => attackResources;
            set
            {
                if (value < -50000 || value > 50000)
                {
                    Logger.LogDebug($"{FactionName}: {value}");
                    Logger.LogDebug(new StackTrace().ToString());
                }

                attackResources = value;
            }
        }

        public float DefensiveResources
        {
            get => defensiveResources;
            set
            {
                if (value < -50000 || value > 50000)
                {
                    Logger.LogDebug($"{FactionName}: {value}");
                    Logger.LogDebug(new StackTrace().ToString());
                }

                defensiveResources = value;
            }
        }

        public Dictionary<string, float> warFactionAttackResources = new();
        public Dictionary<string, List<SystemStatus>> attackTargets = new();
        public List<SystemStatus> defenseTargets = new();
        public Dictionary<string, bool> IncreaseAggression = new();
        public List<string> adjacentFactions = new();
        private DeathListTracker deathListTracker;

        internal DeathListTracker DeathListTracker
        {
            get => deathListTracker ??= Globals.WarStatusTracker.deathListTracker.Find(x => x.faction == FactionName);
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
            Faction = Globals.FactionValues.Find(fv => fv.Name == factionName);
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
