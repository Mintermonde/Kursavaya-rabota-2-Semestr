using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using KursMVVM.Models;
using KursMVVM.Services;
using ReactiveUI;

namespace KursMVVM.ViewModels;

public class CalculatorViewModel : ViewModelBase
{
    private readonly DataBaseService _dbService;
    private readonly AuthService _authService;

    public ObservableCollection<Client> Clients { get; } = new();
    public ObservableCollection<Cashier> Cashiers { get; } = new();
    public ObservableCollection<Currency> Currencies { get; } = new();

    private Client? _selectedClient;
    private Cashier? _selectedCashier;
    private Currency? _soldCurrency;
    private Currency? _boughtCurrency;
    private double _amount;
    private string _resultText = "Заполните все поля для расчета";
    private string _statusMessage = string.Empty;

    public Client? SelectedClient
    {
        get => _selectedClient;
        set { this.RaiseAndSetIfChanged(ref _selectedClient, value); }
    }

    public Cashier? SelectedCashier
    {
        get => _selectedCashier;
        set { this.RaiseAndSetIfChanged(ref _selectedCashier, value); Calculate(); }
    }

    public Currency? SoldCurrency
    {
        get => _soldCurrency;
        set { this.RaiseAndSetIfChanged(ref _soldCurrency, value); Calculate(); }
    }

    public Currency? BoughtCurrency
    {
        get => _boughtCurrency;
        set { this.RaiseAndSetIfChanged(ref _boughtCurrency, value); Calculate(); }
    }

    public double Amount
    {
        get => _amount;
        set { this.RaiseAndSetIfChanged(ref _amount, value); Calculate(); }
    }

    public string ResultText
    {
        get => _resultText;
        private set => this.RaiseAndSetIfChanged(ref _resultText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public bool IsAdmin => _authService.IsAdmin;
    public bool CanChangeCashier => _authService.IsAdmin;

    public ReactiveCommand<Unit, Unit> CalculateCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateDealCommand { get; }

    public CalculatorViewModel(DataBaseService dbService, AuthService authService)
    {
        _dbService = dbService;
        _authService = authService;

        CalculateCommand = ReactiveCommand.Create(Calculate);
        CreateDealCommand = ReactiveCommand.CreateFromTask(CreateDealAsync);

        _ = LoadDataSafeAsync();
    }

    public async Task LoadDataSafeAsync()
    {
        try
        {
            await Task.Run(() =>
            {
                var clients = _dbService.GetAllClients();
                var cashiers = _dbService.GetAllCashiers();
                var currencies = _dbService.GetAllCurrencies();
                var rub = new Currency { Code = "RUB", Name = "Российский рубль", BuyRate = 1.0, SellRate = 1.0 };

                // ОЧИЩАЕМ перед загрузкой
                Clients.Clear();
                Cashiers.Clear();
                Currencies.Clear();

                foreach (var c in clients) Clients.Add(c);
                foreach (var k in cashiers) Cashiers.Add(k);
                foreach (var v in currencies) Currencies.Add(v);
                Currencies.Add(rub);

                SelectedClient = Clients.FirstOrDefault();
                SoldCurrency = Currencies.FirstOrDefault(c => c.Code == "USD");
                BoughtCurrency = Currencies.FirstOrDefault(c => c.Code == "RUB");

                if (_authService.IsCashier && _authService.CurrentCashierId.HasValue)
                {
                    SelectedCashier = Cashiers.FirstOrDefault(c => c.CashierId == _authService.CurrentCashierId.Value);
                }
                else
                {
                    SelectedCashier = Cashiers.FirstOrDefault();
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Calculator LoadData ERROR: {ex}");
        }
    }

    private void Calculate()
    {
        if (SoldCurrency is null || BoughtCurrency is null || Amount <= 0)
        {
            ResultText = "Заполните все поля для расчета";
            return;
        }

        double result;
        if (SoldCurrency.Code == "RUB")
            result = Amount / BoughtCurrency.BuyRate;
        else if (BoughtCurrency.Code == "RUB")
            result = Amount * SoldCurrency.SellRate;
        else
            result = (Amount * SoldCurrency.SellRate) / BoughtCurrency.BuyRate;

        ResultText = $"При обмене {Amount:N2} {SoldCurrency.Name} вы получите {result:N2} {BoughtCurrency.Name}";
    }

    private async Task CreateDealAsync()
    {
        if (SelectedClient is null || SelectedCashier is null ||
            SoldCurrency is null || BoughtCurrency is null || Amount <= 0)
        {
            StatusMessage = "Ошибка: заполните все поля";
            return;
        }

        if (_authService.IsCashier && _authService.CurrentCashierId.HasValue &&
            SelectedCashier.CashierId != _authService.CurrentCashierId.Value)
        {
            StatusMessage = "Ошибка: вы можете создавать сделки только от своего имени";
            return;
        }

        double boughtAmount;
        if (SoldCurrency.Code == "RUB")
            boughtAmount = Amount / BoughtCurrency.BuyRate;
        else if (BoughtCurrency.Code == "RUB")
            boughtAmount = Amount * SoldCurrency.SellRate;
        else
            boughtAmount = (Amount * SoldCurrency.SellRate) / BoughtCurrency.BuyRate;

        var deal = new Deal
        {
            SoldCurrencyCode = SoldCurrency.Code,
            BoughtCurrencyCode = BoughtCurrency.Code,
            CashierId = SelectedCashier.CashierId,
            ClientId = SelectedClient.ClientId,
            DealDate = DateTime.Now.Date,
            DealTime = DateTime.Now.TimeOfDay,
            SoldAmount = Amount,
            BoughtAmount = Math.Round(boughtAmount, 2)
        };

        await Task.Run(() =>
        {
            _dbService.AddDeal(deal);
            _dbService.LogAudit(_authService.CurrentUserName, "CREATE_DEAL",
                $"Создана сделка: {SelectedClient.FullName}, {SoldCurrency.Code} -> {BoughtCurrency.Code}, {Amount:N2}");
        });

        StatusMessage = "Сделка успешно создана!";
        Amount = 0;
    }
}