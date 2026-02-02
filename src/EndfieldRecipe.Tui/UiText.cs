namespace EndfieldRecipe.Tui;

public static class UiText {
    // UI strings must remain Japanese (do not translate).
    public const string AppTitle = "Endfieldレシピコンソール";
    public const string HomeTitle = "ホーム";
    public const string NeedTitle = "需要";
    public const string NeedResultTitle = "需要結果";
    public const string ItemsTitle = "アイテム";
    public const string MachinesTitle = "マシン";
    public const string RecipesTitle = "レシピ";
    public const string SettingsTitle = "設定";
    public const string KeymapTitle = "キー設定";
    public const string DiagnosticsTitle = "診断";
    public const string OptimizationTitle = "最適化";
    public const string OptimizationResultTitle = "最適化結果";
    public const string ExitTitle = "終了";
    public const string HelpHome = "Enter/Space=決定 / Esc=終了";
    public const string HelpNeedList = "Enter/Space=編集 A=追加 D=削除 R=結果 / Esc=戻る / Ctrl+Z=戻す / Ctrl+Y=やり直す";
    public const string HelpNeedResult = "T=展開/折りたたみ / Esc=戻る / Ctrl+Z=戻す / Ctrl+Y=やり直す";
    public const string HelpList = "Esc=戻る";
    public const string HelpListSearch = "Esc=戻る / /=検索";
    public const string HelpSettings = "Enter/Space=変更 / Esc=戻る";
    public const string HelpKeymap = "Enter/Space=割当 / D=削除 / X=初期化 / Esc=戻る";
    public const string HelpItems = "Enter/Space=編集 A=追加 D=削除 /=検索 / Esc=戻る";
    public const string HelpMachines = "Enter/Space=編集 A=追加 D=削除 /=検索 / Esc=戻る";
    public const string HelpRecipes = "Enter/Space=編集 A=追加 D=削除 /=検索 / Esc=戻る";
    public const string HelpPicker = "Enter/Space=選択 / Esc=戻る / /=検索";
    public const string HelpDiagnostics = "Esc=戻る";
    public const string HelpOptimizationInput = "Enter/Space=編集 A=追加 D=削除 R=実行 / /=検索 / Esc=戻る";
    public const string HelpOptimizationResult = "Esc=戻る";
    public const string StatusEmpty = "(なし)";
    public const string InputItemKey = "アイテムID入力";
    public const string InputLineCount = "ライン数入力";
    public const string LabelSearch = "検索";
    public const string LabelNameInput = "名前入力";
    public const string LabelValueInput = "価値入力";
    public const string LabelRawInput = "RAW設定";
    public const string LabelSecondsInput = "秒数入力";
    public const string LabelInputTypeCountInput = "入力種類数入力";
    public const string LabelOutputTypeCountInput = "出力種類数入力";
    public const string LabelAmountInput = "数量入力";
    public const string LabelInputPerMinInput = "入力/分入力";
    public const string LabelDeleteConfirm = "削除しますか? (Y/N)";
    public const string ErrorItemNotFound = "アイテムが見つかりません";
    public const string ErrorLineCount = "ライン数が正しくありません";
    public const string ErrorInvalidNumber = "数値が正しくありません";
    public const string ErrorNameRequired = "名前が必要です";
    public const string ErrorDeleteInUse = "使用中のため削除できません";
    public const string ErrorMachineNotFound = "マシンが見つかりません";
    public const string ErrorRecipeInvalid = "レシピが正しくありません";
    public const string LabelOut = "出力";
    public const string LabelIn = "入力";
    public const string LabelTree = "ツリー";
    public const string LabelMachine = "マシン";
    public const string LabelValue = "価値";
    public const string LabelRaw = "RAW";
    public const string LabelYes = "はい";
    public const string LabelNo = "いいえ";
    public const string LabelAddInput = "入力追加";
    public const string LabelAddOutput = "出力追加";
    public const string LabelSave = "保存";
    public const string DiagnosticsSummary = "警告/エラー";
    public const string DiagnosticsWarning = "警告";
    public const string DiagnosticsError = "エラー";
    public const string DiagnosticsInfo = "情報";
    public const string LabelCandidates = "候補";
    public const string LabelOutputs = "出力";
    public const string LabelInputsUsed = "使用入力";
    public const string LabelInputsLeft = "余り入力";
    public const string LabelAllocations = "マシン割当";
    public const string LabelInvalidOptimization = "有効なレシピがありません (入力アイテム/マシン定義を確認してください)";
    public const string LabelHideInternalKeys = "内部ID非表示";
    public const string LabelHistoryDepth = "Undo段数";
    public const string LabelHistoryDepthInput = "Undo段数入力";
    public const string LabelItemSort = "アイテム並び";
    public const string LabelSortValue = "価値";
    public const string LabelSortName = "名前";
    public const string LabelOn = "オン";
    public const string LabelOff = "オフ";
    public const string LabelNotSet = "(未設定)";
    public const string KeymapCapture = "新しいキーを押してください";
    public const string KeymapInvalid = "このキーは割り当てできません";
    public const string KeymapNoBinding = "削除するキーがありません";
    public const string LabelCyclic = " (循環)";
    public const string ColumnItem = "アイテム";
    public const string ColumnLines = "ライン数";
    public const string ColumnFlow = "流量/分";

    public static string GetIntentLabel(IntentKind kind) {
        return kind switch {
            IntentKind.Confirm => "決定",
            IntentKind.Back => "戻る",
            IntentKind.DeleteChar => "文字削除",
            IntentKind.MoveUp => "上移動",
            IntentKind.MoveDown => "下移動",
            IntentKind.MoveLeft => "左移動",
            IntentKind.MoveRight => "右移動",
            IntentKind.PageUp => "ページ上",
            IntentKind.PageDown => "ページ下",
            IntentKind.JumpTop => "先頭へ",
            IntentKind.JumpBottom => "末尾へ",
            IntentKind.SearchStart => "検索開始",
            IntentKind.Help => "ヘルプ",
            IntentKind.OpenResult => "結果を開く",
            IntentKind.ToggleExpand => "展開/折りたたみ",
            IntentKind.Add => "追加",
            IntentKind.Remove => "削除",
            IntentKind.Undo => "戻す",
            IntentKind.Redo => "やり直す",
            _ => "不明"
        };
    }
}
