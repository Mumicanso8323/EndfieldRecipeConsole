using System.Text;
using EndfieldRecipe.Core.Application;
using EndfieldRecipe.Infra;

namespace EndfieldRecipe.Tui;

public static class Program {
    public static void Main(string[] args) {
        Console.OutputEncoding = Encoding.UTF8;
        var dataPath = args.Length > 0 ? args[0] : "data.json";
        var repository = new JsonAppRepository(dataPath);
        var data = repository.Load();
        var history = new NeedHistory(3);
        var context = new ScreenContext(data, repository, history);

        try {
            var navigator = new ScreenNavigator(new HomeScreen());
            navigator.Run(context);
        } finally {
            Console.CursorVisible = true;
        }
    }
}
