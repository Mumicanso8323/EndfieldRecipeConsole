using System.IO;
using EndfieldRecipe.Core.Application;
using EndfieldRecipe.Core.Domain;
using EndfieldRecipe.Infra;
using Xunit;

public sealed class DiagnosticsTests {
    [Fact]
    public void Analyze_DetectsDuplicateRecipeIdentity() {
        var data = new AppData();
        data.Items["i1"] = new Item { Key = "i1", DisplayName = "入力1" };
        data.Items["i2"] = new Item { Key = "i2", DisplayName = "入力2" };
        data.Items["o1"] = new Item { Key = "o1", DisplayName = "出力1" };
        data.Machines["m1"] = new Machine { Key = "m1", DisplayName = "マシン1", Seconds = 5, InputTypeCount = 2, OutputTypeCount = 1 };
        data.Recipes.Add(new Recipe {
            Id = "r1",
            MachineKey = "m1",
            Inputs = new List<ItemStack> { new("i1", 1), new("i2", 1) },
            Outputs = new List<ItemStack> { new("o1", 1) }
        });
        data.Recipes.Add(new Recipe {
            Id = "r2",
            MachineKey = "m1",
            Inputs = new List<ItemStack> { new("i2", 2), new("i1", 3) },
            Outputs = new List<ItemStack> { new("o1", 1) }
        });

        var diagnostics = AppDiagnostics.Analyze(data);

        Assert.Contains(diagnostics, d => d.Code == "DuplicateRecipeIdentity" && d.Level == DiagnosticLevel.Warning);
    }

    [Fact]
    public void Load_MigratesMachineSecondsFromRecipes() {
        var json = """
        {
          "Items": {
            "i1": { "Key": "i1", "DisplayName": "入力1", "IsRawInput": false, "Value": 0 },
            "o1": { "Key": "o1", "DisplayName": "出力1", "IsRawInput": false, "Value": 0 }
          },
          "Machines": {
            "m1": { "Key": "m1", "DisplayName": "マシン1", "InputTypeCount": 0, "OutputTypeCount": 0 }
          },
          "Recipes": [
            {
              "Id": "r1",
              "DisplayName": "",
              "Seconds": 5,
              "MachineKey": "m1",
              "Inputs": [ { "Key": "i1", "Amount": 1 } ],
              "Outputs": [ { "Key": "o1", "Amount": 1 } ]
            },
            {
              "Id": "r2",
              "DisplayName": "",
              "Seconds": 5,
              "MachineKey": "m1",
              "Inputs": [ { "Key": "i1", "Amount": 2 } ],
              "Outputs": [ { "Key": "o1", "Amount": 2 } ]
            }
          ],
          "NeedPlan": { "Targets": [] }
        }
        """;

        var path = Path.GetTempFileName();
        try {
            File.WriteAllText(path, json);
            var repo = new JsonAppRepository(path);

            var data = repo.Load();

            Assert.True(data.Machines.TryGetValue("m1", out var machine));
            Assert.Equal(5, machine.Seconds);
            Assert.Equal(1, machine.InputTypeCount);
            Assert.Equal(1, machine.OutputTypeCount);
        } finally {
            File.Delete(path);
        }
    }
}
