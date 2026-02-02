using EndfieldRecipe.Core.Application;
using EndfieldRecipe.Core.Domain;

namespace EndfieldRecipe.Tui;

public sealed class NeedResultScreen : IScreen {
    private readonly HashSet<string> _collapsed = new();
    private readonly List<TreeLine> _treeLines = new();
    private int _selectedTreeIndex;
    private int _offset;
    private int _bodyHeight;
    private int _treeStartLine;

    public string Title => UiText.NeedResultTitle;

    public void Render(IRenderContext context, ScreenView view, ScreenContext screenContext) {
        var solver = new NeedSolver(screenContext.Data);
        var result = solver.Solve(screenContext.Data.NeedPlan);
        _treeLines.Clear();
        BuildTreeLines(screenContext, result.TreeRoots, string.Empty, 0);
        _bodyHeight = context.BodyHeight;
        if (_selectedTreeIndex >= _treeLines.Count) {
            _selectedTreeIndex = Math.Max(0, _treeLines.Count - 1);
        }

        var lines = BuildDisplayLines(screenContext, result);
        RenderLines(context, lines);
    }

    public ScreenResult Handle(Intent intent, ScreenContext context) {
        if (intent.Kind == IntentKind.Undo) {
            context.History.Undo(context.Data.NeedPlan);
            context.SaveData();
            return ScreenResult.None();
        }

        if (intent.Kind == IntentKind.Redo) {
            context.History.Redo(context.Data.NeedPlan);
            context.SaveData();
            return ScreenResult.None();
        }

        switch (intent.Kind) {
            case IntentKind.MoveUp:
                _selectedTreeIndex = Math.Max(0, _selectedTreeIndex - 1);
                break;
            case IntentKind.MoveDown:
                _selectedTreeIndex = Math.Min(Math.Max(0, _treeLines.Count - 1), _selectedTreeIndex + 1);
                break;
            case IntentKind.ToggleExpand:
                ToggleSelected();
                break;
            case IntentKind.Back:
                return ScreenResult.Pop();
            case IntentKind.JumpTop:
                _selectedTreeIndex = 0;
                break;
            case IntentKind.JumpBottom:
                _selectedTreeIndex = Math.Max(0, _treeLines.Count - 1);
                break;
        }

        ClampOffset();
        return ScreenResult.None();
    }

    private void RenderLines(IRenderContext context, List<string> lines) {
        if (lines.Count == 0) {
            context.WriteBodyLine(0, UiText.StatusEmpty);
            return;
        }

        ClampOffset();
        _offset = Math.Min(_offset, Math.Max(0, lines.Count - context.BodyHeight));
        var max = Math.Min(lines.Count, _offset + context.BodyHeight);
        var line = 0;
        for (var i = _offset; i < max; i++) {
            context.WriteBodyLine(line, lines[i]);
            line++;
        }
    }

    private List<string> BuildDisplayLines(ScreenContext context, NeedResult result) {
        var lines = new List<string> { UiText.LabelOut };
        foreach (var summary in result.OutSummary) {
            var name = ResolveItemName(context, summary.ItemKey);
            lines.Add($"  {name} | {summary.LineCount} | {summary.FlowPerMin:G29}");
        }

        lines.Add(string.Empty);
        lines.Add(UiText.LabelIn);
        if (result.InSummary.Count == 0) {
            lines.Add($"  {UiText.StatusEmpty}");
        } else {
            foreach (var summary in result.InSummary) {
                var name = ResolveItemName(context, summary.ItemKey);
                lines.Add($"  {name} | {summary.FlowPerMin:G29}");
            }
        }

        lines.Add(string.Empty);
        lines.Add(UiText.LabelTree);
        _treeStartLine = lines.Count;

        for (var i = 0; i < _treeLines.Count; i++) {
            var line = _treeLines[i];
            var prefix = i == _selectedTreeIndex ? ">" : " ";
            lines.Add($"{prefix} {line.Text}");
        }

        return lines;
    }

    private void BuildTreeLines(ScreenContext screenContext, List<RecipeNode> nodes, string path, int depth) {
        for (var i = 0; i < nodes.Count; i++) {
            var node = nodes[i];
            var nodeId = string.IsNullOrEmpty(path) ? i.ToString() : $"{path}.{i}";
            var indent = new string(' ', depth * 2);
            var marker = node.Children.Count > 0 ? (_collapsed.Contains(nodeId) ? "[+]" : "[-]") : "[ ]";
            var outputs = string.Join(", ", node.Outputs.Select(o => $"{ResolveItemName(screenContext, o.ItemKey)}:{o.FlowPerMin:G29}"));
            var inputs = string.Join(", ", node.Inputs.Select(i => $"{ResolveItemName(screenContext, i.ItemKey)}:{i.FlowPerMin:G29}"));
            var cyclic = node.IsCyclic ? UiText.LabelCyclic : string.Empty;
            var machineLabel = ResolveMachineName(screenContext, node.MachineType);
            var text = $"{indent}{marker} {machineLabel} x{node.MachineCount:G29} {UiText.LabelOut}:{outputs} {UiText.LabelIn}:{inputs}{cyclic}";
            _treeLines.Add(new TreeLine(nodeId, text, node.Children.Count > 0));

            if (node.Children.Count > 0 && !_collapsed.Contains(nodeId)) {
                BuildTreeLines(screenContext, node.Children, nodeId, depth + 1);
            }
        }
    }

    private void ToggleSelected() {
        if (_treeLines.Count == 0) return;
        var line = _treeLines[_selectedTreeIndex];
        if (!line.HasChildren) return;
        if (_collapsed.Contains(line.Id)) {
            _collapsed.Remove(line.Id);
        } else {
            _collapsed.Add(line.Id);
        }
    }

    private static string ResolveItemName(ScreenContext context, string key) {
        var name = FlowCalculator.ResolveItemName(context.Data, key);
        if (string.IsNullOrWhiteSpace(name)) name = key;
        if (!context.Settings.HideInternalKeys) {
            name = $"{name} ({key})";
        }
        return name;
    }

    private static string ResolveMachineName(ScreenContext context, string displayName) {
        if (context.Settings.HideInternalKeys) return displayName;
        var machine = context.Data.Machines.Values.FirstOrDefault(m =>
            string.Equals(m.DisplayName, displayName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(m.Key, displayName, StringComparison.OrdinalIgnoreCase));
        return machine == null ? displayName : $"{displayName} ({machine.Key})";
    }

    private void ClampOffset() {
        var selectedLine = _treeStartLine + _selectedTreeIndex;
        if (selectedLine < _offset) _offset = selectedLine;
        if (selectedLine >= _offset + _bodyHeight) {
            _offset = Math.Max(0, selectedLine - _bodyHeight + 1);
        }
    }

    private sealed record TreeLine(string Id, string Text, bool HasChildren);
}
