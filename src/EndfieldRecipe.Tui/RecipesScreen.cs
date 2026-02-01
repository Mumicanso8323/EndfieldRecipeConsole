using EndfieldRecipe.Core.Domain;

namespace EndfieldRecipe.Tui;

public sealed class RecipesScreen : IScreen {
    private int _selected;
    private int _offset;

    public string Title => UiText.RecipesTitle;

    public void Render(IRenderContext context, ScreenView view, ScreenContext screenContext) {
        var recipes = GetRecipes(screenContext);
        RenderList(context, recipes);
    }

    public ScreenResult Handle(Intent intent, ScreenContext context) {
        var recipes = GetRecipes(context);
        switch (intent.Kind) {
            case IntentKind.MoveUp:
                _selected = Math.Max(0, _selected - 1);
                break;
            case IntentKind.MoveDown:
                _selected = Math.Min(recipes.Count - 1, _selected + 1);
                break;
            case IntentKind.Back:
                return ScreenResult.Pop();
        }

        ClampOffset(context, recipes.Count);
        return ScreenResult.None();
    }

    private static List<Recipe> GetRecipes(ScreenContext context) {
        return context.Data.Recipes
            .OrderBy(r => string.IsNullOrWhiteSpace(r.DisplayName) ? r.Id : r.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void RenderList(IRenderContext context, List<Recipe> recipes) {
        if (recipes.Count == 0) {
            context.WriteBodyLine(0, UiText.StatusEmpty);
            return;
        }

        ClampOffset(context, recipes.Count);
        var max = Math.Min(recipes.Count, _offset + context.BodyHeight);
        var line = 0;
        for (var i = _offset; i < max; i++) {
            var recipe = recipes[i];
            var prefix = i == _selected ? "> " : "  ";
            var name = string.IsNullOrWhiteSpace(recipe.DisplayName) ? recipe.Id : recipe.DisplayName;
            var outputs = string.Join(", ", recipe.Outputs.Select(o => $"{ResolveItem(context, o.Key)}x{ItemStack.FormatAmount(o.Amount)}"));
            context.WriteBodyLine(line, $"{prefix}{name} ({outputs})");
            line++;
        }
    }

    private static string ResolveItem(ScreenContext context, string key) {
        return context.Data.Items.TryGetValue(key, out var item) ? item.DisplayName : key;
    }

    private void ClampOffset(IRenderContext context, int count) {
        if (_selected < _offset) _offset = _selected;
        if (_selected >= _offset + context.BodyHeight) {
            _offset = Math.Min(_selected, Math.Max(0, count - context.BodyHeight));
        }
    }
}
