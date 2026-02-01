using EndfieldRecipe.Core.Domain;
using EndfieldRecipe.Core.Ports;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EndfieldRecipe.Infra;

public sealed class JsonAppRepository : IAppRepository {
    private readonly string _path;

    public JsonAppRepository(string path) {
        _path = path;
    }

    public AppData Load() {
        if (!File.Exists(_path)) return new AppData();

        try {
            var json = File.ReadAllText(_path);
            var root = JObject.Parse(json);

            var data = new AppData();

            var items = root["Items"]?.ToObject<Dictionary<string, Item>>() ?? new Dictionary<string, Item>();
            foreach (var kv in items) data.Items[kv.Key] = kv.Value;

            var machines = root["Machines"]?.ToObject<Dictionary<string, Machine>>() ?? new Dictionary<string, Machine>();
            foreach (var kv in machines) data.Machines[kv.Key] = kv.Value;

            var recipes = root["Recipes"]?.ToObject<List<Recipe>>() ?? new List<Recipe>();
            data.Recipes.AddRange(recipes);

            var needPlan = ParseNeedPlan(root);
            foreach (var target in needPlan.Targets) data.NeedPlan.Targets.Add(target);
            foreach (var kv in needPlan.RecipeChoiceByItem) data.NeedPlan.RecipeChoiceByItem[kv.Key] = kv.Value;

            return data;
        } catch {
            return new AppData();
        }
    }

    public void Save(AppData data) {
        var dto = new DataStoreDto {
            Items = data.Items,
            Machines = data.Machines,
            Recipes = data.Recipes,
            NeedPlan = new NeedPlanDto {
                Targets = data.NeedPlan.Targets
                    .Select(t => new NeedTargetDto { ItemKey = t.ItemKey, LineCount = t.LineCount })
                    .ToList(),
                RecipeChoiceByItem = new Dictionary<string, string>(data.NeedPlan.RecipeChoiceByItem)
            }
        };

        var json = JsonConvert.SerializeObject(dto, Formatting.Indented);
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

    private static NeedPlan ParseNeedPlan(JObject root) {
        var plan = new NeedPlan();

        var planToken = root["NeedPlan"];
        if (planToken != null) {
            var planDto = planToken.ToObject<NeedPlanDto>();
            if (planDto != null) {
                foreach (var target in planDto.Targets ?? new List<NeedTargetDto>()) {
                    if (string.IsNullOrWhiteSpace(target.ItemKey)) continue;
                    plan.Targets.Add(new NeedTarget(target.ItemKey, target.LineCount));
                }
                if (planDto.RecipeChoiceByItem != null) {
                    foreach (var kv in planDto.RecipeChoiceByItem) {
                        plan.RecipeChoiceByItem[kv.Key] = kv.Value;
                    }
                }
                return plan;
            }
        }

        var legacy = root["NeedDraft"]; // legacy List<ItemStack>
        if (legacy is JArray legacyArray) {
            foreach (var entry in legacyArray) {
                var key = entry["Key"]?.ToString();
                if (string.IsNullOrWhiteSpace(key)) continue;
                var amountToken = entry["Amount"];
                var lineCount = amountToken != null ? (int)Math.Max(1, amountToken.Value<decimal>()) : 1;
                plan.Targets.Add(new NeedTarget(key, lineCount));
            }
        }

        return plan;
    }

    private sealed class DataStoreDto {
        public Dictionary<string, Item> Items { get; set; } = new();
        public Dictionary<string, Machine> Machines { get; set; } = new();
        public List<Recipe> Recipes { get; set; } = new();
        public NeedPlanDto NeedPlan { get; set; } = new();
    }

    private sealed class NeedPlanDto {
        public List<NeedTargetDto> Targets { get; set; } = new();
        public Dictionary<string, string> RecipeChoiceByItem { get; set; } = new();
    }

    private sealed class NeedTargetDto {
        public string ItemKey { get; set; } = "";
        public int LineCount { get; set; } = 1;
    }
}
