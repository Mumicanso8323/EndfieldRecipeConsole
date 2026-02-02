using EndfieldRecipe.Core.Domain;

namespace EndfieldRecipe.Core.Application;

public sealed class NeedSolver {
    private readonly AppData _data;
    private readonly Dictionary<string, List<Recipe>> _recipesByOutput;

    public NeedSolver(AppData data) {
        _data = data;
        _recipesByOutput = IndexRecipes(data);
    }

    public NeedResult Solve(NeedPlan plan) {
        var result = new NeedResult();
        var inSummary = new Dictionary<string, decimal>();

        foreach (var target in plan.Targets) {
            var recipe = ChooseRecipe(target.ItemKey, plan);
            if (recipe == null) {
                result.OutSummary.Add(new NeedOutSummary(target.ItemKey, target.LineCount, 0m));
                AddTo(inSummary, target.ItemKey, 0m);
                continue;
            }

            var outMain = recipe.Outputs.FirstOrDefault(o => o.Key == target.ItemKey);
            if (string.IsNullOrEmpty(outMain.Key) || outMain.Amount <= 0m) {
                result.OutSummary.Add(new NeedOutSummary(target.ItemKey, target.LineCount, 0m));
                continue;
            }

            var outPerMin = GetOutPerMin(recipe.MachineKey, outMain.Amount);
            var flow = target.LineCount * outPerMin;
            result.OutSummary.Add(new NeedOutSummary(target.ItemKey, target.LineCount, flow));

            var node = BuildNode(recipe, plan, target.ItemKey, target.LineCount, new HashSet<string>(), inSummary);
            if (node != null) {
                result.TreeRoots.Add(node);
            }
        }

        foreach (var kv in inSummary.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase)) {
            result.InSummary.Add(new NeedInSummary(kv.Key, kv.Value));
        }

        if (result.TreeRoots.Count > 0) {
            var merged = MergeNodes(result.TreeRoots);
            result.TreeRoots.Clear();
            result.TreeRoots.AddRange(merged);
        }

        return result;
    }

    private Recipe? ChooseRecipe(string itemKey, NeedPlan plan) {
        if (!_recipesByOutput.TryGetValue(itemKey, out var recipes)) return null;

        return recipes
            .Select(recipe => new {
                Recipe = recipe,
                Seconds = ResolveMachineSeconds(recipe.MachineKey),
                OutputAmount = recipe.Outputs.FirstOrDefault(o => o.Key == itemKey).Amount
            })
            .OrderByDescending(entry => entry.Seconds > 0)
            .ThenByDescending(entry => entry.Seconds > 0 && entry.OutputAmount > 0m
                ? (60m / entry.Seconds) * entry.OutputAmount
                : 0m)
            .ThenBy(entry => entry.Recipe.MachineKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Recipe.Id, StringComparer.OrdinalIgnoreCase)
            .Select(entry => entry.Recipe)
            .FirstOrDefault();
    }

    private RecipeNode? BuildNode(
        Recipe recipe,
        NeedPlan plan,
        string outputKey,
        decimal lineCount,
        HashSet<string> path,
        Dictionary<string, decimal> inSummary
    ) {
        if (path.Contains(outputKey)) {
            return new RecipeNode {
                RecipeId = recipe.Id,
                RecipeName = string.Empty,
                MachineType = ResolveMachineName(recipe.MachineKey),
                MachineCount = lineCount,
                RunsPerMin = lineCount * GetRunsPerMin(recipe.MachineKey),
                IsCyclic = true
            };
        }

        path.Add(outputKey);

        var node = new RecipeNode {
            RecipeId = recipe.Id,
            RecipeName = string.Empty,
            MachineType = ResolveMachineName(recipe.MachineKey),
            MachineCount = lineCount,
            RunsPerMin = lineCount * GetRunsPerMin(recipe.MachineKey)
        };

        var outPerRun = recipe.Outputs.FirstOrDefault(o => o.Key == outputKey).Amount;
        if (outPerRun <= 0m) {
            path.Remove(outputKey);
            return node;
        }

        var rateFactor = lineCount * GetRunsPerMin(recipe.MachineKey);
        if (rateFactor <= 0m) {
            path.Remove(outputKey);
            return node;
        }

        foreach (var output in recipe.Outputs) {
            node.Outputs.Add(new ItemFlow(output.Key, rateFactor * output.Amount));
        }

        foreach (var input in recipe.Inputs) {
            var flow = rateFactor * input.Amount;
            node.Inputs.Add(new ItemFlow(input.Key, flow));

            if (IsExternal(input.Key)) {
                AddTo(inSummary, input.Key, flow);
                continue;
            }

            var childRecipe = ChooseRecipe(input.Key, plan);
            if (childRecipe == null) {
                AddTo(inSummary, input.Key, flow);
                continue;
            }

            var childOutput = childRecipe.Outputs.FirstOrDefault(o => o.Key == input.Key);
            if (string.IsNullOrEmpty(childOutput.Key) || childOutput.Amount <= 0m) {
                AddTo(inSummary, input.Key, flow);
                continue;
            }

            var outPerMin = GetOutPerMin(childRecipe.MachineKey, childOutput.Amount);
            if (outPerMin <= 0m) {
                AddTo(inSummary, input.Key, flow);
                continue;
            }

            var childLineCount = flow / outPerMin;
            var childNode = BuildNode(childRecipe, plan, input.Key, childLineCount, new HashSet<string>(path), inSummary);
            if (childNode != null) {
                node.Children.Add(childNode);
            }
        }

        path.Remove(outputKey);
        return node;
    }

    private bool IsExternal(string itemKey) {
        if (_data.Items.TryGetValue(itemKey, out var item) && item.IsRawInput) {
            return true;
        }

        return !_recipesByOutput.ContainsKey(itemKey);
    }

    private decimal GetOutPerMin(string machineKey, decimal outQty) {
        var seconds = ResolveMachineSeconds(machineKey);
        if (seconds <= 0) return 0m;
        return (60m / seconds) * outQty;
    }

    private string ResolveMachineName(string machineKey) {
        if (_data.Machines.TryGetValue(machineKey, out var machine)) {
            return string.IsNullOrWhiteSpace(machine.DisplayName) ? machine.Key : machine.DisplayName;
        }
        return machineKey;
    }

    private static void AddTo(Dictionary<string, decimal> map, string key, decimal value) {
        if (!map.TryGetValue(key, out var v)) v = 0m;
        map[key] = v + value;
    }

    private int ResolveMachineSeconds(string machineKey) {
        if (_data.Machines.TryGetValue(machineKey, out var machine) && machine.Seconds > 0) {
            return machine.Seconds;
        }
        return 0;
    }

    private decimal GetRunsPerMin(string machineKey) {
        var seconds = ResolveMachineSeconds(machineKey);
        if (seconds <= 0) return 0m;
        return 60m / seconds;
    }

    private static Dictionary<string, List<Recipe>> IndexRecipes(AppData data) {
        var map = new Dictionary<string, List<Recipe>>();
        foreach (var recipe in data.Recipes) {
            foreach (var output in recipe.Outputs) {
                if (!map.TryGetValue(output.Key, out var list)) {
                    list = new List<Recipe>();
                    map[output.Key] = list;
                }
                list.Add(recipe);
            }
        }
        return map;
    }

    private static List<RecipeNode> MergeNodes(List<RecipeNode> nodes) {
        var grouped = nodes.GroupBy(n => new {
            n.RecipeId,
            n.MachineType
        });

        var merged = new List<RecipeNode>();
        foreach (var group in grouped) {
            var mergedNode = new RecipeNode {
                RecipeId = group.Key.RecipeId,
                RecipeName = string.Empty,
                MachineType = group.Key.MachineType,
                MachineCount = group.Sum(x => x.MachineCount),
                RunsPerMin = group.Sum(x => x.RunsPerMin),
                IsCyclic = group.Any(x => x.IsCyclic)
            };

            MergeFlows(mergedNode.Outputs, group.SelectMany(x => x.Outputs));
            MergeFlows(mergedNode.Inputs, group.SelectMany(x => x.Inputs));

            var children = group.SelectMany(x => x.Children).ToList();
            if (children.Count > 0) {
                var mergedChildren = MergeNodes(children);
                mergedNode.Children.AddRange(mergedChildren);
            }

            merged.Add(mergedNode);
        }

        return merged
            .OrderBy(n => n.MachineType, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(n => n.MachineCount)
            .ToList();
    }

    private static void MergeFlows(List<ItemFlow> target, IEnumerable<ItemFlow> flows) {
        var map = new Dictionary<string, decimal>();
        foreach (var flow in flows) {
            if (!map.TryGetValue(flow.ItemKey, out var v)) v = 0m;
            map[flow.ItemKey] = v + flow.FlowPerMin;
        }

        foreach (var kv in map.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase)) {
            target.Add(new ItemFlow(kv.Key, kv.Value));
        }
    }
}
