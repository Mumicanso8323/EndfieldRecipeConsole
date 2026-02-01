namespace EndfieldRecipe.Core.Domain;

public sealed class Item {
    public string Key { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsRawInput { get; set; }
}
