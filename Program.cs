// Program.cs
// NuGet: Newtonsoft.Json
//
// dotnet new console -n EndfieldRecipeConsole
// dotnet add package Newtonsoft.Json
//
// Commands (examples):
//   item add        / item list / item edit / item del / item merge
//   machine add     / machine list / machine edit / machine del
//   recipe add      / recipe list / recipe view / recipe edit / recipe del
//   need show       / need add / need set / need del / need clear / need save <name> / need load <name> / need run
//   config show     / config maxpatterns 10 / config round 3 / config mode halfup / config detail all / config maxdepth 20
//   help / exit

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace EndfieldRecipeConsole {
    public static class Program {
        private const string DefaultDataFile = "data.json";

        public static void Main(string[] args) {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var dataPath = DefaultDataFile;
            var store = DataStore.Load(dataPath);

            while (true) {
                Console.Write(T.Prompt);
                var line = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = SplitArgs(line);
                if (parts.Count == 0) continue;

                var cmd = parts[0].ToLowerInvariant();
                var sub = parts.Count >= 2 ? parts[1].ToLowerInvariant() : "";
                var rest = parts.Skip(2).ToArray();

                try {
                    switch (cmd) {
                        case "help":
                        case "?":
                            PrintHelp();
                            break;

                        case "exit":
                        case "quit":
                        case "q":
                            return;

                        case "item":
                            RunItem(store, dataPath, sub, rest);
                            break;

                        case "items":
                            RunItem(store, dataPath, "list", rest);
                            break;

                        case "machine":
                            RunMachine(store, dataPath, sub, rest);
                            break;

                        case "machines":
                            RunMachine(store, dataPath, "list", rest);
                            break;

                        case "recipe":
                            RunRecipe(store, dataPath, sub, rest);
                            break;

                        case "recipes":
                            RunRecipe(store, dataPath, "list", rest);
                            break;

                        case "need":
                            RunNeed(store, dataPath, sub, rest);
                            break;

                        case "config":
                            RunConfig(store, dataPath, sub, rest);
                            break;

                        default:
                            Console.WriteLine(T.Unknown);
                            break;
                    }
                } catch (Exception ex) {
                    // 最小ログ方針。ただし致命的原因が分からないと困るので1行だけ。
                    Console.WriteLine($"{T.Error}: {ex.Message}");
                }
            }
        }

        private static List<string> SplitArgs(string line) {
            // 超簡易: スペース区切り（クォート未対応）
            return line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        private static void PrintHelp() {
            Console.WriteLine(T.Help);
        }

        // -------------------------
        // ITEM
        // -------------------------

        private static void RunItem(DataStore store, string dataPath, string sub, string[] args) {
            if (string.IsNullOrWhiteSpace(sub)) sub = "list";

            switch (sub) {
                case "add":
                    CmdItemAdd(store, dataPath);
                    break;
                case "list":
                    CmdItemList(store);
                    break;
                case "edit":
                    CmdItemEdit(store, dataPath, args);
                    break;
                case "del":
                case "delete":
                    CmdItemDel(store, dataPath, args);
                    break;
                case "merge":
                    CmdItemMerge(store, dataPath);
                    break;
                default:
                    Console.WriteLine("item: add | list | edit | del | merge");
                    break;
            }
        }

        private static void CmdItemAdd(DataStore store, string dataPath) {
            while (true) {
                Console.Write(T.ItemBlankExit);
                var raw = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(raw)) return;

                var key = ConsoleUI.NormalizeItemKey(raw.Trim());

                if (store.Items.ContainsKey(key)) {
                    Console.WriteLine(T.Exists);
                    continue;
                }

                var isRaw = ConsoleUI.InputBool(T.RawQ);
                Console.Write(T.DisplayNameQ);
                var dn = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(dn)) dn = raw.Trim();

                store.Items[key] = new Item {
                    Key = key,
                    DisplayName = dn!,
                    IsRawInput = isRaw,
                    CreatedAtUtc = DateTime.UtcNow
                };
                store.TouchUpdated();
                DataStore.SaveAtomic(store, dataPath);
            }
        }

        private static void CmdItemList(DataStore store) {
            var list = store.Items.Values
                .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (list.Count == 0) {
                Console.WriteLine(T.NoneItems);
                return;
            }

            for (int i = 0; i < list.Count; i++) {
                var it = list[i];
                Console.WriteLine($"{i + 1}: {it.DisplayName} ({it.Key}) raw={(it.IsRawInput ? "y" : "n")}");
            }
        }

        private static void CmdItemEdit(DataStore store, string dataPath, string[] args) {
            var (item, _) = ResolveItemByArgOrPick(store, args, prompt: "item edit: index/key (blank=cancel): ");
            if (item == null) return;

            while (true) {
                Console.WriteLine($"{T.ItemLabel}: {item.DisplayName} ({item.Key}) raw={(item.IsRawInput ? "y" : "n")}");
                Console.Write("edit (name/raw/blank=exit): ");
                var s = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(s)) break;

                s = s.Trim().ToLowerInvariant();
                if (s == "name") {
                    Console.Write(T.DisplayNameQ);
                    var dn = Console.ReadLine();
                    if (!string.IsNullOrWhiteSpace(dn))
                        item.DisplayName = dn.Trim();
                } else if (s == "raw") {
                    item.IsRawInput = ConsoleUI.InputBool(T.RawQ);
                }
            }

            store.TouchUpdated();
            DataStore.SaveAtomic(store, dataPath);
        }

        private static void CmdItemDel(DataStore store, string dataPath, string[] args) {
            var (item, key) = ResolveItemByArgOrPick(store, args, prompt: "item del: index/key (blank=cancel): ");
            if (item == null || key == null) return;

            var refCount = CountItemReferences(store, key);
            if (refCount > 0) {
                Console.WriteLine($"{T.DeleteBlocked} refs={refCount}");
                return;
            }

            // need draft / saved sets also remove lines referencing the item (or keep as dangling?)
            // ここは安全策：削除するなら参照も削除（保持したいなら拒否でも良い）
            store.NeedDraft = store.NeedDraft.Where(x => x.Key != key).ToList();
            foreach (var name in store.SavedNeedSets.Keys.ToList())
                store.SavedNeedSets[name] = store.SavedNeedSets[name].Where(x => x.Key != key).ToList();

            store.Items.Remove(key);
            store.TouchUpdated();
            DataStore.SaveAtomic(store, dataPath);
        }

        private static int CountItemReferences(DataStore store, string itemKey) {
            int n = 0;
            foreach (var r in store.Recipes) {
                n += r.Inputs.Count(x => x.Key == itemKey);
                n += r.Outputs.Count(x => x.Key == itemKey);
            }
            return n;
        }

        private static void CmdItemMerge(DataStore store, string dataPath) {
            var list = store.Items.Values
                .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (list.Count < 2) {
                Console.WriteLine(T.MergeNoItems);
                return;
            }

            for (int i = 0; i < list.Count; i++) {
                var it = list[i];
                Console.WriteLine($"{i + 1}: {it.DisplayName} ({it.Key}) raw={(it.IsRawInput ? "y" : "n")}");
            }

            Console.Write("merge from index (blank=cancel): ");
            var s1 = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(s1)) return;

            Console.Write("merge into index: ");
            var s2 = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(s2)) return;

            if (!int.TryParse(s1, out var a) || !int.TryParse(s2, out var b)) return;
            a--; b--;
            if (a < 0 || a >= list.Count || b < 0 || b >= list.Count || a == b) return;

            var src = list[a];
            var dst = list[b];

            // dst raw は安全側に倒す（OR）
            dst.IsRawInput = dst.IsRawInput || src.IsRawInput;

            foreach (var r in store.Recipes) {
                for (int i = 0; i < r.Inputs.Count; i++)
                    if (r.Inputs[i].Key == src.Key)
                        r.Inputs[i] = new ItemStack(dst.Key, r.Inputs[i].Amount);

                for (int i = 0; i < r.Outputs.Count; i++)
                    if (r.Outputs[i].Key == src.Key)
                        r.Outputs[i] = new ItemStack(dst.Key, r.Outputs[i].Amount);

                r.Inputs = ItemStack.CombineSameKeys(r.Inputs);
                r.Outputs = ItemStack.CombineSameKeys(r.Outputs);
            }

            // need draft / saved sets update
            store.NeedDraft = store.NeedDraft.Select(x => x.Key == src.Key ? new ItemStack(dst.Key, x.Amount) : x).ToList();
            store.NeedDraft = ItemStack.CombineSameKeys(store.NeedDraft);

            foreach (var name in store.SavedNeedSets.Keys.ToList()) {
                var v = store.SavedNeedSets[name].Select(x => x.Key == src.Key ? new ItemStack(dst.Key, x.Amount) : x).ToList();
                store.SavedNeedSets[name] = ItemStack.CombineSameKeys(v);
            }

            store.Items.Remove(src.Key);
            store.TouchUpdated();
            DataStore.SaveAtomic(store, dataPath);

            Console.WriteLine(T.Merged);
        }

        private static (Item? item, string? key) ResolveItemByArgOrPick(DataStore store, string[] args, string prompt) {
            if (store.Items.Count == 0) { Console.WriteLine(T.NoneItems); return (null, null); }

            // args[0] があればそれを使う
            if (args.Length >= 1) {
                var s = args[0].Trim();
                return ResolveItemByToken(store, s);
            }

            // 対話選択
            Console.Write(prompt);
            var tok = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(tok)) return (null, null);
            return ResolveItemByToken(store, tok.Trim());
        }

        private static (Item? item, string? key) ResolveItemByToken(DataStore store, string token) {
            // index?
            if (int.TryParse(token, out var idx)) {
                var list = store.Items.Values.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
                idx--;
                if (idx < 0 || idx >= list.Count) return (null, null);
                var it = list[idx];
                return (it, it.Key);
            }

            // normalize key if raw name
            var key = token.Contains(':') ? token : ConsoleUI.NormalizeItemKey(token);
            if (store.Items.TryGetValue(key, out var item))
                return (item, key);

            // fallback: display name search
            var hit = store.Items.Values.FirstOrDefault(x => x.DisplayName.Equals(token, StringComparison.OrdinalIgnoreCase));
            return hit != null ? (hit, hit.Key) : (null, null);
        }

        // -------------------------
        // MACHINE
        // -------------------------

        private static void RunMachine(DataStore store, string dataPath, string sub, string[] args) {
            if (string.IsNullOrWhiteSpace(sub)) sub = "list";

            switch (sub) {
                case "add":
                    CmdMachineAdd(store, dataPath);
                    break;
                case "list":
                    CmdMachineList(store);
                    break;
                case "edit":
                    CmdMachineEdit(store, dataPath, args);
                    break;
                case "del":
                case "delete":
                    CmdMachineDel(store, dataPath, args);
                    break;
                default:
                    Console.WriteLine("machine: add | list | edit | del");
                    break;
            }
        }

        private static void CmdMachineAdd(DataStore store, string dataPath) {
            while (true) {
                Console.Write(T.MachineBlankExit);
                var raw = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(raw)) return;

                var key = ConsoleUI.NormalizeMachineKey(raw.Trim());

                if (store.Machines.ContainsKey(key)) {
                    Console.WriteLine(T.Exists);
                    continue;
                }

                Console.Write(T.DisplayNameQ);
                var dn = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(dn)) dn = raw.Trim();

                store.Machines[key] = new Machine {
                    Key = key,
                    DisplayName = dn!,
                    CreatedAtUtc = DateTime.UtcNow
                };

                store.TouchUpdated();
                DataStore.SaveAtomic(store, dataPath);
            }
        }

        private static void CmdMachineList(DataStore store) {
            var list = store.Machines.Values
                .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (list.Count == 0) {
                Console.WriteLine(T.NoneMachines);
                return;
            }

            for (int i = 0; i < list.Count; i++) {
                var m = list[i];
                Console.WriteLine($"{i + 1}: {m.DisplayName} ({m.Key})");
            }
        }

        private static void CmdMachineEdit(DataStore store, string dataPath, string[] args) {
            var (m, key) = ResolveMachineByArgOrPick(store, args, "machine edit: index/key (blank=cancel): ");
            if (m == null || key == null) return;

            Console.WriteLine($"{T.MachineLabel}: {m.DisplayName} ({m.Key})");
            Console.Write(T.DisplayNameQ);
            var dn = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(dn))
                m.DisplayName = dn.Trim();

            store.TouchUpdated();
            DataStore.SaveAtomic(store, dataPath);
        }

        private static void CmdMachineDel(DataStore store, string dataPath, string[] args) {
            var (m, key) = ResolveMachineByArgOrPick(store, args, "machine del: index/key (blank=cancel): ");
            if (m == null || key == null) return;

            var refs = store.Recipes.Count(r => r.MachineKey == key);
            if (refs > 0) {
                Console.WriteLine($"{T.DeleteBlocked} refs={refs}");
                return;
            }

            store.Machines.Remove(key);
            store.TouchUpdated();
            DataStore.SaveAtomic(store, dataPath);
        }

        private static (Machine? m, string? key) ResolveMachineByArgOrPick(DataStore store, string[] args, string prompt) {
            if (store.Machines.Count == 0) { Console.WriteLine(T.NoneMachines); return (null, null); }

            if (args.Length >= 1) {
                return ResolveMachineByToken(store, args[0]);
            }

            Console.Write(prompt);
            var tok = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(tok)) return (null, null);
            return ResolveMachineByToken(store, tok.Trim());
        }

        private static (Machine? m, string? key) ResolveMachineByToken(DataStore store, string token) {
            if (int.TryParse(token, out var idx)) {
                var list = store.Machines.Values.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
                idx--;
                if (idx < 0 || idx >= list.Count) return (null, null);
                var m = list[idx];
                return (m, m.Key);
            }

            var key = token.Contains(':') ? token : ConsoleUI.NormalizeMachineKey(token);
            if (store.Machines.TryGetValue(key, out var m2))
                return (m2, key);

            var hit = store.Machines.Values.FirstOrDefault(x => x.DisplayName.Equals(token, StringComparison.OrdinalIgnoreCase));
            return hit != null ? (hit, hit.Key) : (null, null);
        }

        // -------------------------
        // RECIPE
        // -------------------------

        private static void RunRecipe(DataStore store, string dataPath, string sub, string[] args) {
            if (string.IsNullOrWhiteSpace(sub)) sub = "list";

            switch (sub) {
                case "add":
                    CmdRecipeAdd(store, dataPath);
                    break;
                case "list":
                    CmdRecipeList(store);
                    break;
                case "view":
                    CmdRecipeView(store, args);
                    break;
                case "edit":
                    CmdRecipeEdit(store, dataPath, args);
                    break;
                case "del":
                case "delete":
                    CmdRecipeDel(store, dataPath, args);
                    break;
                default:
                    Console.WriteLine("recipe: add | list | view | edit | del");
                    break;
            }
        }

        private static void CmdRecipeAdd(DataStore store, string dataPath) {
            while (true) {
                // Inputs: first prompt decides exit
                Console.Write(T.RecipeInFirst);
                var first = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(first)) return;

                var inputs = new List<ItemStack>();
                {
                    var k = ConsoleUI.ResolveOrCreateItemKeyFromRaw(store, first.Trim(), ItemInputMode.RecipeIO);
                    var amt = ConsoleUI.InputAmount(T.RecipeInAmt);
                    inputs.Add(new ItemStack(k, amt));
                }

                while (true) {
                    Console.Write(T.RecipeInNext);
                    var raw = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(raw)) break;

                    var k = ConsoleUI.ResolveOrCreateItemKeyFromRaw(store, raw.Trim(), ItemInputMode.RecipeIO);
                    var amt = ConsoleUI.InputAmount(T.RecipeInAmt);
                    inputs.Add(new ItemStack(k, amt));
                }

                // Outputs
                var outputs = new List<ItemStack>();
                while (true) {
                    Console.Write(T.RecipeOut);
                    var raw = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(raw)) {
                        if (outputs.Count == 0) return; // cancel
                        break;
                    }

                    var k = ConsoleUI.ResolveOrCreateItemKeyFromRaw(store, raw.Trim(), ItemInputMode.RecipeIO);
                    var amt = ConsoleUI.InputAmount(T.RecipeOutAmt);
                    outputs.Add(new ItemStack(k, amt));
                }

                // Machine
                var machineKey = ConsoleUI.InputMachineKey(store, required: true);

                // Seconds
                var seconds = ConsoleUI.InputInt(T.Seconds, min: 1, max: 3600 * 24);

                var recipe = new Recipe {
                    Id = Guid.NewGuid().ToString("N"),
                    Seconds = seconds,
                    MachineKey = machineKey!,
                    Inputs = ItemStack.CombineSameKeys(inputs),
                    Outputs = ItemStack.CombineSameKeys(outputs),
                    CreatedAtUtc = DateTime.UtcNow
                };

                store.Recipes.Add(recipe);
                store.TouchUpdated();
                DataStore.SaveAtomic(store, dataPath);
            }
        }

        private static void CmdRecipeList(DataStore store) {
            if (store.Recipes.Count == 0) {
                Console.WriteLine(T.NoneRecipes);
                return;
            }

            for (int i = 0; i < store.Recipes.Count; i++) {
                var r = store.Recipes[i];
                var mainOutName = r.Outputs.Count > 0 ? DisplayNameForItem(store, r.Outputs[0].Key) : "(no output)";
                var mName = DisplayNameForMachine(store, r.MachineKey);
                Console.WriteLine($"{i + 1}: {mainOutName} ({r.Seconds}s) machine={mName} in={r.Inputs.Count} out={r.Outputs.Count}");
            }
        }

        private static void CmdRecipeView(DataStore store, string[] args) {
            var r = ResolveRecipeByArgOrPick(store, args, "recipe view: index (blank=cancel): ");
            if (r == null) return;

            PrintRecipeDetail(store, r);
        }

        private static void CmdRecipeEdit(DataStore store, string dataPath, string[] args) {
            var r = ResolveRecipeByArgOrPick(store, args, "recipe edit: index (blank=cancel): ");
            if (r == null) return;

            while (true) {
                PrintRecipeDetail(store, r);
                Console.Write("edit (machine/seconds/inputs/outputs/blank=exit): ");
                var s = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(s)) break;

                s = s.Trim().ToLowerInvariant();
                if (s == "machine") {
                    var mk = ConsoleUI.InputMachineKey(store, required: true);
                    r.MachineKey = mk!;
                } else if (s == "seconds") {
                    r.Seconds = ConsoleUI.InputInt(T.Seconds, 1, 3600 * 24);
                } else if (s == "inputs") {
                    EditStacks(store, r.Inputs, isOutput: false);
                    r.Inputs = ItemStack.CombineSameKeys(r.Inputs);
                } else if (s == "outputs") {
                    EditStacks(store, r.Outputs, isOutput: true);
                    r.Outputs = ItemStack.CombineSameKeys(r.Outputs);
                }
            }

            store.TouchUpdated();
            DataStore.SaveAtomic(store, dataPath);
        }

        private static void EditStacks(DataStore store, List<ItemStack> stacks, bool isOutput) {
            while (true) {
                Console.WriteLine(isOutput ? "outputs:" : "inputs:");
                if (stacks.Count == 0) Console.WriteLine("(none)");
                for (int i = 0; i < stacks.Count; i++)
                    Console.WriteLine($"{i + 1}: {DisplayNameForItem(store, stacks[i].Key)} × {ItemStack.FormatAmount(stacks[i].Amount)}");

                Console.Write("cmd (add/set/del/blank=back): ");
                var s = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(s)) break;

                s = s.Trim().ToLowerInvariant();

                if (s == "add") {
                    Console.Write(isOutput ? "out item: " : "in item: ");
                    var raw = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(raw)) continue;

                    var key = ConsoleUI.ResolveOrCreateItemKeyFromRaw(store, raw.Trim(), ItemInputMode.RecipeIO);
                    var amt = ConsoleUI.InputAmount(isOutput ? T.RecipeOutAmt : T.RecipeInAmt);
                    stacks.Add(new ItemStack(key, amt));
                } else if (s == "set") {
                    Console.Write("index: ");
                    var idxS = Console.ReadLine();
                    if (!int.TryParse(idxS, out var idx)) continue;
                    idx--;
                    if (idx < 0 || idx >= stacks.Count) continue;

                    var amt = ConsoleUI.InputAmount("amount: ");
                    stacks[idx] = new ItemStack(stacks[idx].Key, amt);
                } else if (s == "del") {
                    Console.Write("index: ");
                    var idxS = Console.ReadLine();
                    if (!int.TryParse(idxS, out var idx)) continue;
                    idx--;
                    if (idx < 0 || idx >= stacks.Count) continue;
                    stacks.RemoveAt(idx);
                }
            }
        }

        private static void CmdRecipeDel(DataStore store, string dataPath, string[] args) {
            var r = ResolveRecipeByArgOrPick(store, args, "recipe del: index (blank=cancel): ");
            if (r == null) return;

            // need saved/draft doesn't store recipe refs; safe.
            store.Recipes.Remove(r);
            store.TouchUpdated();
            DataStore.SaveAtomic(store, dataPath);
        }

        private static Recipe? ResolveRecipeByArgOrPick(DataStore store, string[] args, string prompt) {
            if (store.Recipes.Count == 0) {
                Console.WriteLine(T.NoneRecipes);
                return null;
            }

            string token;
            if (args.Length >= 1)
                token = args[0].Trim();
            else {
                Console.Write(prompt);
                token = Console.ReadLine() ?? "";
            }

            if (string.IsNullOrWhiteSpace(token)) return null;

            if (!int.TryParse(token, out var idx)) return null;
            idx--;
            if (idx < 0 || idx >= store.Recipes.Count) return null;
            return store.Recipes[idx];
        }

        private static void PrintRecipeDetail(DataStore store, Recipe r) {
            Console.WriteLine($"id={r.Id}");
            Console.WriteLine($"machine={DisplayNameForMachine(store, r.MachineKey)} ({r.MachineKey})");
            Console.WriteLine($"seconds={r.Seconds}");
            Console.WriteLine("inputs:");
            foreach (var x in r.Inputs)
                Console.WriteLine($"- {ItemStack.FormatName(DisplayNameForItem(store, x.Key))}×{ItemStack.FormatAmount(x.Amount)}");
            Console.WriteLine("outputs:");
            foreach (var x in r.Outputs)
                Console.WriteLine($"- {ItemStack.FormatName(DisplayNameForItem(store, x.Key))}×{ItemStack.FormatAmount(x.Amount)}");
        }

        // -------------------------
        // NEED (draft + save/load + run + detailed report + tree)
        // -------------------------

        private static void RunNeed(DataStore store, string dataPath, string sub, string[] args) {
            if (string.IsNullOrWhiteSpace(sub)) sub = "show";

            switch (sub) {
                case "show":
                    CmdNeedShow(store);
                    break;
                case "add":
                    CmdNeedAdd(store, dataPath);
                    break;
                case "set":
                    CmdNeedSet(store, dataPath, args);
                    break;
                case "del":
                case "delete":
                    CmdNeedDel(store, dataPath, args);
                    break;
                case "clear":
                    store.NeedDraft = new List<ItemStack>();
                    store.TouchUpdated();
                    DataStore.SaveAtomic(store, dataPath);
                    break;
                case "save":
                    CmdNeedSave(store, dataPath, args);
                    break;
                case "load":
                    CmdNeedLoad(store, dataPath, args);
                    break;
                case "run":
                    CmdNeedRun(store, dataPath);
                    break;
                default:
                    Console.WriteLine("need: show | add | set | del | clear | save <name> | load <name> | run");
                    break;
            }
        }

        private static void CmdNeedShow(DataStore store) {
            if (store.NeedDraft.Count == 0) {
                Console.WriteLine(T.NeedDraftEmpty);
                return;
            }

            Console.WriteLine(T.NeedDraftLabel);
            var list = store.NeedDraft;
            for (int i = 0; i < list.Count; i++) {
                var x = list[i];
                Console.WriteLine($"{i + 1}: {ItemStack.FormatName(DisplayNameForItem(store, x.Key))}×{ItemStack.FormatAmount(x.Amount)}");
            }
        }

        private static void CmdNeedAdd(DataStore store, string dataPath) {
            while (true) {
                Console.Write(T.NeedItem);
                var raw = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(raw)) break;

                var key = ConsoleUI.ResolveOrCreateItemKeyFromRaw(store, raw.Trim(), ItemInputMode.NeedTarget);
                var amt = ConsoleUI.InputAmount(T.NeedAmt);
                store.NeedDraft.Add(new ItemStack(key, amt));
                store.NeedDraft = ItemStack.CombineSameKeys(store.NeedDraft);
                store.TouchUpdated();
                DataStore.SaveAtomic(store, dataPath);
            }
        }

        private static void CmdNeedSet(DataStore store, string dataPath, string[] args) {
            if (store.NeedDraft.Count == 0) { Console.WriteLine(T.NeedDraftEmpty); return; }

            int idx;
            if (args.Length >= 1 && int.TryParse(args[0], out var v)) idx = v;
            else {
                Console.Write("index: ");
                if (!int.TryParse(Console.ReadLine(), out idx)) return;
            }

            idx--;
            if (idx < 0 || idx >= store.NeedDraft.Count) return;

            var amt = ConsoleUI.InputAmount("amount: ");
            store.NeedDraft[idx] = new ItemStack(store.NeedDraft[idx].Key, amt);
            store.NeedDraft = ItemStack.CombineSameKeys(store.NeedDraft);

            store.TouchUpdated();
            DataStore.SaveAtomic(store, dataPath);
        }

        private static void CmdNeedDel(DataStore store, string dataPath, string[] args) {
            if (store.NeedDraft.Count == 0) { Console.WriteLine(T.NeedDraftEmpty); return; }

            int idx;
            if (args.Length >= 1 && int.TryParse(args[0], out var v)) idx = v;
            else {
                Console.Write("index: ");
                if (!int.TryParse(Console.ReadLine(), out idx)) return;
            }

            idx--;
            if (idx < 0 || idx >= store.NeedDraft.Count) return;

            store.NeedDraft.RemoveAt(idx);
            store.TouchUpdated();
            DataStore.SaveAtomic(store, dataPath);
        }

        private static void CmdNeedSave(DataStore store, string dataPath, string[] args) {
            if (args.Length < 1) {
                Console.Write("name: ");
                var nm = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(nm)) return;
                args = new[] { nm.Trim() };
            }

            var name = args[0].Trim();
            store.SavedNeedSets[name] = store.NeedDraft.ToList();
            store.TouchUpdated();
            DataStore.SaveAtomic(store, dataPath);
        }

        private static void CmdNeedLoad(DataStore store, string dataPath, string[] args) {
            if (store.SavedNeedSets.Count == 0) {
                Console.WriteLine(T.NeedNoSaved);
                return;
            }

            string name;
            if (args.Length >= 1) name = args[0].Trim();
            else {
                Console.Write("name: ");
                name = (Console.ReadLine() ?? "").Trim();
            }

            if (string.IsNullOrWhiteSpace(name)) return;

            if (!store.SavedNeedSets.TryGetValue(name, out var list)) {
                Console.WriteLine(T.NeedSavedNotFound);
                return;
            }

            store.NeedDraft = list.ToList();
            store.TouchUpdated();
            DataStore.SaveAtomic(store, dataPath);

            // 仕様：loadしても実行しない（編集可能な状態へ）
            CmdNeedShow(store);
        }

        private static void CmdNeedRun(DataStore store, string dataPath) {
            // cycle detect on need run
            var cycles = GraphAnalysis.FindCycles(store);
            if (cycles.Count > 0)
                Console.WriteLine($"{T.WarnCycles}{cycles.Count} (該当パスはブロックします)");

            if (store.NeedDraft.Count == 0) {
                Console.WriteLine(T.NeedDraftEmpty);
                return;
            }

            var demands = ItemStack.CombineSameKeys(store.NeedDraft);

            var engine = new NeedEngine(store);
            var solutions = engine.Solve(demands, store.Config.MaxPatterns);

            if (solutions.Count == 0) {
                Console.WriteLine(T.NoSolution);
                return;
            }

            // --- Filter: keep only solutions with minimum number of distinct machines used ---
            int CountMachines(NeedSolution sol) {
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in sol.Result.RecipeRuns) {
                    var recipeId = kv.Key;
                    var recipe = store.Recipes.FirstOrDefault(r => r.Id == recipeId);
                    if (recipe != null && !string.IsNullOrWhiteSpace(recipe.MachineKey))
                        set.Add(recipe.MachineKey);
                }
                return set.Count;
            }

            var minMachineCount = solutions.Min(CountMachines);

            // 「ヒット順」を維持したまま最少のみ残す
            solutions = solutions.Where(s => CountMachines(s) == minMachineCount).ToList();
            // --- end filter ---

            // 詳細表示
            for (int i = 0; i < solutions.Count; i++) {
                var sol = solutions[i];
                Console.WriteLine();
                Console.WriteLine($"========== PATTERN {i + 1} ==========");

                PrintNeedHeader(store, demands);

                if (store.Config.NeedDetailMode == NeedDetailMode.Summary || store.Config.NeedDetailMode == NeedDetailMode.All)
                    PrintNeedSummary(store, sol);

                if (store.Config.NeedDetailMode == NeedDetailMode.Full || store.Config.NeedDetailMode == NeedDetailMode.All) {
                    PrintNeedInputs(store, sol);
                    PrintNeedByproducts(store, sol);
                    PrintNeedRecipesUsed(store, sol);
                    PrintNeedItemBalance(store, sol);
                }

                if (store.Config.NeedDetailMode == NeedDetailMode.Tree || store.Config.NeedDetailMode == NeedDetailMode.All)
                    PrintNeedTree(store, sol, store.Config.NeedMaxDepth);
            }

            // need は基本データ変更しないが、アイテム新規追加が発生している可能性があるので保存しておく
            DataStore.SaveAtomic(store, dataPath);
        }

        private static void PrintNeedHeader(DataStore store, List<ItemStack> demands) {
            Console.WriteLine("[Need]");
            foreach (var d in demands)
                Console.WriteLine($"- {ItemStack.FormatName(DisplayNameForItem(store, d.Key))}×{ItemStack.FormatAmount(store.Config.ApplyRounding(d.Amount))}");
        }

        private static void PrintNeedSummary(DataStore store, NeedSolution sol) {
            var rawCount = sol.Result.Inputs.Count;
            var bypCount = sol.Result.Byproducts.Count;
            var recipeKinds = sol.Result.RecipeRuns.Count;

            var totalRuns = sol.Result.RecipeRuns.Values.Sum();
            var totalTime = sol.Result.RecipeRuns.Sum(kv => {
                var r = store.Recipes.FirstOrDefault(x => x.Id == kv.Key);
                return r == null ? 0m : kv.Value * r.Seconds;
            });

            Console.WriteLine();
            Console.WriteLine("[Summary]");
            Console.WriteLine($"rawInputs={rawCount}, byproducts={bypCount}, recipesUsed={recipeKinds}");
            Console.WriteLine($"totalRecipeRuns={ItemStack.FormatAmount(store.Config.ApplyRounding(totalRuns))}, totalTimeSec={ItemStack.FormatAmount(store.Config.ApplyRounding(totalTime))}");
        }

        private static void PrintNeedInputs(DataStore store, NeedSolution sol) {
            Console.WriteLine();
            Console.WriteLine(T.Inputs);

            foreach (var kv in sol.Result.Inputs.OrderBy(k => DisplayNameForItem(store, k.Key))) {
                var name = DisplayNameForItem(store, kv.Key);
                var shown = store.Config.ApplyRounding(kv.Value);
                Console.WriteLine($"{ItemStack.FormatName(name)}×{ItemStack.FormatAmount(shown)}");
            }
        }

        private static void PrintNeedByproducts(DataStore store, NeedSolution sol) {
            Console.WriteLine();
            Console.WriteLine(T.Byproducts);

            if (sol.Result.Byproducts.Count == 0) {
                Console.WriteLine("(none)");
                return;
            }

            foreach (var kv in sol.Result.Byproducts.OrderBy(k => DisplayNameForItem(store, k.Key))) {
                var name = DisplayNameForItem(store, kv.Key);
                var shown = store.Config.ApplyRounding(kv.Value);
                Console.WriteLine($"{ItemStack.FormatName(name)}×{ItemStack.FormatAmount(shown)}");
            }
        }

        private static void PrintNeedRecipesUsed(DataStore store, NeedSolution sol) {
            Console.WriteLine();
            Console.WriteLine(T.RecipesUsed);

            foreach (var kv in sol.Result.RecipeRuns
                         .OrderByDescending(x => x.Value)
                         .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)) {
                var recipeId = kv.Key;
                var runs = kv.Value;

                var recipe = store.Recipes.FirstOrDefault(r => r.Id == recipeId);
                if (recipe == null) {
                    Console.WriteLine($"- (missing recipe) id={recipeId} ×{ItemStack.FormatAmount(store.Config.ApplyRounding(runs))}");
                    continue;
                }

                var machine = DisplayNameForMachine(store, recipe.MachineKey);
                var timeTotal = runs * recipe.Seconds;

                // main output label
                var mainOut = recipe.Outputs.Count > 0 ? DisplayNameForItem(store, recipe.Outputs[0].Key) : "(no output)";

                Console.WriteLine($"- {mainOut} / machine={ItemStack.FormatName(machine)} / runs={ItemStack.FormatAmount(store.Config.ApplyRounding(runs))} / timeSec={ItemStack.FormatAmount(store.Config.ApplyRounding(timeTotal))}");

                // scaled IO
                Console.WriteLine("  inputs:");
                foreach (var inp in recipe.Inputs) {
                    var amt = inp.Amount * runs;
                    Console.WriteLine($"   - {ItemStack.FormatName(DisplayNameForItem(store, inp.Key))}×{ItemStack.FormatAmount(store.Config.ApplyRounding(amt))}");
                }

                Console.WriteLine("  outputs:");
                foreach (var o in recipe.Outputs) {
                    var amt = o.Amount * runs;
                    Console.WriteLine($"   - {ItemStack.FormatName(DisplayNameForItem(store, o.Key))}×{ItemStack.FormatAmount(store.Config.ApplyRounding(amt))}");
                }
            }
        }

        private static void PrintNeedItemBalance(DataStore store, NeedSolution sol) {
            Console.WriteLine();
            Console.WriteLine("[ItemBalance] produced/consumed/net");

            var keys = sol.Result.ItemBalance.Keys
                .Union(sol.Result.Inputs.Keys)
                .Union(sol.Result.Byproducts.Keys)
                .ToHashSet();

            if (keys.Count == 0) {
                Console.WriteLine("(none)");
                return;
            }

            var rows = new List<(string key, decimal produced, decimal consumed, decimal net, bool raw)>();

            foreach (var k in keys) {
                sol.Result.ItemBalance.TryGetValue(k, out var bal);
                var produced = bal.Produced;
                var consumed = bal.Consumed;

                // raw inputs are consumed too (already tracked) but ensure they appear
                var raw = store.Items.TryGetValue(k, out var it) && it.IsRawInput;

                rows.Add((k, produced, consumed, produced - consumed, raw));
            }

            foreach (var r in rows.OrderByDescending(x => Math.Abs(x.net)).ThenBy(x => DisplayNameForItem(store, x.key))) {
                var name = DisplayNameForItem(store, r.key);
                Console.WriteLine($"{ItemStack.FormatName(name)} raw={(r.raw ? "y" : "n")} produced={ItemStack.FormatAmount(store.Config.ApplyRounding(r.produced))} consumed={ItemStack.FormatAmount(store.Config.ApplyRounding(r.consumed))} net={ItemStack.FormatAmount(store.Config.ApplyRounding(r.net))}");
            }
        }

        private static void PrintNeedTree(DataStore store, NeedSolution sol, int maxDepth) {
            Console.WriteLine();
            Console.WriteLine("[Tree]");

            if (sol.Roots.Count == 0) {
                Console.WriteLine("(none)");
                return;
            }

            for (int i = 0; i < sol.Roots.Count; i++) {
                var root = sol.Roots[i];
                PrintNode(store, root, prefix: "", isLast: true, depth: 0, maxDepth: maxDepth);
            }
        }

        private static void PrintNode(DataStore store, NeedNode node, string prefix, bool isLast, int depth, int maxDepth) {
            var branch = depth == 0 ? "" : (isLast ? "└─ " : "├─ ");
            var nextPrefix = depth == 0 ? "" : (prefix + (isLast ? "   " : "│  "));

            if (depth >= maxDepth) {
                Console.WriteLine($"{prefix}{branch}...(maxDepth)");
                return;
            }

            var itemName = DisplayNameForItem(store, node.ItemKey);
            var req = store.Config.ApplyRounding(node.RequiredAmount);

            if (node.Kind == NeedNodeKind.Raw) {
                Console.WriteLine($"{prefix}{branch}{ItemStack.FormatName(itemName)}×{ItemStack.FormatAmount(req)} (RAW)");
                return;
            }

            // Recipe node
            var recipe = store.Recipes.FirstOrDefault(x => x.Id == node.RecipeId);
            var machineName = recipe != null ? DisplayNameForMachine(store, recipe.MachineKey) : "(missing machine)";
            var runs = store.Config.ApplyRounding(node.Runs);
            var time = recipe != null ? store.Config.ApplyRounding(node.Runs * recipe.Seconds) : 0m;

            Console.WriteLine($"{prefix}{branch}{ItemStack.FormatName(itemName)}×{ItemStack.FormatAmount(req)}");
            Console.WriteLine($"{nextPrefix}   (Recipe×{ItemStack.FormatAmount(runs)}) machine={ItemStack.FormatName(machineName)} timeSec={ItemStack.FormatAmount(time)}");

            if (node.Byproducts.Count > 0) {
                var bp = node.Byproducts
                    .OrderBy(kv => DisplayNameForItem(store, kv.Key))
                    .Select(kv => $"{ItemStack.FormatName(DisplayNameForItem(store, kv.Key))}×{ItemStack.FormatAmount(store.Config.ApplyRounding(kv.Value))}");
                Console.WriteLine($"{nextPrefix}   byp: {string.Join(", ", bp)}");
            }

            // children
            for (int i = 0; i < node.Children.Count; i++) {
                var c = node.Children[i];
                var last = i == node.Children.Count - 1;
                PrintNode(store, c, nextPrefix, last, depth + 1, maxDepth);
            }
        }

        // -------------------------
        // CONFIG
        // -------------------------

        private static void RunConfig(DataStore store, string dataPath, string sub, string[] args) {
            if (string.IsNullOrWhiteSpace(sub)) sub = "show";

            switch (sub) {
                case "show":
                    Console.WriteLine($"maxPatterns: {store.Config.MaxPatterns}");
                    Console.WriteLine($"roundDecimals: {store.Config.RoundDecimals}");
                    Console.WriteLine($"roundMode: {store.Config.RoundingMode}");
                    Console.WriteLine($"needDetail: {store.Config.NeedDetailMode}");
                    Console.WriteLine($"needMaxDepth: {store.Config.NeedMaxDepth}");
                    break;

                case "maxpatterns": {
                    var v = args.Length >= 1 ? ParseIntSafe(args[0], store.Config.MaxPatterns) : ConsoleUI.InputInt("maxPatterns: ", 1, 1000);
                    store.Config.MaxPatterns = Math.Clamp(v, 1, 1000);
                    store.TouchUpdated();
                    DataStore.SaveAtomic(store, dataPath);
                }
                break;

                case "round": {
                    var v = args.Length >= 1 ? ParseIntSafe(args[0], store.Config.RoundDecimals) : ConsoleUI.InputInt("roundDecimals: ", 0, 12);
                    store.Config.RoundDecimals = Math.Clamp(v, 0, 12);
                    store.TouchUpdated();
                    DataStore.SaveAtomic(store, dataPath);
                }
                break;

                case "mode": {
                    var m = args.Length >= 1 ? args[0] : null;
                    store.Config.RoundingMode = ConsoleUI.InputRoundingMode(m);
                    store.TouchUpdated();
                    DataStore.SaveAtomic(store, dataPath);
                }
                break;

                case "detail": {
                    var s = args.Length >= 1 ? args[0] : null;
                    store.Config.NeedDetailMode = ParseNeedDetailMode(s);
                    store.TouchUpdated();
                    DataStore.SaveAtomic(store, dataPath);
                }
                break;

                case "maxdepth": {
                    var v = args.Length >= 1 ? ParseIntSafe(args[0], store.Config.NeedMaxDepth) : ConsoleUI.InputInt("needMaxDepth: ", 1, 200);
                    store.Config.NeedMaxDepth = Math.Clamp(v, 1, 200);
                    store.TouchUpdated();
                    DataStore.SaveAtomic(store, dataPath);
                }
                break;

                default:
                    Console.WriteLine("config: show | maxpatterns <n> | round <decimals> | mode <none|halfup|up|down> | detail <summary|full|tree|all> | maxdepth <n>");
                    break;
            }
        }

        private static NeedDetailMode ParseNeedDetailMode(string? s) {
            if (string.IsNullOrWhiteSpace(s)) {
                Console.Write("needDetail (summary/full/tree/all): ");
                s = Console.ReadLine();
            }

            s = (s ?? "").Trim().ToLowerInvariant();
            return s switch {
                "summary" => NeedDetailMode.Summary,
                "full" => NeedDetailMode.Full,
                "tree" => NeedDetailMode.Tree,
                "all" => NeedDetailMode.All,
                _ => NeedDetailMode.All
            };
        }

        private static int ParseIntSafe(string s, int fallback)
            => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        // -------------------------
        // Display helpers
        // -------------------------

        private static string DisplayNameForItem(DataStore store, string itemKey)
            => store.Items.TryGetValue(itemKey, out var it) ? it.DisplayName : itemKey;

        private static string DisplayNameForMachine(DataStore store, string machineKey)
            => store.Machines.TryGetValue(machineKey, out var m) ? m.DisplayName : machineKey;
    }

    // -------------------------
    // Texts (JP)
    // -------------------------

    public static class T {
        public const string Prompt = "> ";
        public const string Error = "error";
        public const string Unknown = "不明なコマンドです。'help' を入力してください。";

        public const string Help =
            "Commands:\n" +
            "  item add|list|edit|del|merge\n" +
            "  machine add|list|edit|del\n" +
            "  recipe add|list|view|edit|del\n" +
            "  need show|add|set|del|clear|save <name>|load <name>|run\n" +
            "  config show|maxpatterns <n>|round <d>|mode <none|halfup|up|down>|detail <summary|full|tree|all>|maxdepth <n>\n" +
            "  help | exit";

        public const string Exists = "(既に存在します)";
        public const string DeleteBlocked = "削除できません（参照中）";
        public const string Merged = "(統合しました)";

        public const string NoneItems = "(アイテムなし)";
        public const string NoneMachines = "(マシンなし)";
        public const string NoneRecipes = "(レシピなし)";
        public const string MergeNoItems = "(統合できるアイテムがありません)";

        public const string ItemBlankExit = "アイテムID (空=終了): ";
        public const string MachineBlankExit = "マシンID (空=終了): ";
        public const string DisplayNameQ = "表示名 (空=そのまま): ";
        public const string RawQ = "入力可能アイテム？ (y/n): ";

        public const string RecipeInFirst = "入力アイテム (空=終了): ";
        public const string RecipeInNext = "入力アイテム (空=次へ): ";
        public const string RecipeInAmt = "入力個数: ";
        public const string RecipeOut = "出力アイテム (空=確定/キャンセル): ";
        public const string RecipeOutAmt = "出力個数: ";
        public const string RecipeMachine = "マシンID: ";
        public const string Seconds = "秒数: ";

        public const string NeedDraftLabel = "[NeedDraft]";
        public const string NeedDraftEmpty = "(NeedDraft なし)";
        public const string NeedItem = "欲しいアイテム (空=終了): ";
        public const string NeedAmt = "欲しい個数: ";
        public const string NeedNoSaved = "(保存済みNeedがありません)";
        public const string NeedSavedNotFound = "(指定名のNeedが見つかりません)";

        public const string WarnCycles = "[warn] 循環参照を検出しました: ";
        public const string NoSolution = "(解なし)";

        public const string Inputs = "必要入力:";
        public const string Byproducts = "副産物:";
        public const string RecipesUsed = "参照レシピ:";
        public const string ItemLabel = "Item";
        public const string MachineLabel = "Machine";
    }

    // -------------------------
    // Data models / persistence
    // -------------------------

    public sealed class DataStore {
        public int SchemaVersion { get; set; } = 2;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        // Key: "i:123" or "s:name"
        public Dictionary<string, Item> Items { get; set; } = new();

        // Key: "m:xxx"
        public Dictionary<string, Machine> Machines { get; set; } = new();

        public List<Recipe> Recipes { get; set; } = new();

        public AppConfig Config { get; set; } = new();

        // Need draft (editable, not auto-run)
        public List<ItemStack> NeedDraft { get; set; } = new();

        // Saved need sets by name
        public Dictionary<string, List<ItemStack>> SavedNeedSets { get; set; } = new();

        public void TouchUpdated() => UpdatedAtUtc = DateTime.UtcNow;

        public static DataStore Load(string path) {
            try {
                if (!File.Exists(path))
                    return new DataStore();

                var json = File.ReadAllText(path);
                var ds = JsonConvert.DeserializeObject<DataStore>(json) ?? new DataStore();

                ds.Items ??= new Dictionary<string, Item>();
                ds.Machines ??= new Dictionary<string, Machine>();
                ds.Recipes ??= new List<Recipe>();
                ds.Config ??= new AppConfig();
                ds.NeedDraft ??= new List<ItemStack>();
                ds.SavedNeedSets ??= new Dictionary<string, List<ItemStack>>();

                // Normalize possible null lists inside SavedNeedSets
                foreach (var k in ds.SavedNeedSets.Keys.ToList())
                    ds.SavedNeedSets[k] ??= new List<ItemStack>();

                return ds;
            } catch {
                return new DataStore();
            }
        }

        public static void SaveAtomic(DataStore store, string path) {
            store.TouchUpdated();

            var json = JsonConvert.SerializeObject(store, Formatting.Indented);
            var tmp = path + ".tmp";

            File.WriteAllText(tmp, json);

            if (File.Exists(path)) {
                try {
                    File.Replace(tmp, path, path + ".bak", ignoreMetadataErrors: true);
                } catch {
                    File.Delete(path);
                    File.Move(tmp, path);
                }
            } else {
                File.Move(tmp, path);
            }
        }
    }

    public sealed class AppConfig {
        public int MaxPatterns { get; set; } = 10;

        public int RoundDecimals { get; set; } = 3;
        public RoundingMode RoundingMode { get; set; } = RoundingMode.None;

        public NeedDetailMode NeedDetailMode { get; set; } = NeedDetailMode.All;
        public int NeedMaxDepth { get; set; } = 20;

        public decimal ApplyRounding(decimal value) {
            return RoundingMode switch {
                RoundingMode.None => value,
                RoundingMode.HalfUp => Math.Round(value, RoundDecimals, MidpointRounding.AwayFromZero),
                RoundingMode.Up => RoundUp(value, RoundDecimals),
                RoundingMode.Down => RoundDown(value, RoundDecimals),
                _ => value
            };
        }

        private static decimal RoundUp(decimal v, int decimals) {
            var factor = Pow10(decimals);
            return Math.Ceiling(v * factor) / factor;
        }

        private static decimal RoundDown(decimal v, int decimals) {
            var factor = Pow10(decimals);
            return Math.Floor(v * factor) / factor;
        }

        private static decimal Pow10(int n) {
            decimal r = 1m;
            for (int i = 0; i < n; i++) r *= 10m;
            return r;
        }
    }

    public enum NeedDetailMode {
        Summary,
        Full,
        Tree,
        All
    }

    public enum RoundingMode {
        None,
        HalfUp,
        Up,
        Down
    }

    public sealed class Item {
        public string Key { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public bool IsRawInput { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public sealed class Machine {
        public string Key { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public sealed class Recipe {
        public string Id { get; set; } = "";
        public int Seconds { get; set; } = 1;

        public string MachineKey { get; set; } = "";

        public List<ItemStack> Inputs { get; set; } = new();
        public List<ItemStack> Outputs { get; set; } = new();

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public readonly struct ItemStack {
        public string Key { get; }
        public decimal Amount { get; }

        public ItemStack(string key, decimal amount) {
            Key = key;
            Amount = amount;
        }

        public static string FormatName(string name) => $"[{name}]";

        public static string FormatAmount(decimal amt)
            => amt.ToString("G29", CultureInfo.InvariantCulture);

        public static List<ItemStack> CombineSameKeys(IEnumerable<ItemStack> stacks) {
            var map = new Dictionary<string, decimal>();
            foreach (var s in stacks) {
                if (!map.TryGetValue(s.Key, out var v)) v = 0m;
                map[s.Key] = v + s.Amount;
            }
            return map.Select(kv => new ItemStack(kv.Key, kv.Value)).ToList();
        }
    }

    // -------------------------
    // UI helpers
    // -------------------------

    public enum ItemInputMode {
        ItemOnly,
        RecipeIO,
        NeedTarget
    }

    public static class ConsoleUI {
        public static string ResolveOrCreateItemKeyFromRaw(DataStore store, string raw, ItemInputMode mode) {
            var key = NormalizeItemKey(raw);

            if (store.Items.TryGetValue(key, out var existing)) {
                if (mode == ItemInputMode.ItemOnly)
                    Console.WriteLine(T.Exists);
                return existing.Key;
            }

            // new item
            var isRaw = InputBool(T.RawQ);
            Console.Write(T.DisplayNameQ);
            var dn = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(dn)) dn = raw.Trim();

            store.Items[key] = new Item {
                Key = key,
                DisplayName = dn!,
                IsRawInput = isRaw,
                CreatedAtUtc = DateTime.UtcNow
            };
            store.TouchUpdated();
            return key;
        }

        public static string NormalizeItemKey(string raw) {
            raw = raw.Trim();
            if (raw.All(char.IsDigit))
                return "i:" + raw;
            return "s:" + raw;
        }

        public static string NormalizeMachineKey(string raw) {
            raw = raw.Trim();
            return raw.StartsWith("m:", StringComparison.OrdinalIgnoreCase) ? raw : "m:" + raw;
        }

        public static string? InputMachineKey(DataStore store, bool required) {
            while (true) {
                Console.Write(T.RecipeMachine);
                var raw = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(raw)) {
                    if (required) continue;
                    return null;
                }

                var key = NormalizeMachineKey(raw.Trim());

                if (store.Machines.TryGetValue(key, out _))
                    return key;

                // create new
                Console.Write(T.DisplayNameQ);
                var dn = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(dn)) dn = raw.Trim();

                store.Machines[key] = new Machine {
                    Key = key,
                    DisplayName = dn!,
                    CreatedAtUtc = DateTime.UtcNow
                };

                store.TouchUpdated();
                return key;
            }
        }

        public static decimal InputAmount(string prompt) {
            while (true) {
                Console.Write(prompt);
                var s = Console.ReadLine();
                if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) && v > 0m)
                    return v;
            }
        }

        public static int InputInt(string prompt, int min, int max) {
            while (true) {
                Console.Write(prompt);
                var s = Console.ReadLine();
                if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) {
                    if (v >= min && v <= max) return v;
                }
            }
        }

        public static bool InputBool(string prompt) {
            while (true) {
                Console.Write(prompt);
                var s = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(s)) continue;

                s = s.Trim().ToLowerInvariant();
                if (s is "y" or "yes" or "true" or "1") return true;
                if (s is "n" or "no" or "false" or "0") return false;
            }
        }

        public static RoundingMode InputRoundingMode(string? maybe) {
            while (true) {
                var s = maybe;
                if (string.IsNullOrWhiteSpace(s)) {
                    Console.Write("mode (none/halfup/up/down): ");
                    s = Console.ReadLine();
                }

                if (string.IsNullOrWhiteSpace(s)) {
                    maybe = null;
                    continue;
                }

                s = s.Trim().ToLowerInvariant();
                return s switch {
                    "none" => RoundingMode.None,
                    "halfup" => RoundingMode.HalfUp,
                    "up" => RoundingMode.Up,
                    "down" => RoundingMode.Down,
                    _ => (maybe = null, RoundingMode.None).Item2
                };
            }
        }
    }

    // -------------------------
    // Graph analysis: cycle detection
    // -------------------------

    public static class GraphAnalysis {
        // Edges: producedItem -> inputItem
        public static List<List<string>> FindCycles(DataStore store) {
            var adj = BuildAdj(store);

            var visited = new HashSet<string>();
            var inStack = new HashSet<string>();
            var stack = new List<string>();
            var cycles = new List<List<string>>();

            foreach (var node in adj.Keys) {
                if (!visited.Contains(node))
                    Dfs(node);
            }

            return cycles;

            void Dfs(string u) {
                visited.Add(u);
                inStack.Add(u);
                stack.Add(u);

                if (adj.TryGetValue(u, out var nexts)) {
                    foreach (var v in nexts) {
                        if (!visited.Contains(v)) {
                            Dfs(v);
                        } else if (inStack.Contains(v)) {
                            var idx = stack.IndexOf(v);
                            if (idx >= 0) {
                                var cyc = stack.Skip(idx).ToList();
                                var sig = string.Join(">", cyc);
                                if (!cycles.Any(c => string.Join(">", c) == sig))
                                    cycles.Add(cyc);
                            }
                        }
                    }
                }

                stack.RemoveAt(stack.Count - 1);
                inStack.Remove(u);
            }
        }

        private static Dictionary<string, HashSet<string>> BuildAdj(DataStore store) {
            var adj = new Dictionary<string, HashSet<string>>();

            foreach (var r in store.Recipes) {
                foreach (var o in r.Outputs) {
                    if (!adj.TryGetValue(o.Key, out var set))
                        adj[o.Key] = set = new HashSet<string>();

                    foreach (var i in r.Inputs)
                        set.Add(i.Key);
                }
            }

            return adj;
        }
    }

    // -------------------------
    // Need solver + Detailed result
    // -------------------------

    public enum NeedNodeKind {
        Raw,
        Recipe
    }

    public sealed class NeedNode {
        public NeedNodeKind Kind { get; set; }
        public string ItemKey { get; set; } = "";
        public decimal RequiredAmount { get; set; }

        // for recipe nodes
        public string RecipeId { get; set; } = "";
        public decimal Runs { get; set; }
        public Dictionary<string, decimal> Byproducts { get; set; } = new();

        public List<NeedNode> Children { get; set; } = new();
    }

    public readonly struct ItemBalance {
        public decimal Produced { get; }
        public decimal Consumed { get; }
        public ItemBalance(decimal produced, decimal consumed) {
            Produced = produced;
            Consumed = consumed;
        }
        public ItemBalance AddProduced(decimal x) => new ItemBalance(Produced + x, Consumed);
        public ItemBalance AddConsumed(decimal x) => new ItemBalance(Produced, Consumed + x);
    }

    public sealed class NeedResult {
        public Dictionary<string, decimal> Inputs { get; } = new();      // raw inputs
        public Dictionary<string, decimal> Byproducts { get; } = new();  // byproducts (informational)
        public Dictionary<string, decimal> RecipeRuns { get; } = new();  // recipeId -> runs
        public Dictionary<string, ItemBalance> ItemBalance { get; } = new(); // itemKey -> produced/consumed

        public NeedResult Clone() {
            var r = new NeedResult();
            foreach (var kv in Inputs) r.Inputs[kv.Key] = kv.Value;
            foreach (var kv in Byproducts) r.Byproducts[kv.Key] = kv.Value;
            foreach (var kv in RecipeRuns) r.RecipeRuns[kv.Key] = kv.Value;
            foreach (var kv in ItemBalance) r.ItemBalance[kv.Key] = kv.Value;
            return r;
        }

        public static NeedResult Merge(NeedResult a, NeedResult b) {
            var r = a.Clone();
            AddMap(r.Inputs, b.Inputs);
            AddMap(r.Byproducts, b.Byproducts);
            AddMap(r.RecipeRuns, b.RecipeRuns);
            AddBalance(r.ItemBalance, b.ItemBalance);
            return r;
        }

        private static void AddMap(Dictionary<string, decimal> dst, Dictionary<string, decimal> src) {
            foreach (var kv in src) {
                if (!dst.TryGetValue(kv.Key, out var v)) v = 0m;
                dst[kv.Key] = v + kv.Value;
            }
        }

        private static void AddBalance(Dictionary<string, ItemBalance> dst, Dictionary<string, ItemBalance> src) {
            foreach (var kv in src) {
                if (!dst.TryGetValue(kv.Key, out var cur))
                    cur = new ItemBalance(0m, 0m);
                dst[kv.Key] = new ItemBalance(cur.Produced + kv.Value.Produced, cur.Consumed + kv.Value.Consumed);
            }
        }
    }

    public sealed class NeedSolution {
        public NeedResult Result { get; set; } = new NeedResult();
        public List<NeedNode> Roots { get; set; } = new();
    }

    public sealed class NeedEngine {
        private readonly DataStore _store;
        private readonly Dictionary<string, List<Recipe>> _recipesByOutput;

        public NeedEngine(DataStore store) {
            _store = store;
            _recipesByOutput = IndexRecipes(store);
        }

        public List<NeedSolution> Solve(List<ItemStack> demands, int maxPatterns) {
            // start with one empty pattern
            var patterns = new List<NeedSolution>
            {
                new NeedSolution { Result = new NeedResult(), Roots = new List<NeedNode>() }
            };

            foreach (var d in demands) {
                var next = new List<NeedSolution>();

                foreach (var p in patterns) {
                    var expanded = ExpandOne(d.Key, d.Amount, maxPatterns - next.Count, new HashSet<string>());
                    foreach (var e in expanded) {
                        // merge result and append root
                        var merged = new NeedSolution {
                            Result = NeedResult.Merge(p.Result, e.Result),
                            Roots = new List<NeedNode>(p.Roots) { e.Root }
                        };
                        next.Add(merged);
                        if (next.Count >= maxPatterns) break;
                    }

                    if (next.Count >= maxPatterns) break;
                }

                patterns = next;
                if (patterns.Count == 0) break;
            }

            return patterns;
        }

        private sealed class ExpandResult {
            public NeedResult Result { get; set; } = new NeedResult();
            public NeedNode Root { get; set; } = new NeedNode();
        }

        // Expand key/amount into up to limit patterns; also builds a node tree
        private List<ExpandResult> ExpandOne(string key, decimal amount, int limit, HashSet<string> path) {
            var results = new List<ExpandResult>();
            if (limit <= 0) return results;

            // raw input leaf
            if (_store.Items.TryGetValue(key, out var it) && it.IsRawInput) {
                var res = new NeedResult();
                AddTo(res.Inputs, key, amount);
                AddConsumed(res.ItemBalance, key, amount);

                results.Add(new ExpandResult {
                    Result = res,
                    Root = new NeedNode {
                        Kind = NeedNodeKind.Raw,
                        ItemKey = key,
                        RequiredAmount = amount
                    }
                });
                return results;
            }

            // cycle block
            if (path.Contains(key)) return results;

            // no recipes
            if (!_recipesByOutput.TryGetValue(key, out var recipes) || recipes.Count == 0) return results;

            path.Add(key);

            foreach (var r in recipes) {
                if (results.Count >= limit) break;

                var outMain = r.Outputs.FirstOrDefault(x => x.Key == key);
                if (outMain.Key == null || outMain.Amount <= 0m) continue;

                var runs = amount / outMain.Amount;

                // seed result
                var seed = new NeedResult();

                // recipe runs
                AddTo(seed.RecipeRuns, r.Id, runs);

                // item balance: produced outputs, consumed inputs
                foreach (var o in r.Outputs)
                    AddProduced(seed.ItemBalance, o.Key, o.Amount * runs);

                foreach (var inp in r.Inputs)
                    AddConsumed(seed.ItemBalance, inp.Key, inp.Amount * runs);

                // byproducts informational (outputs except main key)
                var byp = new Dictionary<string, decimal>();
                foreach (var o in r.Outputs) {
                    if (o.Key == key) continue;
                    var amt = o.Amount * runs;
                    if (amt > 0m) {
                        AddTo(seed.Byproducts, o.Key, amt);
                        AddTo(byp, o.Key, amt);
                    }
                }

                // build node
                var node = new NeedNode {
                    Kind = NeedNodeKind.Recipe,
                    ItemKey = key,
                    RequiredAmount = amount,
                    RecipeId = r.Id,
                    Runs = runs,
                    Byproducts = byp,
                    Children = new List<NeedNode>()
                };

                // expand each input, cartesian product of its alternatives
                var partials = new List<ExpandResult>
                {
                    new ExpandResult { Result = seed, Root = node }
                };

                foreach (var inp in r.Inputs) {
                    var required = inp.Amount * runs;
                    var nextPartials = new List<ExpandResult>();

                    foreach (var p in partials) {
                        // Expand child with new path copy (still blocks cycles within this branch)
                        var childExpanded = ExpandOne(inp.Key, required, limit - nextPartials.Count, new HashSet<string>(path));
                        foreach (var c in childExpanded) {
                            var mergedRes = NeedResult.Merge(p.Result, c.Result);

                            // clone node tree root (shallow) and append this child
                            var rootClone = CloneNodeShallow(p.Root);
                            rootClone.Children = new List<NeedNode>(p.Root.Children) { c.Root };

                            nextPartials.Add(new ExpandResult {
                                Result = mergedRes,
                                Root = rootClone
                            });

                            if (nextPartials.Count >= limit) break;
                        }
                        if (nextPartials.Count >= limit) break;
                    }

                    partials = nextPartials;
                    if (partials.Count == 0) break;
                    if (results.Count >= limit) break;
                }

                foreach (var p in partials) {
                    results.Add(p);
                    if (results.Count >= limit) break;
                }
            }

            path.Remove(key);
            return results;
        }

        private static NeedNode CloneNodeShallow(NeedNode n) {
            return new NeedNode {
                Kind = n.Kind,
                ItemKey = n.ItemKey,
                RequiredAmount = n.RequiredAmount,
                RecipeId = n.RecipeId,
                Runs = n.Runs,
                Byproducts = n.Byproducts.ToDictionary(kv => kv.Key, kv => kv.Value),
                Children = new List<NeedNode>() // caller sets
            };
        }

        private static void AddTo(Dictionary<string, decimal> map, string key, decimal value) {
            if (!map.TryGetValue(key, out var v)) v = 0m;
            map[key] = v + value;
        }

        private static void AddProduced(Dictionary<string, ItemBalance> bal, string key, decimal value) {
            if (!bal.TryGetValue(key, out var cur)) cur = new ItemBalance(0m, 0m);
            bal[key] = cur.AddProduced(value);
        }

        private static void AddConsumed(Dictionary<string, ItemBalance> bal, string key, decimal value) {
            if (!bal.TryGetValue(key, out var cur)) cur = new ItemBalance(0m, 0m);
            bal[key] = cur.AddConsumed(value);
        }

        private static Dictionary<string, List<Recipe>> IndexRecipes(DataStore store) {
            var map = new Dictionary<string, List<Recipe>>();
            foreach (var r in store.Recipes) {
                foreach (var o in r.Outputs) {
                    if (!map.TryGetValue(o.Key, out var list))
                        map[o.Key] = list = new List<Recipe>();
                    list.Add(r);
                }
            }
            return map;
        }
    }
}
