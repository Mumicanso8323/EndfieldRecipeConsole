using EndfieldRecipe.Core.Domain;

namespace EndfieldRecipe.Tui;

public sealed class RecipesScreen : IScreen, ITextEntryModeProvider {
    private int _selected;
    private int _offset;
    private bool _searchMode;
    private string _searchQuery = string.Empty;
    private string _status = string.Empty;
    private bool _deleteMode;

    public string Title => UiText.RecipesTitle;
    public bool PreferTextInput => _searchMode || _deleteMode;

    public void Render(IRenderContext context, ScreenView view, ScreenContext screenContext) {
        if (_searchMode) {
            view.Status = $"{UiText.LabelSearch}: {_searchQuery}";
        } else if (_deleteMode) {
            view.Status = UiText.LabelDeleteConfirm;
        } else if (!string.IsNullOrEmpty(_status)) {
            view.Status = _status;
            _status = string.Empty;
        }

        var recipes = GetRecipes(screenContext, _searchQuery);
        if (_selected >= recipes.Count) _selected = Math.Max(0, recipes.Count - 1);
        RenderList(context, screenContext, recipes);
    }

    public ScreenResult Handle(Intent intent, ScreenContext context) {
        if (_searchMode) {
            return HandleSearch(intent, context);
        }

        if (_deleteMode) {
            return HandleDeleteConfirm(intent, context);
        }

        var recipes = GetRecipes(context, _searchQuery);
        var page = Math.Max(1, context.BodyHeight);
        switch (intent.Kind) {
            case IntentKind.MoveUp:
                _selected = Math.Max(0, _selected - 1);
                break;
            case IntentKind.MoveDown:
                _selected = Math.Min(Math.Max(0, recipes.Count - 1), _selected + 1);
                break;
            case IntentKind.PageUp:
                _selected = Math.Max(0, _selected - page);
                break;
            case IntentKind.PageDown:
                _selected = Math.Min(Math.Max(0, recipes.Count - 1), _selected + page);
                break;
            case IntentKind.JumpTop:
                _selected = 0;
                break;
            case IntentKind.JumpBottom:
                _selected = Math.Max(0, recipes.Count - 1);
                break;
            case IntentKind.SearchStart:
                _searchMode = true;
                break;
            case IntentKind.Confirm:
                if (recipes.Count > 0) {
                    var recipe = recipes[_selected];
                    return ScreenResult.Push(new RecipeEditorScreen(recipe.Id));
                }
                break;
            case IntentKind.Add:
                return ScreenResult.Push(new RecipeEditorScreen(null));
            case IntentKind.Remove:
                if (recipes.Count > 0) _deleteMode = true;
                break;
            case IntentKind.TextInput:
                break;
            case IntentKind.Back:
                return ScreenResult.Pop();
        }

        ClampOffset(context, recipes.Count);
        return ScreenResult.None();
    }

    private ScreenResult HandleSearch(Intent intent, ScreenContext context) {
        switch (intent.Kind) {
            case IntentKind.TextInput:
                if (intent.Char.HasValue) {
                    _searchQuery += intent.Char.Value;
                }
                break;
            case IntentKind.DeleteChar:
                if (_searchQuery.Length > 0) {
                    _searchQuery = _searchQuery[..^1];
                }
                break;
            case IntentKind.Back:
                _searchQuery = string.Empty;
                _searchMode = false;
                break;
            case IntentKind.Confirm:
                _searchMode = false;
                break;
        }

        var recipes = GetRecipes(context, _searchQuery);
        if (_selected >= recipes.Count) _selected = Math.Max(0, recipes.Count - 1);
        ClampOffset(context, recipes.Count);
        return ScreenResult.None();
    }

    private ScreenResult HandleDeleteConfirm(Intent intent, ScreenContext context) {
        if (intent.Kind == IntentKind.TextInput && intent.Char is 'y' or 'Y') {
            var recipes = GetRecipes(context, _searchQuery);
            if (recipes.Count > 0) {
                var recipe = recipes[_selected];
                var target = context.Data.Recipes.FirstOrDefault(r => r.Id == recipe.Id);
                if (target != null) {
                    context.Data.Recipes.Remove(target);
                    context.SaveData();
                }
            }
            _deleteMode = false;
        } else if (intent.Kind == IntentKind.TextInput && intent.Char is 'n' or 'N') {
            _deleteMode = false;
        } else if (intent.Kind == IntentKind.Back) {
            _deleteMode = false;
        }

        return ScreenResult.None();
    }

    private static List<Recipe> GetRecipes(ScreenContext context, string query) {
        var recipes = context.Data.Recipes.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query)) {
            recipes = recipes.Where(r => MatchesQuery(context, r, query));
        }

        return recipes.ToList();
    }

    private static bool MatchesQuery(ScreenContext context, Recipe recipe, string query) {
        if (recipe.MachineKey.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
        var machineName = ResolveMachine(context, recipe.MachineKey);
        if (machineName.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;

        foreach (var input in recipe.Inputs) {
            if (input.Key.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
            if (ResolveItem(context, input.Key).Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
        }

        foreach (var output in recipe.Outputs) {
            if (output.Key.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
            if (ResolveItem(context, output.Key).Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    private void RenderList(IRenderContext context, ScreenContext screenContext, List<Recipe> recipes) {
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
            var inputs = string.Join(", ", recipe.Inputs.Select(i => $"{ResolveItem(screenContext, i.Key)}x{ItemStack.FormatAmount(i.Amount)}"));
            var outputs = string.Join(", ", recipe.Outputs.Select(o => $"{ResolveItem(screenContext, o.Key)}x{ItemStack.FormatAmount(o.Amount)}"));
            var machine = ResolveMachine(screenContext, recipe.MachineKey);
            context.WriteBodyLine(line, $"{prefix}{UiText.LabelMachine}:{machine} {UiText.LabelOut}:{outputs} {UiText.LabelIn}:{inputs}");
            line++;
        }
    }

    private static string ResolveItem(ScreenContext context, string key) {
        if (context.Data.Items.TryGetValue(key, out var item)) {
            var baseName = string.IsNullOrWhiteSpace(item.DisplayName) ? item.Key : item.DisplayName;
            return context.Settings.HideInternalKeys ? baseName : $"{baseName} ({item.Key})";
        }
        return key;
    }

    private static string ResolveMachine(ScreenContext context, string key) {
        if (context.Data.Machines.TryGetValue(key, out var machine)) {
            var name = string.IsNullOrWhiteSpace(machine.DisplayName) ? machine.Key : machine.DisplayName;
            return context.Settings.HideInternalKeys ? name : $"{name} ({machine.Key})";
        }
        return key;
    }

    private void ClampOffset(IRenderContext context, int count) {
        if (_selected < _offset) _offset = _selected;
        if (_selected >= _offset + context.BodyHeight) {
            _offset = Math.Min(_selected, Math.Max(0, count - context.BodyHeight));
        }
    }

}
