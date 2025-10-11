using Fun_Dub_Tool_Box.Utilities.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Fun_Dub_Tool_Box.Utilities
{
    public static class QueueRepository
    {
        private static readonly string QueueDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FunDubToolBox",
            "Queue");

        private static readonly string QueueFilePath = Path.Combine(QueueDirectory, "queue.json");

        public static IReadOnlyList<RenderJob> Load()
        {
            try
            {
                if (!File.Exists(QueueFilePath))
                {
                    return [];
                }

                var json = File.ReadAllText(QueueFilePath);
                var data = JsonSerializer.Deserialize<List<RenderJob>>(json);
                return data;
            }
            catch
            {
                return [];
            }
        }

        public static void Save(IEnumerable<RenderJob> jobs)
        {
            Directory.CreateDirectory(QueueDirectory);
            var payload = jobs.ToList();
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(QueueFilePath, json);
        }

        public static int Count() => Load().Count;
    }
}