using System.Globalization;

namespace EndfieldRecipe.Core.Domain;

public readonly struct ItemStack {
    public string Key { get; }
    public decimal Amount { get; }

    public ItemStack(string key, decimal amount) {
        Key = key;
        Amount = amount;
    }

    public static string FormatAmount(decimal amt)
        => amt.ToString("G29", CultureInfo.InvariantCulture);
}
