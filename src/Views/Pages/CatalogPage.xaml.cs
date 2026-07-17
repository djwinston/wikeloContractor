using Wpf.Ui.Abstractions.Controls;
using WikeloContractor.ViewModels;

namespace WikeloContractor.Views.Pages;

public partial class CatalogPage : INavigableView<CatalogViewModel>
{
    public CatalogViewModel ViewModel { get; }

    public CatalogPage(CatalogViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
