using System;
using System.IO;
using System.Reflection;
using System.Text;
using Harmony;
using UnityEngine;
using static GalaxyatWar.Globals;

namespace GalaxyatWar
{
    public static class Logger
    {
        private static readonly string LogFilename = Directory.GetParent(Assembly.GetExecutingAssembly().Location)?.FullName + "/log.txt";

        internal static void Error(Exception ex)
        {
            using (var sw = new StreamWriter(LogFilename, true))
            {
                sw.AutoFlush = true;
                sw.WriteLine($"{GetFormattedStartupTime()}  {ex}");
            }
        }

        public static void LogDebug(object line)
        {
            if (!Settings.Debug) return;
            Log(line);
        }

        private static void Log(object line)
        {
            using (var sw = new StreamWriter(LogFilename, true))
            {
                sw.AutoFlush = true;
                sw.WriteLine($"{GetFormattedStartupTime()}  {line ?? "null value logged"}");
            }
        }

        internal static void Clear()
        {
            if (!Settings.Debug) return;
            File.Delete(LogFilename);
            using (var sw = new StreamWriter(LogFilename, false))
            {
                sw.AutoFlush = true;
                sw.WriteLine($"{DateTime.Now.ToLongTimeString()} Galaxy-at-War Init");
            }
        }

        // this beauty is from BetterLog from CptMoore's MechEngineer - thanks!
        // https://github.com/BattletechModders/MechEngineer/tree/master/source/Features/BetterLog
        private static string GetFormattedStartupTime()
        {
            var value = TimeSpan.FromSeconds(Time.realtimeSinceStartup);
            var formatted = string.Format(
                "[{0:D2}:{1:D2}:{2:D2}.{3:D3}]",
                value.Hours,
                value.Minutes,
                value.Seconds,
                value.Milliseconds);
            return formatted;
        }
    }
}
