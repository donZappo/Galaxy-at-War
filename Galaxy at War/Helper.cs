using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using BattleTech;
using Newtonsoft.Json;
using Harmony;
using System.Reflection;

namespace Galaxy_at_War
{
    class Helper
    {
        public static class Holder
        {
            public static bool Debug = false;
        }

        internal class ModSettings
        {
            public static ModSettings ReadSettings(string json)
            {
                try
                {
                    return JsonConvert.DeserializeObject<ModSettings>(json);
                }
                catch (Exception e)
                {
                    Logger.LogDebug($"Reading settings failed: {e.Message}");
                    return new ModSettings();
                }
            }
        }

        public static class Logger
        {
        private static string LogFilePath =>
        Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName +
        "\\Galaxy_at_War.log.txt";

            public static void Error(Exception ex)
            {
                using (var writer = new StreamWriter(LogFilePath, true))
                {
                    writer.WriteLine($"Message: {ex.Message}");
                    writer.WriteLine($"StackTrace: {ex.StackTrace}");
                }
            }

            public static void LogDebug(string line)
            {
                if (!Holder.Debug) return;
                using (var writer = new StreamWriter(LogFilePath, true))
                {
                    writer.WriteLine(line);
                }
            }

            public static void Log(string line)
            {
                using (var writer = new StreamWriter(LogFilePath, true))
                {
                    writer.WriteLine(line);
                }
            }
        }
    }
}
