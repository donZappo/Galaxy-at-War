using System;
using System.IO;
using System.Reflection;
using Harmony;
using UnityEngine;
using static GalaxyatWar.Globals;

namespace GalaxyatWar
{
    public static class Logger
    {
        private static string logFilePath = Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName + "/Galaxy-at-War.log";
        private static StreamWriter writer = new(logFilePath, true);

        public static async void Error(Exception ex)
        {
            await writer.WriteLineAsync($"{GetFormattedStartupTime()}  {ex}");
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
                await writer.WriteLineAsync($"{GetFormattedStartupTime()}  {line}");
            }
            catch (Exception ex)
            {
                FileLog.Log(ex.ToString());
            }
        }

        public static async void Log(string line)
        {
            await writer.WriteLineAsync(line);
        }

        public static async void Clear()
        {
            if (!Settings.Debug) return;
            await writer.WriteLineAsync($"{DateTime.Now.ToLongTimeString()} Galaxy-at-War Init");
        }
    }
}
