using EndfieldRecipe.Core.Domain;

namespace EndfieldRecipe.Core.Ports;

public interface IAppRepository {
    AppData Load();
    void Save(AppData data);
}
