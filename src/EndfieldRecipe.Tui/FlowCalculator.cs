using EndfieldRecipe.Core.Domain;

namespace EndfieldRecipe.Tui;

public static class FlowCalculator {
    public static decimal GetFlowPerMin(AppData data, string itemKey, int lineCount) {
        var recipe = ChooseRecipe(data, itemKey);
        if (recipe == null) return 0m;
        var output = recipe.Outputs.FirstOrDefault(o => o.Key == itemKey);
        if (string.IsNullOrEmpty(output.Key) || output.Amount <= 0m) return 0m;
        if (recipe.Seconds <= 0) return 0m;
        var outPerMin = (60m / recipe.Seconds) * output.Amount;
        return lineCount * outPerMin;
    }

    public static string ResolveItemName(AppData data, string key) {
        return data.Items.TryGetValue(key, out var item) ? item.DisplayName : key;
    }

    public static string ResolveRecipeName(Recipe recipe) {
        return string.IsNullOrWhiteSpace(recipe.DisplayName) ? recipe.Id : recipe.DisplayName;
    }

    private static Recipe? ChooseRecipe(AppData data, string itemKey) {
        if (data.NeedPlan.RecipeChoiceByItem.TryGetValue(itemKey, out var recipeId)) {
            return data.Recipes.FirstOrDefault(r => r.Id == recipeId);
        }

        return data.Recipes.FirstOrDefault(r => r.Outputs.Any(o => o.Key == itemKey));
    }
}
