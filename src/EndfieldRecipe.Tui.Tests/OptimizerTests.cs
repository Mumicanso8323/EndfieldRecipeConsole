using EndfieldRecipe.Core.Application;
using EndfieldRecipe.Core.Domain;
using Xunit;

public sealed class OptimizerTests {
    [Fact]
    public void Solve_ReturnsCandidatesAndLeftovers() {
        var data = new AppData();
        data.Items["iron"] = new Item { Key = "iron", DisplayName = "鉄", Value = 2 };
        data.Items["copper"] = new Item { Key = "copper", DisplayName = "銅", Value = 1 };
        data.Items["plate"] = new Item { Key = "plate", DisplayName = "板", Value = 5 };
        data.Machines["m1"] = new Machine { Key = "m1", DisplayName = "加工機", Seconds = 10, InputTypeCount = 2, OutputTypeCount = 1 };
        data.Recipes.Add(new Recipe {
            Id = "r1",
            MachineKey = "m1",
            Inputs = new List<ItemStack> { new("iron", 2), new("copper", 1) },
            Outputs = new List<ItemStack> { new("plate", 1) }
        });

        data.Optimization.Supplies.Add(new SupplyEntry { ItemKey = "iron", AmountPerMin = 12m });
        data.Optimization.Supplies.Add(new SupplyEntry { ItemKey = "copper", AmountPerMin = 6m });

        var optimizer = new Optimizer();
        var result = optimizer.Solve(data, data.Optimization);

        Assert.NotEmpty(result.Candidates);
        var candidate = result.Candidates[0];
        Assert.True(candidate.TotalOutputValuePerMin > 0m);
        Assert.True(candidate.InputLeftPerMin.ContainsKey("iron"));
    }
}
