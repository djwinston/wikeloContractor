using WikeloContractor.Models;

namespace WikeloContractor.Services;

public interface ISettingsService
{
    AppSettings Current { get; }

    Task LoadAsync();

    Task SaveAsync();
}
