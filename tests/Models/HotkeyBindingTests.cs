using System.Windows.Input;
using WikeloContractor.Models;
using Xunit;

namespace WikeloContractor.Tests.Models;

/// <summary>
/// The hotkey text format. It is both what the user sees and what lands in settings.json, so a round
/// trip has to be exact and garbage has to degrade instead of throwing — the file is hand-editable.
/// </summary>
public class HotkeyBindingTests
{
    [Theory]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+Shift")]
    [InlineData("Ctrl+Alt+O")]
    [InlineData("Ctrl+Alt+Shift+Win+F5")]
    [InlineData("Alt+D1")]
    public void Format_and_parse_round_trip(string text)
    {
        Assert.True(HotkeyBinding.TryParse(text, out var binding));

        Assert.Equal(text, binding.Format());
    }

    [Fact]
    public void Modifier_order_is_normalised_so_the_stored_text_does_not_churn()
    {
        Assert.True(HotkeyBinding.TryParse("Shift+Alt+Ctrl", out var binding));

        Assert.Equal("Ctrl+Alt+Shift", binding.Format());
    }

    [Theory]
    [InlineData("ctrl+alt")]
    [InlineData("CONTROL+ALT")]
    [InlineData("Control + Alt")]
    public void Parsing_is_case_and_space_tolerant(string text)
    {
        Assert.True(HotkeyBinding.TryParse(text, out var binding));

        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Alt, binding.Modifiers);
        Assert.True(binding.IsPattern);
    }

    [Fact]
    public void Modifiers_alone_are_a_pattern_a_slot_digit_completes()
    {
        Assert.True(HotkeyBinding.TryParse("Ctrl+Alt", out var pattern));
        Assert.True(pattern.IsPattern);

        var slotThree = pattern.WithKey(OverlaySlots.KeyFor(3));

        Assert.False(slotThree.IsPattern);
        Assert.Equal("Ctrl+Alt+D3", slotThree.Format());
    }

    [Theory]
    [InlineData("D1")]
    [InlineData("F5")]
    public void A_key_with_no_modifier_is_rejected(string text) =>
        // Owning a bare digit globally would swallow it in every other application.
        Assert.False(HotkeyBinding.TryParse(text, out _));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl+NotAKey")]
    [InlineData("Ctrl+A+B")]
    [InlineData("+++")]
    public void Garbage_returns_false_rather_than_throwing(string? text) =>
        Assert.False(HotkeyBinding.TryParse(text, out _));

    [Fact]
    public void A_binding_without_modifiers_is_not_valid() =>
        Assert.False(new HotkeyBinding(HotkeyModifiers.None, Key.F5).IsValid);
}
