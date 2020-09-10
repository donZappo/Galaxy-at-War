using System;
using System.IO;
using System.Reflection;
   using static GalaxyatWar.Globals;
namespace GalaxyatWar
{
    public static class Logger
    {
        internal static string LogFilePath =>
            Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName + "/Galaxy-at-War.log";

        public static void Error(Exception ex)
        {
            using (var writer = new StreamWriter(LogFilePath, true))
            {
                writer.WriteLine($"{ex}");
            }
        }

        public static void LogDebug(object line)
        {
            if (!Settings.Debug) return;
            using (var writer = new StreamWriter(LogFilePath, true))
            {
                writer.WriteLine(line.ToString());
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