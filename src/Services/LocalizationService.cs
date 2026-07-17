namespace WikeloContractor.Services;

public sealed class LocalizationService : ILocalizationService
{
    private const string DictionaryPathFragment = "/Resources/Localization/";
    private static readonly string[] _supportedLanguages = ["en", "uk"];

    public string CurrentLanguage { get; private set; } = "en";

    public void ApplyLanguage(string languageCode)
    {
        if (!_supportedLanguages.Contains(languageCode))
        {
            languageCode = "en";
        }

        var dictionaries = Application.Current.Resources.MergedDictionaries;

        var oldDictionary = dictionaries.FirstOrDefault(d =>
            d.Source is not null && d.Source.OriginalString.Contains(DictionaryPathFragment, StringComparison.OrdinalIgnoreCase));

        var newDictionary = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,{DictionaryPathFragment}Strings.{languageCode}.xaml"),
        };

        if (oldDictionary is not null)
        {
            var index = dictionaries.IndexOf(oldDictionary);
            dictionaries[index] = newDictionary;
        }
        else
        {
            dictionaries.Add(newDictionary);
        }

        CurrentLanguage = languageCode;
    }
}
