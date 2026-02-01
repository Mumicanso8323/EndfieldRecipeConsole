namespace EndfieldRecipe.Core.Domain;

public sealed class Recipe {
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Seconds { get; set; } = 1;
    public string MachineKey { get; set; } = "";
    public List<ItemStack> Inputs { get; set; } = new();
    public List<ItemStack> Outputs { get; set; } = new();
}
