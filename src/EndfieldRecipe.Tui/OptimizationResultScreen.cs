using EndfieldRecipe.Core.Application;

namespace EndfieldRecipe.Tui;

public sealed class OptimizationResultScreen : IScreen {
    private int _selected;
    private int _offset;

    public string Title => UiText.OptimizationResultTitle;

    public void Render(IRenderContext context, ScreenView view, ScreenContext screenContext) {
        var result = BuildResult(screenContext);
        if (result.Candidates.Count == 0) {
            context.WriteBodyLine(0, UiText.StatusEmpty);
            context.WriteBodyLine(1, UiText.LabelInvalidOptimization);
            return;
        }

        if (_selected >= result.Candidates.Count) _selected = Math.Max(0, result.Candidates.Count - 1);
        var lines = BuildLines(screenContext, result);

        ClampOffset(context, lines.Count);
        var max = Math.Min(lines.Count, _offset + context.BodyHeight);
        var line = 0;
        for (var i = _offset; i < max; i++) {
            context.WriteBodyLine(line, lines[i]);
            line++;
        }
    }

    public ScreenResult Handle(Intent intent, ScreenContext context) {
        var result = BuildResult(context);
        var count = result.Candidates.Count;
        var page = Math.Max(1, context.BodyHeight);

        switch (intent.Kind) {
            case IntentKind.MoveUp:
                _selected = Math.Max(0, _selected - 1);
                break;
            case IntentKind.MoveDown:
                _selected = Math.Min(Math.Max(0, count - 1), _selected + 1);
                break;
            case IntentKind.PageUp:
                _selected = Math.Max(0, _selected - page);
                break;
            case IntentKind.PageDown:
                _selected = Math.Min(Math.Max(0, count - 1), _selected + page);
                break;
            case IntentKind.JumpTop:
                _selected = 0;
                break;
            case IntentKind.JumpBottom:
                _selected = Math.Max(0, count - 1);
                break;
            case IntentKind.Back:
                return ScreenResult.Pop();
        }

        ClampOffset(context, BuildLines(context, result).Count);
        return ScreenResult.None();
    }

    private static OptimizationResult BuildResult(ScreenContext context) {
        var optimizer = new Optimizer();
        return optimizer.Solve(context.Data, context.Data.Optimization);
    }

    private List<string> BuildLines(ScreenContext context, OptimizationResult result) {
        var lines = new List<string> { UiText.LabelCandidates };
        for (var i = 0; i < result.Candidates.Count; i++) {
            var candidate = result.Candidates[i];
            var prefix = i == _selected ? "> " : "  ";
            var leftoverCount = candidate.InputLeftPerMin.Count(kv => kv.Value > 0m);
            lines.Add($"{prefix}{candidate.Name} {UiText.LabelValue}:{candidate.TotalOutputValuePerMin:G29} 余り:{leftoverCount}");
        }

        lines.Add(string.Empty);
        var selected = result.Candidates[_selected];
        lines.Add($"{UiText.LabelOutputs}({UiText.LabelValue}:{selected.TotalOutputValuePerMin:G29})");
        foreach (var output in selected.OutputProducedPerMin
                     .OrderByDescending(kv => ResolveItemValue(context, kv.Key))
                     .ThenByDescending(kv => kv.Value)) {
            lines.Add($"  {ResolveItemName(context, output.Key)}: {output.Value:G29}");
        }

        lines.Add(string.Empty);
        lines.Add(UiText.LabelInputsUsed);
        foreach (var input in selected.InputUsedPerMin.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)) {
            lines.Add($"  {ResolveItemName(context, input.Key)}: {input.Value:G29}");
        }

        lines.Add(string.Empty);
        lines.Add(UiText.LabelInputsLeft);
        foreach (var input in selected.InputLeftPerMin.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)) {
            lines.Add($"  {ResolveItemName(context, input.Key)}: {input.Value:G29}");
        }

        lines.Add(string.Empty);
        lines.Add(UiText.LabelAllocations);
        foreach (var allocation in selected.Allocations) {
            var inputs = BuildFlowSummary(context, allocation.InputFlows);
            var outputs = BuildFlowSummary(context, allocation.OutputFlows);
            lines.Add($"  {allocation.MachineName} x{allocation.MachineCount:G29} {UiText.LabelIn}:{inputs} {UiText.LabelOut}:{outputs}");
        }

        return lines;
    }

    private static string BuildFlowSummary(ScreenContext context, Dictionary<string, decimal> flows) {
        return string.Join(", ", flows.Select(kv => $"{ResolveItemName(context, kv.Key)}:{kv.Value:G29}"));
    }

    private static string ResolveItemName(ScreenContext context, string key) {
        if (context.Data.Items.TryGetValue(key, out var item)) {
            var baseName = string.IsNullOrWhiteSpace(item.DisplayName) ? item.Key : item.DisplayName;
            return context.Settings.HideInternalKeys ? baseName : $"{baseName} ({item.Key})";
        }
        return key;
    }

    private static int ResolveItemValue(ScreenContext context, string key) {
        return context.Data.Items.TryGetValue(key, out var item) ? item.Value : 0;
    }

    private void ClampOffset(IRenderContext context, int count) {
        if (_selected < _offset) _offset = _selected;
        if (_selected >= _offset + context.BodyHeight) {
            _offset = Math.Min(_selected, Math.Max(0, count - context.BodyHeight));
        }
    }
}
