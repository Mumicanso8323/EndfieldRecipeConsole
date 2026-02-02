using EndfieldRecipe.Core.Domain;

namespace EndfieldRecipe.Core.Application;

public sealed class AppDiagnostics {
    public static List<Diagnostic> Analyze(AppData data) {
        var diagnostics = new List<Diagnostic>();

        diagnostics.AddRange(AnalyzeDuplicateRecipeIdentities(data));
        diagnostics.AddRange(AnalyzeSlotMismatches(data));
        diagnostics.AddRange(AnalyzeUnknownReferences(data));
        diagnostics.AddRange(AnalyzeMissingMachineDefinitions(data));

        return diagnostics;
    }

    private static IEnumerable<Diagnostic> AnalyzeDuplicateRecipeIdentities(AppData data) {
        var groups = data.Recipes
            .GroupBy(r => $"{r.MachineKey}|{CanonicalInputTypeSet(r)}", StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups) {
            if (group.Count() <= 1) continue;
            var recipe = group.First();
            var machineName = ResolveMachineName(data, recipe.MachineKey);
            var inputSummary = CanonicalInputTypeSetDisplay(data, recipe);
            yield return new Diagnostic(
                DiagnosticLevel.Warning,
                "DuplicateRecipeIdentity",
                $"同一マシン+同一入力(種類)のレシピが複数あります: マシン={machineName}, 入力={inputSummary}"
            );
        }
    }

    private static IEnumerable<Diagnostic> AnalyzeSlotMismatches(AppData data) {
        foreach (var recipe in data.Recipes) {
            if (!data.Machines.TryGetValue(recipe.MachineKey, out var machine)) continue;

            var inputTypes = recipe.Inputs.Select(i => i.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var outputTypes = recipe.Outputs.Select(o => o.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var summary = BuildRecipeSummary(data, recipe);

            if (machine.InputTypeCount > 0 && inputTypes != machine.InputTypeCount) {
                yield return new Diagnostic(
                    DiagnosticLevel.Warning,
                    "InputSlotMismatch",
                    $"入力スロット数が一致しません: マシン={ResolveMachineName(data, recipe.MachineKey)}, レシピ={summary}"
                );
            }

            if (machine.OutputTypeCount > 0 && outputTypes != machine.OutputTypeCount) {
                yield return new Diagnostic(
                    DiagnosticLevel.Warning,
                    "OutputSlotMismatch",
                    $"出力スロット数が一致しません: マシン={ResolveMachineName(data, recipe.MachineKey)}, レシピ={summary}"
                );
            }
        }
    }

    private static IEnumerable<Diagnostic> AnalyzeUnknownReferences(AppData data) {
        foreach (var recipe in data.Recipes) {
            if (!data.Machines.ContainsKey(recipe.MachineKey)) {
                yield return new Diagnostic(
                    DiagnosticLevel.Error,
                    "UnknownMachine",
                    $"未知のマシン参照があります: {recipe.MachineKey}"
                );
            }

            foreach (var input in recipe.Inputs) {
                if (!data.Items.ContainsKey(input.Key)) {
                    yield return new Diagnostic(
                        DiagnosticLevel.Error,
                        "UnknownItem",
                        $"未知のアイテム参照があります: {input.Key}"
                    );
                }
            }

            foreach (var output in recipe.Outputs) {
                if (!data.Items.ContainsKey(output.Key)) {
                    yield return new Diagnostic(
                        DiagnosticLevel.Error,
                        "UnknownItem",
                        $"未知のアイテム参照があります: {output.Key}"
                    );
                }
            }
        }

        foreach (var target in data.NeedPlan.Targets) {
            if (!data.Items.ContainsKey(target.ItemKey)) {
                yield return new Diagnostic(
                    DiagnosticLevel.Error,
                    "UnknownNeedTarget",
                    $"需要対象アイテムが見つかりません: {target.ItemKey}"
                );
            }
        }
    }

    private static IEnumerable<Diagnostic> AnalyzeMissingMachineDefinitions(AppData data) {
        foreach (var machine in data.Machines.Values) {
            if (machine.Seconds <= 0 || machine.InputTypeCount <= 0 || machine.OutputTypeCount <= 0) {
                yield return new Diagnostic(
                    DiagnosticLevel.Warning,
                    "MissingMachineDefinition",
                    $"マシン定義が不足しています: {ResolveMachineName(data, machine.Key)}"
                );
            }
        }
    }

    private static string CanonicalInputTypeSet(Recipe recipe) {
        return string.Join(
            ",",
            recipe.Inputs
                .Select(i => i.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
        );
    }

    private static string CanonicalInputTypeSetDisplay(AppData data, Recipe recipe) {
        return string.Join(
            ",",
            recipe.Inputs
                .Select(i => ResolveItemName(data, i.Key))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
        );
    }

    private static string BuildRecipeSummary(AppData data, Recipe recipe) {
        var inputs = string.Join(
            ",",
            recipe.Inputs.Select(i => $"{ResolveItemName(data, i.Key)}x{i.Amount:G29}")
        );
        var outputs = string.Join(
            ",",
            recipe.Outputs.Select(o => $"{ResolveItemName(data, o.Key)}x{o.Amount:G29}")
        );
        var machine = ResolveMachineName(data, recipe.MachineKey);
        return $"{machine} {inputs} -> {outputs}";
    }

    private static string ResolveMachineName(AppData data, string machineKey) {
        return data.Machines.TryGetValue(machineKey, out var machine)
            ? (string.IsNullOrWhiteSpace(machine.DisplayName) ? machine.Key : machine.DisplayName)
            : machineKey;
    }

    private static string ResolveItemName(AppData data, string itemKey) {
        return data.Items.TryGetValue(itemKey, out var item) ? item.DisplayName : itemKey;
    }
}

public enum DiagnosticLevel {
    Info,
    Warning,
    Error
}

public sealed record Diagnostic(DiagnosticLevel Level, string Code, string Message);
