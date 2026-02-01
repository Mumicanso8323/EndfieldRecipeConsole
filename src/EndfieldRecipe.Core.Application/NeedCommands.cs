using EndfieldRecipe.Core.Domain;

namespace EndfieldRecipe.Core.Application;

public interface INeedCommand {
    void Do(NeedPlan plan);
    void Undo(NeedPlan plan);
}

public sealed class SetLineCountCommand : INeedCommand {
    private readonly string _itemKey;
    private readonly int _newLineCount;
    private int _oldLineCount;
    private bool _hasOld;

    public SetLineCountCommand(string itemKey, int newLineCount) {
        _itemKey = itemKey;
        _newLineCount = newLineCount;
    }

    public void Do(NeedPlan plan) {
        var target = plan.Targets.FirstOrDefault(t => t.ItemKey == _itemKey);
        if (target == null) return;
        _oldLineCount = target.LineCount;
        _hasOld = true;
        target.LineCount = _newLineCount;
    }

    public void Undo(NeedPlan plan) {
        if (!_hasOld) return;
        var target = plan.Targets.FirstOrDefault(t => t.ItemKey == _itemKey);
        if (target == null) return;
        target.LineCount = _oldLineCount;
    }
}

public sealed class AddTargetCommand : INeedCommand {
    private readonly string _itemKey;
    private readonly int _lineCount;
    private int _index = -1;

    public AddTargetCommand(string itemKey, int lineCount) {
        _itemKey = itemKey;
        _lineCount = lineCount;
    }

    public void Do(NeedPlan plan) {
        _index = plan.Targets.Count;
        plan.Targets.Add(new NeedTarget(_itemKey, _lineCount));
    }

    public void Undo(NeedPlan plan) {
        if (_index < 0 || _index >= plan.Targets.Count) return;
        plan.Targets.RemoveAt(_index);
    }
}

public sealed class RemoveTargetCommand : INeedCommand {
    private readonly string _itemKey;
    private NeedTarget? _removed;
    private int _index = -1;

    public RemoveTargetCommand(string itemKey) {
        _itemKey = itemKey;
    }

    public void Do(NeedPlan plan) {
        var index = plan.Targets.FindIndex(t => t.ItemKey == _itemKey);
        if (index < 0) return;
        _index = index;
        _removed = plan.Targets[index];
        plan.Targets.RemoveAt(index);
    }

    public void Undo(NeedPlan plan) {
        if (_removed == null || _index < 0) return;
        if (_index > plan.Targets.Count) _index = plan.Targets.Count;
        plan.Targets.Insert(_index, _removed);
    }
}

public sealed class SetRecipeChoiceCommand : INeedCommand {
    private readonly string _itemKey;
    private readonly string? _recipeId;
    private string? _previous;

    public SetRecipeChoiceCommand(string itemKey, string? recipeId) {
        _itemKey = itemKey;
        _recipeId = recipeId;
    }

    public void Do(NeedPlan plan) {
        plan.RecipeChoiceByItem.TryGetValue(_itemKey, out _previous);
        if (_recipeId == null) {
            plan.RecipeChoiceByItem.Remove(_itemKey);
        } else {
            plan.RecipeChoiceByItem[_itemKey] = _recipeId;
        }
    }

    public void Undo(NeedPlan plan) {
        if (_previous == null) {
            plan.RecipeChoiceByItem.Remove(_itemKey);
        } else {
            plan.RecipeChoiceByItem[_itemKey] = _previous;
        }
    }
}

public sealed class NeedHistory {
    private readonly int _maxDepth;
    private readonly Stack<INeedCommand> _undo = new();
    private readonly Stack<INeedCommand> _redo = new();

    public NeedHistory(int maxDepth = 3) {
        _maxDepth = maxDepth;
    }

    public void Execute(NeedPlan plan, INeedCommand command) {
        command.Do(plan);
        _undo.Push(command);
        Trim(_undo);
        _redo.Clear();
    }

    public bool Undo(NeedPlan plan) {
        if (_undo.Count == 0) return false;
        var command = _undo.Pop();
        command.Undo(plan);
        _redo.Push(command);
        Trim(_redo);
        return true;
    }

    public bool Redo(NeedPlan plan) {
        if (_redo.Count == 0) return false;
        var command = _redo.Pop();
        command.Do(plan);
        _undo.Push(command);
        Trim(_undo);
        return true;
    }

    private void Trim(Stack<INeedCommand> stack) {
        if (stack.Count <= _maxDepth) return;
        var items = stack.ToArray();
        stack.Clear();
        foreach (var item in items.Take(_maxDepth).Reverse()) {
            stack.Push(item);
        }
    }
}
