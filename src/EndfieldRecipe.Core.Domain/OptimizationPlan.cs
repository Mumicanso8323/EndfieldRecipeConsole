namespace EndfieldRecipe.Core.Domain;

public sealed class OptimizationPlan {
    public List<SupplyEntry> Supplies { get; } = new();
}

public sealed class SupplyEntry {
    public string ItemKey { get; set; } = "";
    public decimal AmountPerMin { get; set; } = 0m;
}
