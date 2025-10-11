using Fun_Dub_Tool_Box.Utilities.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Fun_Dub_Tool_Box.Utilities
{
    public static class PresetRepository
    {
        private static readonly string PresetsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FunDubToolBox",
            "Presets");

        public static IReadOnlyList<string> GetPresetNames()
        {
            try
            {
                if (!Directory.Exists(PresetsDirectory))
                {
                    return Array.Empty<string>();
                }

                return Directory.GetFiles(PresetsDirectory, "*.json")
                                 .Select(Path.GetFileNameWithoutExtension)
                                 .Where(name => !string.IsNullOrWhiteSpace(name))
                                 .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                                 .ToList();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        public static bool TryLoadPreset(string name, out Preset preset)
        {
            preset = new Preset { Name = name };

            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            try
            {
                Directory.CreateDirectory(PresetsDirectory);
                var path = Path.Combine(PresetsDirectory, Sanitize(name) + ".json");
                if (!File.Exists(path))
                {
                    return false;
                }

                var json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<Preset>(json);
                if (loaded != null)
                {
                    loaded.Name = name;
                    preset = loaded;
                    return true;
                }
            }
            catch
            {
                // ignored; return false
            }

            return false;
        }

        public static string GetPresetPath(string name)
        {
            Directory.CreateDirectory(PresetsDirectory);
            return Path.Combine(PresetsDirectory, Sanitize(name) + ".json");
        }

        private static string Sanitize(string value)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(c, '_');
            }

            return value;
        }
    }
}