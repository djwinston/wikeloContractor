using Wpf.Ui.Abstractions.Controls;
using WikeloContractor.ViewModels;

namespace WikeloContractor.Views.Pages;

public partial class FavoritesPage : INavigableView<FavoritesViewModel>
{
    public FavoritesViewModel ViewModel { get; }

    public FavoritesPage(FavoritesViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
