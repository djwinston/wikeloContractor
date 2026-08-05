using WikeloContractor.Models;
using WikeloContractor.Models.Api;
using WikeloContractor.ViewModels;
using Xunit;

namespace WikeloContractor.Tests.E2E;

/// <summary>
/// The overlay, end to end: hotkey → inventory store → every surface that shows a count.
/// <para>
/// The seam is <c>IHotkeyService.Pressed</c> — the event, not the Win32 message. That is the whole
/// reason the overlay's view models hold no window reference: the interesting behaviour is what a
/// keypress does to the app's state while the player is inside Star Citizen, and none of it needs a
/// window to be provable. <see cref="HotkeyServiceTests"/> covers the message decoding separately.
/// </para>
/// </summary>
[Collection("WpfApp")]
public sealed class OverlayScenarios(WpfAppFixture app)
{
    /// <summary>Two contracts whose requirements give the inventory a handful of distinct items.</summary>
    private static ScriptedWikiApi MiningCatalog()
    {
        var api = new ScriptedWikiApi
        {
            Missions =
            [
                ScriptedWikiApi.Mission("m1", "Asgard Wikelo War Special"),
                ScriptedWikiApi.Mission("m2", "Testudo Armor Trade"),
            ],
        };

        api.MissionDetails["m1"] = new MissionDetailDto
        {
            Uuid = "m1",
            RewardItems = [new RewardItemDto { Name = "Asgard", Uuid = "ship-1", Amount = 1 }],
            HaulingOrders =
            [
                new HaulingOrderDto { Name = "Gold", MinScu = 36, MaxScu = 36 },
                new HaulingOrderDto { Name = "Carinite (Pure)", MaxAmount = 4 },
            ],
        };
        api.MissionDetails["m2"] = new MissionDetailDto
        {
            Uuid = "m2",
            RewardItems = [new RewardItemDto { Name = "Testudo Helmet", Uuid = "item-1", Amount = 1 }],
            HaulingOrders = [new HaulingOrderDto { Name = "Wikelo Favor", MaxAmount = 2 }],
        };

        api.Classifications["ship-1"] = new("Asgard", null, IsSpaceship: true, IsVehicleRecord: true, Images: []);
        api.Classifications["item-1"] = new(
            "Testudo Helmet", "Char_Armor_Helmet", IsSpaceship: false, IsVehicleRecord: false, Images: []);

        return api;
    }

    /// <summary>Loads the catalog and lets enrichment settle, so the requirement set is final.</summary>
    private async Task<CatalogHarness> ReadyAsync()
    {
        var harness = await CatalogHarness.CreateAsync(app, MiningCatalog());

        await harness.LoadAndEnrichAsync();

        await app.OnUiAsync(() => harness.Inventoried.OnNavigatedToAsync());
        await app.OnUiAsync(harness.Overlay.Initialize);

        return harness;
    }

    private static InventoryItemViewModel Row(CatalogHarness harness, string name) =>
        harness.Inventoried.Items!.Cast<InventoryItemViewModel>().Single(row => row.Name == name);

    private static ContractCardViewModel Card(CatalogHarness harness, string title) =>
        harness.Catalogue.Contracts!.Cast<ContractCardViewModel>().Single(card => card.Contract.Title == title);

    // The core journey: pin an item, press the hotkey in game, everything agrees.
    [Fact]
    public async Task An_increment_hotkey_moves_the_overlay_the_inventory_row_and_the_readiness_chip()
    {
        using var harness = await ReadyAsync();
        await app.OnUiAsync(() => harness.Catalogue.OnNavigatedToAsync());

        await app.OnUiAsync(async () =>
        {
            _ = await harness.Pins.PinAsync("Carinite (Pure)");

            harness.Hotkeys.Press(HotkeyAction.Increment, slot: 1);
            harness.Hotkeys.Press(HotkeyAction.Increment, slot: 1);
            harness.Hotkeys.Press(HotkeyAction.Increment, slot: 1);
        });

        await app.OnUiAsync(() =>
        {
            Assert.Equal(3, harness.Inventory.GetCount("Carinite (Pure)"));
            Assert.Equal(3, harness.Hud.SlotAt(1)!.Count);
            Assert.Equal(3, Row(harness, "Carinite (Pure)").Count);

            // The catalog card listens to the same store, so its chip has to follow.
            var chip = Card(harness, "Asgard Wikelo War Special")
                .RequirementChips.Single(r => r.Name == "Carinite (Pure)");
            Assert.Equal(RequirementAvailability.Partial, chip.Availability);
        });
    }

    [Fact]
    public async Task Decrementing_stops_at_zero()
    {
        using var harness = await ReadyAsync();

        await app.OnUiAsync(async () =>
        {
            _ = await harness.Pins.PinAsync("Gold");
            harness.Hotkeys.Press(HotkeyAction.Increment, slot: 1);

            harness.Hotkeys.Press(HotkeyAction.Decrement, slot: 1);
            harness.Hotkeys.Press(HotkeyAction.Decrement, slot: 1);
            harness.Hotkeys.Press(HotkeyAction.Decrement, slot: 1);
        });

        await app.OnUiAsync(() => Assert.Equal(0, harness.Hud.SlotAt(1)!.Count));
    }

    [Fact]
    public async Task A_hotkey_for_an_empty_slot_does_nothing()
    {
        using var harness = await ReadyAsync();

        await app.OnUiAsync(async () =>
        {
            _ = await harness.Pins.PinAsync("Gold");
            harness.Hotkeys.Press(HotkeyAction.Increment, slot: 7); // nothing pinned there
        });

        await app.OnUiAsync(() =>
        {
            Assert.Equal(0, harness.Inventory.GetCount("Gold"));
            Assert.Single(harness.Hud.Slots);
        });
    }

    [Fact]
    public async Task Editing_the_inventory_page_moves_the_overlay_too()
    {
        using var harness = await ReadyAsync();

        await app.OnUiAsync(async () =>
        {
            _ = await harness.Pins.PinAsync("Wikelo Favor");
            Row(harness, "Wikelo Favor").Count = 12;
        });

        await app.OnUiAsync(() => Assert.Equal(12, harness.Hud.SlotAt(1)!.Count));
    }

    [Fact]
    public async Task The_eleventh_pin_is_refused_and_the_counter_says_so()
    {
        using var harness = await ReadyAsync();

        await app.OnUiAsync(async () =>
        {
            // Only four distinct items exist in this catalog, so fill the grid directly.
            for (var i = 1; i <= OverlaySlots.MaxSlots; i++)
            {
                _ = await harness.Pins.PinAsync($"Filler {i}");
            }

            var row = Row(harness, "Gold");
            Assert.False(row.Pin.CanPin, "the pin button must stop inviting the click once the grid is full");
            Assert.False(row.Pin.ToggleCommand.CanExecute(null));

            Assert.False(await harness.Pins.PinAsync("Gold"));
            Assert.Equal(OverlaySlots.MaxSlots, harness.Hud.Slots.Count);
            Assert.Contains($"{OverlaySlots.MaxSlots}/{OverlaySlots.MaxSlots}", harness.Inventoried.OverlayPins.Summary);
        });
    }

    [Fact]
    public async Task Clearing_the_pins_empties_the_overlay_and_frees_every_slot()
    {
        using var harness = await ReadyAsync();

        await app.OnUiAsync(async () =>
        {
            _ = await harness.Pins.PinAsync("Gold");
            _ = await harness.Pins.PinAsync("Carinite (Pure)");

            Assert.True(harness.Inventoried.OverlayPins.ClearCommand.CanExecute(null));

            // ExecuteAsync, not Execute: the command is async and Execute fires and forgets, so the
            // assertions below would race the disk write.
            await harness.Inventoried.OverlayPins.ClearCommand.ExecuteAsync(null);
        });

        await app.OnUiAsync(() =>
        {
            Assert.Empty(harness.Hud.Slots);
            Assert.True(harness.Hud.IsEmpty);
            Assert.False(Row(harness, "Gold").Pin.IsPinned);
            Assert.Contains($"0/{OverlaySlots.MaxSlots}", harness.Inventoried.OverlayPins.Summary);

            // Nothing left to clear, so the button stops inviting the click.
            Assert.False(harness.Inventoried.OverlayPins.ClearCommand.CanExecute(null));
        });
    }

    [Fact]
    public async Task The_tenth_slot_is_reachable_and_labelled_zero()
    {
        // Regression: a saved window height turned the HUD into a fixed size, so the tenth row was
        // clipped away — the counter said 10/10 while the overlay showed nine and the "0" badge was
        // nowhere. The model side must be unambiguous about the tenth slot existing.
        using var harness = await ReadyAsync();

        await app.OnUiAsync(async () =>
        {
            for (var i = 1; i <= OverlaySlots.MaxSlots; i++)
            {
                _ = await harness.Pins.PinAsync($"Item {i}");
            }
        });

        await app.OnUiAsync(() =>
        {
            Assert.Equal(OverlaySlots.MaxSlots, harness.Hud.Slots.Count);
            Assert.Equal("1", harness.Hud.SlotAt(1)!.DigitLabel);
            Assert.Equal("9", harness.Hud.SlotAt(9)!.DigitLabel);
            Assert.Equal("0", harness.Hud.SlotAt(OverlaySlots.MaxSlots)!.DigitLabel);

            harness.Hotkeys.Press(HotkeyAction.Increment, slot: OverlaySlots.MaxSlots);
            Assert.Equal(1, harness.Inventory.GetCount($"Item {OverlaySlots.MaxSlots}"));
        });
    }

    [Fact]
    public async Task Unpinning_renumbers_the_slots_below_it()
    {
        using var harness = await ReadyAsync();

        await app.OnUiAsync(async () =>
        {
            _ = await harness.Pins.PinAsync("Gold");
            _ = await harness.Pins.PinAsync("Carinite (Pure)");
            _ = await harness.Pins.PinAsync("Wikelo Favor");

            await harness.Pins.UnpinAsync("Carinite (Pure)");
        });

        await app.OnUiAsync(() =>
        {
            Assert.Equal("Wikelo Favor", harness.Hud.SlotAt(2)!.Name);
            Assert.Null(harness.Hud.SlotAt(3));

            // And the inventory row's badge agrees, so the digit on screen is the digit to press.
            Assert.Equal("2", Row(harness, "Wikelo Favor").Pin.SlotLabel);
            Assert.False(Row(harness, "Carinite (Pure)").Pin.IsPinned);
        });
    }

    [Fact]
    public async Task Pinning_re_registers_the_hotkeys_for_the_new_slot_count()
    {
        using var harness = await ReadyAsync();

        await app.OnUiAsync(async () =>
        {
            _ = await harness.Pins.PinAsync("Gold");
            _ = await harness.Pins.PinAsync("Carinite (Pure)");
        });

        await app.OnUiAsync(() =>
        {
            // Two toggles plus two slots each way: registering all twenty digits up front would steal
            // combinations from the machine for slots that hold nothing.
            Assert.Equal(6, harness.Hotkeys.LastPlan!.Registrations.Count);
        });
    }

    [Fact]
    public async Task The_toggle_hotkey_shows_and_hides_the_window()
    {
        using var harness = await ReadyAsync();

        await app.OnUiAsync(() =>
        {
            harness.Hotkeys.Press(HotkeyAction.ToggleOverlay);
            Assert.True(harness.Overlay.IsShown);
            Assert.True(harness.OverlayWindow.IsVisible);

            harness.Hotkeys.Press(HotkeyAction.ToggleOverlay);
            Assert.False(harness.Overlay.IsShown);
            Assert.Equal(1, harness.OverlayWindow.HideCount);
        });
    }

    [Fact]
    public async Task Interactive_mode_turns_click_through_off_and_back_on()
    {
        using var harness = await ReadyAsync();

        await app.OnUiAsync(() =>
        {
            harness.Hotkeys.Press(HotkeyAction.ToggleOverlay);
            Assert.True(harness.OverlayWindow.ClickThrough, "a shown HUD starts click-through");

            harness.Hotkeys.Press(HotkeyAction.ToggleInteractive);
            Assert.False(harness.OverlayWindow.ClickThrough);
            Assert.True(harness.Hud.IsInteractive);

            harness.Hotkeys.Press(HotkeyAction.ToggleInteractive);
            Assert.True(harness.OverlayWindow.ClickThrough);
        });
    }

    [Fact]
    public async Task Unlocking_a_hidden_overlay_shows_it_first()
    {
        // Otherwise the hotkey appears to do nothing and reads as broken.
        using var harness = await ReadyAsync();

        await app.OnUiAsync(() =>
        {
            Assert.False(harness.Overlay.IsShown);

            harness.Hotkeys.Press(HotkeyAction.ToggleInteractive);

            Assert.True(harness.Overlay.IsShown);
            Assert.True(harness.Hud.IsInteractive);
        });
    }

    [Fact]
    public async Task A_failed_interactive_toggle_forces_interactive_mode_at_startup()
    {
        // The one failure that produces "the app is broken and I can't fix it": a click-through HUD
        // that no hotkey can unlock and no mouse can reach.
        using var harness = await CatalogHarness.CreateAsync(app, MiningCatalog());
        harness.Hotkeys.RefuseToRegister.Add(HotkeyAction.ToggleInteractive);

        await app.OnUiAsync(harness.Overlay.Initialize);

        await app.OnUiAsync(() =>
        {
            Assert.True(harness.Overlay.IsInteractive);

            harness.Overlay.Show();
            Assert.False(harness.OverlayWindow.ClickThrough, "the window must accept the mouse");
        });
    }

    [Fact]
    public async Task Geometry_is_saved_on_hide_and_restored_on_the_next_show()
    {
        using var harness = await ReadyAsync();

        await app.OnUiAsync(() =>
        {
            harness.Overlay.Show();
            harness.OverlayWindow.Placement = new System.Windows.Rect(640, 200, 300, 420);
            harness.Overlay.Hide();

            Assert.Equal(640, harness.Settings.Current.Overlay.Left);
            Assert.Equal(420, harness.Settings.Current.Overlay.Height);

            harness.Overlay.Show();
            Assert.Equal((640d, 200d, 300d, 420d), harness.OverlayWindow.Restored);
        });
    }

    [Fact]
    public async Task Resetting_the_position_forgets_the_saved_geometry()
    {
        using var harness = await ReadyAsync();

        await app.OnUiAsync(() =>
        {
            harness.Overlay.Show();
            harness.OverlayWindow.Placement = new System.Windows.Rect(640, 200, 300, 420);
            harness.Overlay.Hide();

            harness.Overlay.ResetPlacement();

            Assert.Null(harness.Settings.Current.Overlay.Left);
            Assert.Null(harness.Settings.Current.Overlay.Width);
        });
    }

    [Fact]
    public async Task Shutdown_saves_the_placement_and_closes_the_window()
    {
        using var harness = await ReadyAsync();

        await app.OnUiAsync(() =>
        {
            harness.Overlay.Show();
            harness.OverlayWindow.Placement = new System.Windows.Rect(100, 100, 280, 300);

            harness.Overlay.Shutdown();

            Assert.True(harness.OverlayWindow.IsClosed);
            Assert.Equal(100, harness.Settings.Current.Overlay.Left);
            Assert.False(harness.Overlay.IsShown);
        });
    }

    [Fact]
    public async Task The_placement_survives_the_window_being_closed_before_shutdown_runs()
    {
        // Application.Shutdown closes every window BEFORE raising Exit, so by the time the host's
        // StopAsync calls Shutdown() the overlay is already gone. Reading the live window's bounds
        // there lost the geometry the user had just dragged into place, on every single exit.
        using var harness = await ReadyAsync();

        await app.OnUiAsync(() =>
        {
            harness.Overlay.Show();
            harness.OverlayWindow.Placement = new System.Windows.Rect(320, 480, 300, 260);
            harness.OverlayWindow.CloseOverlay();

            harness.Overlay.Shutdown();

            Assert.Equal(320, harness.Settings.Current.Overlay.Left);
            Assert.Equal(480, harness.Settings.Current.Overlay.Top);
        });
    }

    [Fact]
    public async Task A_hotkey_after_shutdown_is_ignored()
    {
        using var harness = await ReadyAsync();

        await app.OnUiAsync(async () =>
        {
            _ = await harness.Pins.PinAsync("Gold");
            harness.Overlay.Shutdown();

            harness.Hotkeys.Press(HotkeyAction.Increment, slot: 1);

            Assert.Equal(0, harness.Inventory.GetCount("Gold"));
        });
    }

    [Fact]
    public async Task Pins_survive_a_restart_and_come_back_in_the_same_slots()
    {
        using var first = await ReadyAsync();
        await app.OnUiAsync(async () =>
        {
            _ = await first.Pins.PinAsync("Gold");
            _ = await first.Pins.PinAsync("Wikelo Favor");
            first.Overlay.Shutdown();
        });

        using var restarted = await CatalogHarness.CreateAsync(app, MiningCatalog(), first.Root);

        await app.OnUiAsync(() =>
        {
            Assert.Equal("Gold", restarted.Hud.SlotAt(1)!.Name);
            Assert.Equal("Wikelo Favor", restarted.Hud.SlotAt(2)!.Name);
        });
    }
}
