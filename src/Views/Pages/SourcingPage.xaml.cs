using Wpf.Ui.Abstractions.Controls;
using WikeloContractor.ViewModels;

namespace WikeloContractor.Views.Pages;

public partial class SourcingPage : INavigableView<SourcingViewModel>
{
    public SourcingViewModel ViewModel { get; }

    public SourcingPage(SourcingViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
