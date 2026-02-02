namespace EndfieldRecipe.Core.Domain;

public sealed class Machine {
    public string Key { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Seconds { get; set; }
    public int InputTypeCount { get; set; }
    public int OutputTypeCount { get; set; }
}
