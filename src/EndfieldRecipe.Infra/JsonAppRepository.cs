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
            foreach (var kv in items) {
                if (string.IsNullOrWhiteSpace(kv.Value.Key) || !string.Equals(kv.Value.Key, kv.Key, StringComparison.OrdinalIgnoreCase)) {
                    kv.Value.Key = kv.Key;
                }
                data.Items[kv.Key] = kv.Value;
            }

            var machines = root["Machines"]?.ToObject<Dictionary<string, Machine>>() ?? new Dictionary<string, Machine>();
            foreach (var kv in machines) {
                if (string.IsNullOrWhiteSpace(kv.Value.Key) || !string.Equals(kv.Value.Key, kv.Key, StringComparison.OrdinalIgnoreCase)) {
                    kv.Value.Key = kv.Key;
                }
                data.Machines[kv.Key] = kv.Value;
            }

            var recipes = root["Recipes"]?.ToObject<List<Recipe>>() ?? new List<Recipe>();
            data.Recipes.AddRange(recipes);

            var needPlan = ParseNeedPlan(root);
            foreach (var target in needPlan.Targets) data.NeedPlan.Targets.Add(target);

            var optimization = ParseOptimization(root);
            foreach (var supply in optimization.Supplies) data.Optimization.Supplies.Add(supply);

            MigrateMachinesFromRecipes(data);

            return data;
        } catch {
            return new AppData();
        }
    }

    public void Save(AppData data) {
        var dto = new DataStoreDto {
            Items = data.Items,
            Machines = data.Machines,
            Recipes = data.Recipes.Select(r => new RecipeDto {
                Id = r.Id,
                MachineKey = r.MachineKey,
                Inputs = r.Inputs,
                Outputs = r.Outputs
            }).ToList(),
            NeedPlan = new NeedPlanDto {
                Targets = data.NeedPlan.Targets
                    .Select(t => new NeedTargetDto { ItemKey = t.ItemKey, LineCount = t.LineCount })
                    .ToList(),
            },
            Optimization = new OptimizationDto {
                Supplies = data.Optimization.Supplies
                    .Select(s => new SupplyEntryDto { ItemKey = s.ItemKey, AmountPerMin = s.AmountPerMin })
                    .ToList()
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

    private static OptimizationPlan ParseOptimization(JObject root) {
        var plan = new OptimizationPlan();
        var token = root["Optimization"];
        if (token != null) {
            var dto = token.ToObject<OptimizationDto>();
            if (dto != null) {
                foreach (var supply in dto.Supplies ?? new List<SupplyEntryDto>()) {
                    if (string.IsNullOrWhiteSpace(supply.ItemKey)) continue;
                    plan.Supplies.Add(new SupplyEntry { ItemKey = supply.ItemKey, AmountPerMin = supply.AmountPerMin });
                }
            }
        }

        return plan;
    }

    private static void MigrateMachinesFromRecipes(AppData data) {
        var recipesByMachine = data.Recipes
            .GroupBy(r => r.MachineKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var machine in data.Machines.Values) {
            if (!recipesByMachine.TryGetValue(machine.Key, out var recipes)) continue;

            if (machine.Seconds <= 0) {
                var secondsCandidate = GetMode(recipes.Select(r => r.Seconds).Where(s => s > 0));
                if (secondsCandidate.HasValue) machine.Seconds = secondsCandidate.Value;
            }

            if (machine.InputTypeCount <= 0) {
                var inputCounts = recipes.Select(r => r.Inputs.Select(i => i.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
                var inputCandidate = GetMode(inputCounts.Where(c => c > 0));
                if (inputCandidate.HasValue) machine.InputTypeCount = inputCandidate.Value;
            }

            if (machine.OutputTypeCount <= 0) {
                var outputCounts = recipes.Select(r => r.Outputs.Select(o => o.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
                var outputCandidate = GetMode(outputCounts.Where(c => c > 0));
                if (outputCandidate.HasValue) machine.OutputTypeCount = outputCandidate.Value;
            }
        }
    }

    private static int? GetMode(IEnumerable<int> values) {
        var groups = values
            .GroupBy(v => v)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Value)
            .ToList();

        return groups.Count > 0 ? groups[0].Value : null;
    }

    private sealed class DataStoreDto {
        public Dictionary<string, Item> Items { get; set; } = new();
        public Dictionary<string, Machine> Machines { get; set; } = new();
        public List<RecipeDto> Recipes { get; set; } = new();
        public NeedPlanDto NeedPlan { get; set; } = new();
        public OptimizationDto Optimization { get; set; } = new();
    }

    private sealed class NeedPlanDto {
        public List<NeedTargetDto> Targets { get; set; } = new();
    }

    private sealed class NeedTargetDto {
        public string ItemKey { get; set; } = "";
        public int LineCount { get; set; } = 1;
    }

    private sealed class OptimizationDto {
        public List<SupplyEntryDto> Supplies { get; set; } = new();
    }

    private sealed class SupplyEntryDto {
        public string ItemKey { get; set; } = "";
        public decimal AmountPerMin { get; set; }
    }

    private sealed class RecipeDto {
        public string Id { get; set; } = "";
        public string MachineKey { get; set; } = "";
        public List<ItemStack> Inputs { get; set; } = new();
        public List<ItemStack> Outputs { get; set; } = new();
    }
}
