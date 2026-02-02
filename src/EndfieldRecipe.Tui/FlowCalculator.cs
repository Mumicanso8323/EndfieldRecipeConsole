using EndfieldRecipe.Core.Domain;

namespace EndfieldRecipe.Tui;

public static class FlowCalculator {
    public static decimal GetFlowPerMin(AppData data, string itemKey, int lineCount) {
        var recipe = ChooseRecipe(data, itemKey);
        if (recipe == null) return 0m;
        var output = recipe.Outputs.FirstOrDefault(o => o.Key == itemKey);
        if (string.IsNullOrEmpty(output.Key) || output.Amount <= 0m) return 0m;
        var seconds = ResolveMachineSeconds(data, recipe.MachineKey);
        if (seconds <= 0) return 0m;
        var outPerMin = (60m / seconds) * output.Amount;
        return lineCount * outPerMin;
    }

    public static string ResolveItemName(AppData data, string key) {
        return data.Items.TryGetValue(key, out var item) ? item.DisplayName : key;
    }

    private static Recipe? ChooseRecipe(AppData data, string itemKey) {
        return data.Recipes
            .Where(r => r.Outputs.Any(o => o.Key == itemKey))
            .Select(recipe => new {
                Recipe = recipe,
                Seconds = ResolveMachineSeconds(data, recipe.MachineKey),
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

    private static int ResolveMachineSeconds(AppData data, string machineKey) {
        if (data.Machines.TryGetValue(machineKey, out var machine) && machine.Seconds > 0) {
            return machine.Seconds;
        }
        return 0;
    }
}
