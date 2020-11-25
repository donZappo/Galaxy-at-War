using System;
using System.IO;
using System.Reflection;
using Harmony;
using UnityEngine;
using static GalaxyatWar.Globals;

namespace GalaxyatWar
{
    public class Logger
    {
        private static string logFilePath;
        private static string logNamePath = "Galaxy-at-war.log";

        public Logger(string lnPath)
        {
            logNamePath = lnPath;
        }

        private static string LogFilePath =>
            logFilePath ?? (logFilePath = Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName + "/" + logNamePath);

        public static void Error(Exception ex)
        {
            using (var writer = new StreamWriter(LogFilePath, true))
            {
                writer.WriteLine($"{GetFormattedStartupTime()}  {ex}");
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

        public static async void LogDebug(object line)
        {
            try
            {
                if (!Settings.Debug) return;
                using (var writer = new StreamWriter(LogFilePath, true))
                {
                    await writer.WriteLineAsync($"{GetFormattedStartupTime()}  {line}");
                }
            }
            catch (Exception ex)
            {
                FileLog.Log(ex.ToString());
            }
        }

        public static void Log(string line)
        {
            using (var writer = new StreamWriter(LogFilePath, true))
            {
                writer.WriteLine(line);
            }
        }

        public static void Clear()
        {
            if (!Settings.Debug) return;
            using (var writer = new StreamWriter(LogFilePath, false))
            {
                writer.WriteLine($"{DateTime.Now.ToLongTimeString()} Galaxy-at-War Init");
            }
        }
    }
}
