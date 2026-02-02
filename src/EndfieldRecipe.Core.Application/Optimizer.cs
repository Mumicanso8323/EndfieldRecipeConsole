using EndfieldRecipe.Core.Domain;

namespace EndfieldRecipe.Core.Application;

public sealed class Optimizer {
    public OptimizationResult Solve(AppData data, OptimizationPlan plan) {
        var supplies = plan.Supplies
            .Where(s => !string.IsNullOrWhiteSpace(s.ItemKey) && s.AmountPerMin > 0m)
            .GroupBy(s => s.ItemKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.AmountPerMin), StringComparer.OrdinalIgnoreCase);

        var feasible = BuildFeasibleRecipes(data, supplies);
        if (feasible.Count == 0) return new OptimizationResult();

        var candidates = new List<OptimizationCandidate>();
        var orderings = BuildOrderings(feasible, data);
        var index = 1;
        foreach (var ordering in orderings) {
            var candidate = BuildCandidate(data, supplies, ordering);
            if (candidate.Allocations.Count == 0) continue;
            candidate.Name = $"候補{index}";
            candidates.Add(candidate);
            index++;
        }

        var deduped = Deduplicate(candidates);
        for (var i = 0; i < deduped.Count; i++) {
            deduped[i].Name = $"候補{i + 1}";
        }
        return new OptimizationResult {
            Candidates = deduped
                .OrderByDescending(c => c.TotalOutputValuePerMin)
                .Take(5)
                .ToList()
        };
    }

    private static List<RecipeProfile> BuildFeasibleRecipes(AppData data, Dictionary<string, decimal> supplies) {
        var profiles = new List<RecipeProfile>();
        foreach (var recipe in data.Recipes) {
            if (!data.Machines.TryGetValue(recipe.MachineKey, out var machine)) continue;
            if (machine.Seconds <= 0) continue;
            if (recipe.Inputs.Any(i => !supplies.ContainsKey(i.Key))) continue;

            var runsPerMin = 60m / machine.Seconds;
            var inputFlows = recipe.Inputs
                .GroupBy(i => i.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => runsPerMin * g.Sum(x => x.Amount), StringComparer.OrdinalIgnoreCase);
            var outputFlows = recipe.Outputs
                .GroupBy(o => o.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => runsPerMin * g.Sum(x => x.Amount), StringComparer.OrdinalIgnoreCase);
            var outputValue = outputFlows.Sum(kv => ResolveItemValue(data, kv.Key) * kv.Value);

            profiles.Add(new RecipeProfile(recipe, machine, inputFlows, outputFlows, outputValue));
        }

        return profiles;
    }

    private static IEnumerable<List<RecipeProfile>> BuildOrderings(List<RecipeProfile> profiles, AppData data) {
        yield return profiles
            .OrderByDescending(p => p.OutputValuePerMin)
            .ToList();

        yield return profiles
            .OrderByDescending(p => p.OutputValuePerMin / Math.Max(1m, p.InputFlows.Values.Sum()))
            .ToList();

        yield return profiles
            .OrderByDescending(p => p.OutputValuePerMin / Math.Max(1m, p.InputFlows.Values.Max()))
            .ToList();

        yield return profiles
            .OrderByDescending(p => HighestOutputValue(data, p))
            .ThenByDescending(p => p.OutputValuePerMin)
            .ToList();
    }

    private static OptimizationCandidate BuildCandidate(
        AppData data,
        Dictionary<string, decimal> supplies,
        List<RecipeProfile> profiles
    ) {
        var remaining = supplies.ToDictionary(k => k.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);
        var allocations = new List<RecipeAllocation>();
        var inputUsed = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var outputProduced = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var profile in profiles) {
            var maxCount = GetMaxMachineCount(profile.InputFlows, remaining);
            if (maxCount <= 0m) continue;

            foreach (var input in profile.InputFlows) {
                remaining[input.Key] -= input.Value * maxCount;
                AddTo(inputUsed, input.Key, input.Value * maxCount);
            }

            foreach (var output in profile.OutputFlows) {
                AddTo(outputProduced, output.Key, output.Value * maxCount);
            }

            allocations.Add(new RecipeAllocation {
                MachineKey = profile.Recipe.MachineKey,
                RecipeId = profile.Recipe.Id,
                MachineName = ResolveMachineName(data, profile.Recipe.MachineKey),
                MachineCount = maxCount,
                InputFlows = new Dictionary<string, decimal>(profile.InputFlows),
                OutputFlows = new Dictionary<string, decimal>(profile.OutputFlows)
            });
        }

        var candidate = new OptimizationCandidate {
            Name = string.Empty,
            InputUsedPerMin = inputUsed,
            InputLeftPerMin = remaining,
            OutputProducedPerMin = outputProduced,
            Allocations = allocations
        };
        candidate.TotalOutputValuePerMin = outputProduced.Sum(kv => ResolveItemValue(data, kv.Key) * kv.Value);
        return candidate;
    }

    private static List<OptimizationCandidate> Deduplicate(List<OptimizationCandidate> candidates) {
        var grouped = candidates
            .GroupBy(c => string.Join("|", c.Allocations
                .OrderBy(a => a.MachineKey, StringComparer.OrdinalIgnoreCase)
                .ThenBy(a => a.MachineCount)
                .Select(a => $"{a.MachineKey}:{a.RecipeId}:{a.MachineCount:G29}")));

        return grouped
            .Select(g => g.OrderByDescending(c => c.TotalOutputValuePerMin).First())
            .ToList();
    }

    private static decimal GetMaxMachineCount(
        Dictionary<string, decimal> inputs,
        Dictionary<string, decimal> remaining
    ) {
        decimal max = decimal.MaxValue;
        foreach (var input in inputs) {
            if (!remaining.TryGetValue(input.Key, out var avail)) return 0m;
            if (input.Value <= 0m) return 0m;
            max = Math.Min(max, avail / input.Value);
        }

        return max == decimal.MaxValue ? 0m : max;
    }

    private static int ResolveItemValue(AppData data, string key) {
        return data.Items.TryGetValue(key, out var item) ? item.Value : 0;
    }

    private static string ResolveMachineName(AppData data, string key) {
        if (data.Machines.TryGetValue(key, out var machine)) {
            return string.IsNullOrWhiteSpace(machine.DisplayName) ? machine.Key : machine.DisplayName;
        }
        return key;
    }

    private static decimal HighestOutputValue(AppData data, RecipeProfile profile) {
        var values = profile.OutputFlows.Keys.Select(key => ResolveItemValue(data, key));
        return values.DefaultIfEmpty(0).Max();
    }

    private static void AddTo(Dictionary<string, decimal> map, string key, decimal value) {
        if (!map.TryGetValue(key, out var existing)) existing = 0m;
        map[key] = existing + value;
    }

    private sealed record RecipeProfile(
        Recipe Recipe,
        Machine Machine,
        Dictionary<string, decimal> InputFlows,
        Dictionary<string, decimal> OutputFlows,
        decimal OutputValuePerMin
    );
}

public sealed class OptimizationResult {
    public List<OptimizationCandidate> Candidates { get; set; } = new();
}

public sealed class OptimizationCandidate {
    public string Name { get; set; } = "";
    public decimal TotalOutputValuePerMin { get; set; }
    public Dictionary<string, decimal> InputUsedPerMin { get; set; } = new();
    public Dictionary<string, decimal> InputLeftPerMin { get; set; } = new();
    public Dictionary<string, decimal> OutputProducedPerMin { get; set; } = new();
    public List<RecipeAllocation> Allocations { get; set; } = new();
}

public sealed class RecipeAllocation {
    public string MachineKey { get; set; } = "";
    public string RecipeId { get; set; } = "";
    public string MachineName { get; set; } = "";
    public string InputsSummary { get; set; } = "";
    public string OutputsSummary { get; set; } = "";
    public decimal MachineCount { get; set; }
    public decimal ScoreContribution { get; set; }
    public Dictionary<string, decimal> InputFlows { get; set; } = new();
    public Dictionary<string, decimal> OutputFlows { get; set; } = new();
}
