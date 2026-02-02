using EndfieldRecipe.Core.Domain;
using EndfieldRecipe.Core.Ports;
using Newtonsoft.Json;

namespace EndfieldRecipe.Infra;

public sealed class JsonSettingsRepository : ISettingsRepository {
    private readonly string _path;

    public JsonSettingsRepository(string path) {
        _path = path;
    }

    public AppSettings Load() {
        var defaults = AppSettings.CreateDefault();
        if (!File.Exists(_path)) return defaults;

        try {
            var json = File.ReadAllText(_path);
            var settings = JsonConvert.DeserializeObject<AppSettings>(json);
            return MergeWithDefaults(settings, defaults);
        } catch {
            return defaults;
        }
    }

    public void Save(AppSettings settings) {
        var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, json);
        if (File.Exists(_path)) {
            try {
                File.Replace(tmp, _path, _path + ".bak", ignoreMetadataErrors: true);
            } catch {
                File.Delete(_path);
                File.Move(tmp, _path);
            }
        } else {
            File.Move(tmp, _path);
        }
    }

    private static AppSettings MergeWithDefaults(AppSettings? settings, AppSettings defaults) {
        if (settings == null) return defaults;

        if (settings.HistoryDepth <= 0) settings.HistoryDepth = defaults.HistoryDepth;
        if (string.IsNullOrWhiteSpace(settings.ItemSortMode)) settings.ItemSortMode = defaults.ItemSortMode;
        if (!IsValidSortMode(settings.ItemSortMode)) settings.ItemSortMode = defaults.ItemSortMode;

        settings.Keymap ??= new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (intent, keys) in defaults.Keymap) {
            if (!settings.Keymap.TryGetValue(intent, out var existing) || existing == null || existing.Count == 0) {
                settings.Keymap[intent] = new List<string>(keys);
            }
        }

        return settings;
    }

    private static bool IsValidSortMode(string mode) {
        return string.Equals(mode, "Value", StringComparison.OrdinalIgnoreCase)
               || string.Equals(mode, "Name", StringComparison.OrdinalIgnoreCase);
    }
}
