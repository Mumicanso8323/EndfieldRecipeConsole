using EndfieldRecipe.Core.Domain;

namespace EndfieldRecipe.Core.Ports;

public interface ISettingsRepository {
    AppSettings Load();
    void Save(AppSettings settings);
}
