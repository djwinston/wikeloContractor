using Wpf.Ui.Abstractions.Controls;
using WikeloContractor.ViewModels;

namespace WikeloContractor.Views.Pages;

public partial class SourcingDetailPage : INavigableView<SourcingDetailViewModel>
{
    public SourcingDetailViewModel ViewModel { get; }

    public SourcingDetailPage(SourcingDetailViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
