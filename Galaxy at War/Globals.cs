using System;
using System.Collections.Generic;
using System.Diagnostics;
using BattleTech;
using BattleTech.UI;
using TMPro;

// ReSharper disable ClassNeverInstantiated.Global

namespace GalaxyatWar
{
    public class Globals
    {
        internal static ModSettings Settings;
        public static WarStatus WarStatusTracker;
        internal static readonly Random Rng = new Random();
        internal static readonly Stopwatch T = new Stopwatch();
        internal static SimGameState Sim;
        public static string TeamFaction;
        public static string EnemyFaction;
        public static double Difficulty;
        public static MissionResult MissionResult;
        public static List<string> FactionEnemyHolder = new List<string>();
        public static string ContractType;
        public static bool BorkedSave;
        public static bool IsFlashpointContract;
        public static bool HoldContracts = false;
        public static double AttackerInfluenceHolder;
        public static bool InfluenceMaxed;
        internal static List<string> IncludedFactions;
        internal static List<string> OffensiveFactions;
        internal static List<FactionValue> FactionValues => FactionEnumeration.FactionList;
        internal const float SpendFactor = 5;
        internal static SimGameInterruptManager SimGameInterruptManager;
        internal static TaskTimelineWidget TaskTimelineWidget;
        internal static TMP_FontAsset Font;
        // todo don't remove any fields from this class or it will break deserialization. probably.
        public static bool IsGoodFaithEffort;
        public static Dictionary<string, List<StarSystem>> AttackTargets = new Dictionary<string, List<StarSystem>>();
        public static List<StarSystem> DefenseTargets = new List<StarSystem>();
        public static int LoopCounter;
        public static Contract LoopContract;
        public static bool FirstDehydrate = true;
        internal static bool ModInitialized;
        internal static List<StarSystem> GaWSystems = new List<StarSystem>();
        //extras for setting max values for the clamp
        public static float PAMaxValue = 100f;
        public static float ResourceGenericMax = 100000f;
    }
}
