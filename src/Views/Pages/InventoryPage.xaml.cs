using Wpf.Ui.Abstractions.Controls;
using WikeloContractor.ViewModels;

namespace WikeloContractor.Views.Pages;

public partial class InventoryPage : INavigableView<InventoryViewModel>
{
    public InventoryViewModel ViewModel { get; }

    public InventoryPage(InventoryViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
