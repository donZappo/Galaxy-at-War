using System;
using System.IO;
using System.Reflection;
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
            var formatted = $"[{value.Hours:D2}:{value.Minutes:D2}:{value.Seconds:D2}.{value.Milliseconds:D3}]";
            return formatted;
        }
    }
}
