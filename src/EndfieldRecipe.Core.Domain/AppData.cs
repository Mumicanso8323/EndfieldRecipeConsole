namespace EndfieldRecipe.Core.Domain;

public sealed class AppData {
    public Dictionary<string, Item> Items { get; } = new();
    public Dictionary<string, Machine> Machines { get; } = new();
    public List<Recipe> Recipes { get; } = new();
    public NeedPlan NeedPlan { get; } = new();
    public OptimizationPlan Optimization { get; } = new();
}
