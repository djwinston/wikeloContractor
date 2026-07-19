using System.Text;
using WikeloContractor.Models;
using WikeloContractor.Services;
using UiMessageBox = Wpf.Ui.Controls.MessageBox;
using UiMessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace WikeloContractor.ViewModels;

/// <summary>
/// Coordinates the "mark completed" flow with the inventory: completing a contract confirms and then
/// deducts its required items; un-completing warns that those items were already spent (they are not
/// added back). Shared by the catalog card and the detail page so the flow lives in one place.
/// </summary>
public sealed class ContractCompletionInteraction
{
    private readonly ICompletionService _completionService;
    private readonly IInventoryStore _inventoryStore;

    public ContractCompletionInteraction(ICompletionService completionService, IInventoryStore inventoryStore)
    {
        _completionService = completionService;
        _inventoryStore = inventoryStore;
    }

    /// <summary>
    /// Toggles the completion state of a contract, showing the appropriate confirmation/warning dialog.
    /// Completing is expected only when the contract is ready (the callers gate the command on it).
    /// </summary>
    public async Task ToggleAsync(WikeloContract contract)
    {
        if (_completionService.IsCompleted(contract.Uuid))
        {
            await UncompleteAsync(contract);
        }
        else
        {
            await CompleteAsync(contract);
        }
    }

    private async Task CompleteAsync(WikeloContract contract)
    {
        var message = Localized.String("Complete_Dialog_Message") + "\n\n" + DeductionList(contract);
        if (!await ConfirmAsync("Complete_Dialog_Title", message, "Complete_Dialog_Confirm"))
        {
            return;
        }

        // Deduct every requirement in one store write + one Changed event (a readiness rebuild fans out
        // to the whole catalog per event). Accumulate per name so a repeated item deducts cumulatively.
        var targets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var requirement in contract.Requirements)
        {
            var current = targets.TryGetValue(requirement.Name, out var planned)
                ? planned
                : _inventoryStore.GetCount(requirement.Name);
            targets[requirement.Name] = current - InventoryReadiness.RequiredCount(requirement);
        }

        await _inventoryStore.SetCountsAsync(targets);
        await _completionService.SetCompletedAsync(contract, true);
    }

    private async Task UncompleteAsync(WikeloContract contract)
    {
        var message = Localized.String("Uncomplete_Dialog_Message") + "\n\n" + DeductionList(contract);
        if (!await ConfirmAsync("Uncomplete_Dialog_Title", message, "Uncomplete_Dialog_Confirm"))
        {
            return;
        }

        await _completionService.SetCompletedAsync(contract, false);
    }

    private static async Task<bool> ConfirmAsync(string titleKey, string message, string confirmKey)
    {
        var box = new UiMessageBox
        {
            Title = Localized.String(titleKey) ?? string.Empty,
            Content = message,
            PrimaryButtonText = Localized.String(confirmKey) ?? string.Empty,
            CloseButtonText = Localized.String("Dialog_Cancel") ?? string.Empty,
            Owner = Application.Current.MainWindow,
        };

        return await box.ShowDialogAsync() == UiMessageBoxResult.Primary;
    }

    /// <summary>"• Wikelo Favor × 1\n• Carinite (Pure) × 4" — the whole-unit amounts to be deducted.</summary>
    private static string DeductionList(WikeloContract contract)
    {
        var builder = new StringBuilder();
        foreach (var requirement in contract.Requirements)
        {
            _ = builder.Append("• ").Append(requirement.Name).Append(" × ")
                .Append(InventoryReadiness.RequiredCount(requirement)).Append('\n');
        }

        return builder.ToString().TrimEnd('\n');
    }
}
