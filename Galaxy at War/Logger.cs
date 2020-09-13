using System;
using System.IO;
using System.Reflection;
using Harmony;
using static GalaxyatWar.Globals;

namespace GalaxyatWar
{
    public static class Logger
    {
        private static string logFilePath;

        private static string LogFilePath =>
            logFilePath ?? (logFilePath = Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName + "/Galaxy-at-War.log");

        public static void Error(Exception ex)
        {
            using (var writer = new StreamWriter(LogFilePath, true))
            {
                writer.WriteLine($"{ex}");
            }
        }

        public static async void LogDebug(object line)
        {
            try
            {
                if (!Settings.Debug) return;
                using (var writer = new StreamWriter(LogFilePath, true))
                {
                    await writer.WriteLineAsync(line.ToString());
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
