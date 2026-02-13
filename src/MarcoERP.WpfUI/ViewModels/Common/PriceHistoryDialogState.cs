using System.Collections.ObjectModel;
using MarcoERP.Application.DTOs.Common;

namespace MarcoERP.WpfUI.ViewModels.Common
{
    public sealed class PriceHistoryDialogState : BaseViewModel
    {
        public ObservableCollection<PriceHistoryRowDto> RecentPrices { get; } = new();

        private string _counterpartyLabel;
        public string CounterpartyLabel
        {
            get => _counterpartyLabel;
            set => SetProperty(ref _counterpartyLabel, value);
        }

        private decimal? _counterpartyPrice;
        public decimal? CounterpartyPrice
        {
            get => _counterpartyPrice;
            set
            {
                if (SetProperty(ref _counterpartyPrice, value))
                    OnPropertyChanged(nameof(HasCounterpartyPrice));
            }
        }

        public bool HasCounterpartyPrice => CounterpartyPrice.HasValue;

        private PriceHistoryRowDto _selectedRow;
        public PriceHistoryRowDto SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (SetProperty(ref _selectedRow, value))
                    OnPropertyChanged(nameof(CanApplySelected));
            }
        }

        public bool CanApplySelected => SelectedRow != null;
    }
}
