using System.Collections.Generic;
using System.Linq;
using BattleTech;
using Newtonsoft.Json;
using static GalaxyatWar.Helpers;
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global

namespace GalaxyatWar
{
    public class WarStatus
    {
        public List<SystemStatus> Systems = new();
        public List<DeathListTracker> DeathListTracker = new();
        public List<WarFaction> WarFactionTracker = new();
        public bool JustArrived = true;
        public bool Escalation = false;
        public bool Deployment = false;
        public WorkOrderEntry_Notification EscalationOrder;
        public int EscalationDays = 0;
        public List<string> PrioritySystems = new();
        public string CurSystem;
        public bool StartGameInitialized = false;
        public bool FirstTickInitialization = true;
        public List<string> SystemChangedOwners = new();
        public List<SystemStatus> HotBox = new();
        public List<string> LostSystems = new();
        public bool GaWEventPopUp = false;

        public List<string> HomeContestedStrings = new();
        public List<string> AbandonedSystems = new();
        public List<string> DeploymentContracts = new();
        public string DeploymentEmployer = "Marik";
        public double DeploymentInfluenceIncrease = 1.0;
        public bool PirateDeployment = false;

        public Dictionary<string, float> FullHomeContestedSystems = new();
        public List<string> HomeContestedSystems = new();
        public Dictionary<string, List<string>> ExternalPriorityTargets = new();
        public HashSet<string> FullPirateSystems = new();
        public List<string> PirateHighlight = new();
        public float PirateResources;
        public float TempPRGain;
        public float MinimumPirateResources;
        public float StartingPirateResources;
        public float LastPRGain;
        public List<string> HyadesRimGeneralPirateSystems = new();
        public int HyadesRimsSystemsTaken = 0;
        public List<string> InactiveTHRFactions = new();
        public List<string> FlashpointSystems = new();
        public List<string> NeverControl = new();
        public int ComstarCycle = 0;
        public string ComstarAlly = "";
        
        // 1) All systems are set with a base value based upon the tags.
        // 2) Each empire is totalled to see how many resources are within their sphere of influence.
        // 3) All factions have their total resources elevated to be identical to each other.
        // 4) Bonus resources are added to this pool.
        // 5) All resources added to the empire from #3 and #4 are averaged out over all the systems in their sphere of influence.
        public WarStatus()
        {
            Logger.LogDebug("WarStatus ctor");
            
            if (Globals.Settings.ISMCompatibility)
                Globals.Settings.IncludedFactions = new List<string>(Globals.Settings.IncludedFactions_ISM);

            CurSystem = Globals.Sim.CurSystem.Name;
            TempPRGain = 0;
            HotBox = new List<SystemStatus>();
            if (Globals.Settings.HyadesRimCompatible)
            {
                InactiveTHRFactions = Globals.Settings.HyadesAppearingPirates;
                FlashpointSystems = Globals.Settings.HyadesFlashpointSystems;
                NeverControl = Globals.Settings.HyadesNeverControl;
            }

            //initialize all WarFactions, DeathListTrackers, and SystemStatuses
            foreach (var faction in Globals.Settings.IncludedFactions)
            {
                var warFaction = new WarFaction(faction);
                var d = new DeathListTracker(faction)
                {
                    WarFaction = warFaction
                };
                warFaction.DeathListTracker = d;
                WarFactionTracker.Add(warFaction);
                DeathListTracker.Add(d);
            }

            foreach (var system in Globals.GaWSystems)
            {
                if (system.OwnerValue.Name == "NoFaction" || system.OwnerValue.Name == "AuriganPirates")
                    AbandonedSystems.Add(system.Name);
                var warFaction = WarFactionTracker.Find(x => x.FactionName == system.OwnerValue.Name);
                if (Globals.Settings.DefensiveFactions.Contains(warFaction.FactionName) && Globals.Settings.DefendersUseARforDR)
                    warFaction.DefensiveResources += GetTotalAttackResources(system);
                else
                    warFaction.AttackResources += GetTotalAttackResources(system);
                warFaction.DefensiveResources += GetTotalDefensiveResources(system);
            }

            Logger.LogDebug("WarFaction AR/DR set.");
            var maxAR = WarFactionTracker.Select(x => x.AttackResources).Max();
            var maxDR = WarFactionTracker.Select(x => x.DefensiveResources).Max();

            foreach (var faction in Globals.Settings.IncludedFactions)
            {
                var warFaction = WarFactionTracker.Find(x => x.FactionName == faction);
                if (Globals.Settings.DefensiveFactions.Contains(faction) && Globals.Settings.DefendersUseARforDR)
                {
                    if (!Globals.Settings.ISMCompatibility)
                        warFaction.DefensiveResources = maxAR + maxDR + Globals.Settings.BonusAttackResources[faction] +
                                                        Globals.Settings.BonusDefensiveResources[faction];
                    else
                        warFaction.DefensiveResources = maxAR + maxDR + Globals.Settings.BonusAttackResources_ISM[faction] +
                                                        Globals.Settings.BonusDefensiveResources_ISM[faction];

                    warFaction.AttackResources = 0;
                }
                else
                {
                    if (!Globals.Settings.ISMCompatibility)
                    {
                        warFaction.AttackResources = maxAR + Globals.Settings.BonusAttackResources[faction];
                        warFaction.DefensiveResources = maxDR + Globals.Settings.BonusDefensiveResources[faction];
                    }
                    else
                    {
                        warFaction.AttackResources = maxAR + Globals.Settings.BonusAttackResources_ISM[faction];
                        warFaction.DefensiveResources = maxDR + Globals.Settings.BonusDefensiveResources_ISM[faction];
                    }
                }
            }

            Logger.LogDebug("WarFaction bonus AR/DR set.");
            if (!Globals.Settings.ISMCompatibility)
                PirateResources = maxAR * Globals.Settings.FractionPirateResources + Globals.Settings.BonusPirateResources;
            else
                PirateResources = maxAR * Globals.Settings.FractionPirateResources_ISM + Globals.Settings.BonusPirateResources_ISM;

            MinimumPirateResources = PirateResources;
            StartingPirateResources = PirateResources;
            Logger.LogDebug("SystemStatus mass creation...");
            Systems = new List<SystemStatus>(Globals.GaWSystems.Count);
            for (var index = 0; index < Globals.Sim.StarSystems.Count; index++)
            {
                var system = Globals.Sim.StarSystems[index];
                if (Globals.Settings.ImmuneToWar.Contains(system.OwnerValue.Name))
                {
                    continue;
                }

                var systemStatus = new SystemStatus(system, system.OwnerValue.Name);
                Systems.Add(systemStatus);
                if (system.Tags.Contains("planet_other_pirate") && !system.Tags.Contains("planet_region_hyadesrim"))
                {
                    FullPirateSystems.Add(system.Name);
                    PiratesAndLocals.FullPirateListSystems.Add(systemStatus);
                }

                if (system.Tags.Contains("planet_region_hyadesrim") && !FlashpointSystems.Contains(system.Name)
                                                                    && (system.OwnerValue.Name == "NoFaction" || system.OwnerValue.Name == "Locals"))
                    HyadesRimGeneralPirateSystems.Add(system.Name);
            }

            Logger.LogDebug("Full pirate systems created.");
            Systems = Systems.OrderBy(x => x.Name).ToList();
            PrioritySystems = new List<string>(Systems.Count);
            Logger.LogDebug("SystemStatus ordered lists created.");
        }

        [JsonConstructor]
        public WarStatus(bool fake = false)
        {
        }
    }
}
