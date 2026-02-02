namespace EndfieldRecipe.Core.Domain;

public sealed class NeedPlan {
    public List<NeedTarget> Targets { get; } = new();
}

public sealed class NeedTarget {
    public NeedTarget(string itemKey, int lineCount) {
        ItemKey = itemKey;
        LineCount = lineCount;
    }

    public string ItemKey { get; }
    public int LineCount { get; set; }
}
