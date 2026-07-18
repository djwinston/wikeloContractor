using Wpf.Ui.Abstractions.Controls;
using WikeloContractor.ViewModels;

namespace WikeloContractor.Views.Pages;

public partial class ContractDetailPage : INavigableView<ContractDetailViewModel>
{
    public ContractDetailViewModel ViewModel { get; }

    public ContractDetailPage(ContractDetailViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
