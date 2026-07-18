using System.IO;
using System.Xml.Linq;
using Xunit;

namespace WikeloContractor.Tests.Localization;

/// <summary>
/// Guards the "always add keys to BOTH dictionaries" project rule by comparing
/// the en and uk resource dictionaries as plain XML (no WPF runtime needed).
/// </summary>
public class LocalizationParityTests
{
    private static readonly XNamespace _xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static string LocalizationDirectory
    {
        get
        {
            // Walk up from the test bin directory to the repo root.
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, "src", "Resources", "Localization");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate src/Resources/Localization above the test bin directory.");
        }
    }

    private static Dictionary<string, string> LoadStrings(string language)
    {
        var path = Path.Combine(LocalizationDirectory, $"Strings.{language}.xaml");
        var document = XDocument.Load(path);

        return document.Root!
            .Elements()
            .ToDictionary(e => (string)e.Attribute(_xaml + "Key")!, e => e.Value);
    }

    [Fact]
    public void Both_dictionaries_declare_the_same_keys()
    {
        var en = LoadStrings("en").Keys.ToHashSet();
        var uk = LoadStrings("uk").Keys.ToHashSet();

        var missingInUk = en.Except(uk).Order().ToList();
        var missingInEn = uk.Except(en).Order().ToList();

        Assert.True(missingInUk.Count == 0 && missingInEn.Count == 0,
            $"Missing in uk: [{string.Join(", ", missingInUk)}]; missing in en: [{string.Join(", ", missingInEn)}]");
    }

    [Theory]
    [InlineData("en")]
    [InlineData("uk")]
    public void No_key_has_an_empty_value(string language)
    {
        var empty = LoadStrings(language)
            .Where(pair => string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => pair.Key)
            .ToList();

        Assert.True(empty.Count == 0, $"Empty values in {language}: [{string.Join(", ", empty)}]");
    }

    [Fact]
    public void Format_placeholders_match_between_languages()
    {
        var en = LoadStrings("en");
        var uk = LoadStrings("uk");

        foreach (var (key, enValue) in en)
        {
            if (!uk.TryGetValue(key, out var ukValue))
            {
                continue; // Key parity is asserted by its own test.
            }

            for (var i = 0; i < 3; i++)
            {
                var placeholder = $"{{{i}}}";
                Assert.True(
                    enValue.Contains(placeholder) == ukValue.Contains(placeholder),
                    $"Placeholder {placeholder} mismatch for key '{key}'.");
            }
        }
    }
}
