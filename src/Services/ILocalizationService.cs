namespace WikeloContractor.Services;

public interface ILocalizationService
{
    string CurrentLanguage { get; }

    /// <param name="languageCode">"en" or "uk".</param>
    void ApplyLanguage(string languageCode);
}
