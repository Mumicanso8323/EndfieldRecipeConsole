namespace EndfieldRecipe.Core.Domain;

public sealed class NeedResult {
    public List<NeedOutSummary> OutSummary { get; } = new();
    public List<NeedInSummary> InSummary { get; } = new();
    public List<RecipeNode> TreeRoots { get; } = new();
}

public sealed class NeedOutSummary {
    public NeedOutSummary(string itemKey, int lineCount, decimal flowPerMin) {
        ItemKey = itemKey;
        LineCount = lineCount;
        FlowPerMin = flowPerMin;
    }

    public string ItemKey { get; }
    public int LineCount { get; }
    public decimal FlowPerMin { get; }
}

public sealed class NeedInSummary {
    public NeedInSummary(string itemKey, decimal flowPerMin) {
        ItemKey = itemKey;
        FlowPerMin = flowPerMin;
    }

    public string ItemKey { get; }
    public decimal FlowPerMin { get; }
}

public sealed class RecipeNode {
    public string RecipeId { get; set; } = "";
    public string RecipeName { get; set; } = "";
    public string MachineType { get; set; } = "";
    public decimal MachineCount { get; set; }
    public decimal RunsPerMin { get; set; }
    public bool IsCyclic { get; set; }
    public List<ItemFlow> Outputs { get; } = new();
    public List<ItemFlow> Inputs { get; } = new();
    public List<RecipeNode> Children { get; } = new();
}

public sealed class ItemFlow {
    public ItemFlow(string itemKey, decimal flowPerMin) {
        ItemKey = itemKey;
        FlowPerMin = flowPerMin;
    }

    public string ItemKey { get; }
    public decimal FlowPerMin { get; }
}
