using WikeloContractor.Models;
using WikeloContractor.ViewModels;

namespace WikeloContractor.Services;

/// <inheritdoc cref="IOverlayService" />
public sealed class OverlayService(
    IHotkeyService hotkeys,
    IPinnedItemsService pins,
    ISettingsService settings,
    OverlayViewModel viewModel,
    Func<IOverlayWindow> windowFactory) : IOverlayService
{
    private IOverlayWindow? _window;

    private bool _initialized;

    public bool IsShown => viewModel.IsShown;

    public bool IsInteractive => viewModel.IsInteractive;

    private OverlaySettings Overlay => settings.Current.Overlay;

    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        hotkeys.Pressed += OnHotkeyPressed;
        pins.Changed += OnPinsChanged;

        ApplyHotkeys();

        // The one failure that produces "the app is broken and I can't fix it": a click-through HUD
        // whose interactive toggle never registered cannot be reached by mouse or by keyboard. Start
        // interactive in that case, so the user can at least drag it away.
        SetInteractive(!hotkeys.LastResult.IsRegistered(HotkeyAction.ToggleInteractive));

        if (Overlay.ShowOnStartup)
        {
            Show();
        }
    }

    public void ApplyHotkeys() => _ = hotkeys.Apply(HotkeyPlan.Build(Overlay, pins.Count));

    public void Show()
    {
        var window = _window ??= windowFactory();

        // Click-through before the window appears, so a HUD-mode overlay never has a frame in which
        // it can swallow a click.
        window.SetClickThrough(!IsInteractive);
        window.ShowOverlay(Overlay.Left, Overlay.Top, Overlay.Width, Overlay.Height);

        viewModel.IsShown = true;
    }

    public void Hide()
    {
        CapturePlacement();
        _window?.HideOverlay();
        viewModel.IsShown = false;
    }

    public void Toggle()
    {
        if (IsShown)
        {
            Hide();
            return;
        }

        Show();
    }

    public void SetInteractive(bool interactive)
    {
        viewModel.IsInteractive = interactive;
        _window?.SetClickThrough(!interactive);
    }

    public void ToggleInteractive()
    {
        // Unlocking a hidden overlay does nothing visible, which reads as a broken hotkey — show it.
        if (!IsShown)
        {
            Show();
        }

        SetInteractive(!IsInteractive);
    }

    public void ResetPlacement()
    {
        Overlay.Left = null;
        Overlay.Top = null;
        Overlay.Width = null;
        Overlay.Height = null;
        SavePlacement();

        if (IsShown)
        {
            // Re-show with nothing saved: the window falls back to its default corner.
            _window?.ShowOverlay(null, null, null, null);
        }
    }

    public void Shutdown()
    {
        hotkeys.Pressed -= OnHotkeyPressed;
        pins.Changed -= OnPinsChanged;

        if (IsShown)
        {
            CapturePlacement(blocking: true);
        }

        _window?.CloseOverlay();
        _window = null;
        viewModel.IsShown = false;
        _initialized = false;
    }

    private void OnPinsChanged(object? sender, EventArgs e) =>
        // The plan only registers the digits actually pinned, so pinning the third item is what makes
        // Ctrl+Alt+3 exist at all.
        ApplyHotkeys();

    private void OnHotkeyPressed(object? sender, HotkeyPressed pressed)
    {
        switch (pressed.Action)
        {
            case HotkeyAction.Increment:
                viewModel.Adjust(pressed.Slot, 1);
                break;
            case HotkeyAction.Decrement:
                viewModel.Adjust(pressed.Slot, -1);
                break;
            case HotkeyAction.ToggleOverlay:
                Toggle();
                break;
            case HotkeyAction.ToggleInteractive:
                ToggleInteractive();
                break;
        }
    }

    private void CapturePlacement(bool blocking = false)
    {
        if (_window?.Placement is not { } placement)
        {
            return;
        }

        Overlay.Left = placement.Left;
        Overlay.Top = placement.Top;
        Overlay.Width = placement.Width;
        Overlay.Height = placement.Height;

        SavePlacement(blocking);
    }

    private void SavePlacement(bool blocking = false)
    {
        if (!blocking)
        {
            _ = settings.SaveAsync();
            return;
        }

        // Shutdown path. Task.Run first: this runs on the dispatcher thread, and blocking on a task
        // whose continuations would be posted back to that same dispatcher is a deadlock.
        Task.Run(settings.SaveAsync).GetAwaiter().GetResult();
    }
}
