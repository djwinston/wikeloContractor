using WikeloContractor.Models;
using Xunit;

namespace WikeloContractor.Tests.Models;

public sealed class HotkeyBackendKindTests
{
    [Theory]
    [InlineData("RawInput", HotkeyBackendKind.RawInput)]
    [InlineData("rawinput", HotkeyBackendKind.RawInput)]
    [InlineData("RegisterHotKey", HotkeyBackendKind.RegisterHotKey)]
    [InlineData("  Auto  ", HotkeyBackendKind.Auto)]
    public void A_named_backend_is_read_case_insensitively(string text, HotkeyBackendKind expected) =>
        Assert.Equal(expected, HotkeyBackendKinds.Parse(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("RawInupt")]
    [InlineData("7")]
    public void Anything_unreadable_falls_back_to_auto(string? text) =>
        // settings.json is hand-editable: a typo must not leave the user with no hotkeys at all.
        Assert.Equal(HotkeyBackendKind.Auto, HotkeyBackendKinds.Parse(text));
}
