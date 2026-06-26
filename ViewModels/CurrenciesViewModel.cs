using KursMVVM.Models;
using KursMVVM.Services;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Reactive;
using System.Threading.Tasks;

namespace KursMVVM.ViewModels;

public class CurrenciesViewModel : ViewModelBase
{
    private readonly DataBaseService _dbService;
    private readonly AuthService _authService;
    private ObservableCollection<Currency> _currencies = new();
    private Currency? _selectedCurrency;
    private string _newCode = string.Empty;
    private string _newName = string.Empty;
    private double _newSellRate;
    private double _newBuyRate;
    private string _statusMessage = string.Empty;

    public ObservableCollection<Currency> Currencies
    {
        get => _currencies;
        private set => this.RaiseAndSetIfChanged(ref _currencies, value);
    }

    public Currency? SelectedCurrency
    {
        get => _selectedCurrency;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedCurrency, value);
            if (value != null)
            {
                NewCode = value.Code;
                NewName = value.Name;
                NewSellRate = value.SellRate;
                NewBuyRate = value.BuyRate;
            }
        }
    }

    public string NewCode { get => _newCode; set => this.RaiseAndSetIfChanged(ref _newCode, value); }
    public string NewName { get => _newName; set => this.RaiseAndSetIfChanged(ref _newName, value); }
    public double NewSellRate { get => _newSellRate; set => this.RaiseAndSetIfChanged(ref _newSellRate, value); }
    public double NewBuyRate { get => _newBuyRate; set => this.RaiseAndSetIfChanged(ref _newBuyRate, value); }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public ReactiveCommand<Unit, Unit> LoadCommand { get; }
    public ReactiveCommand<Unit, Unit> AddCommand { get; }
    public ReactiveCommand<Unit, Unit> UpdateCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshRatesCommand { get; }


    public CurrenciesViewModel(DataBaseService dbService, AuthService authService)
    {
        _dbService = dbService;
        _authService = authService;

        LoadCommand = ReactiveCommand.CreateFromTask(LoadAsync);
        AddCommand = ReactiveCommand.CreateFromTask(AddAsync);
        UpdateCommand = ReactiveCommand.CreateFromTask(UpdateAsync);
        DeleteCommand = ReactiveCommand.CreateFromTask(DeleteAsync);
        ClearCommand = ReactiveCommand.Create(ClearFields);
        RefreshRatesCommand = ReactiveCommand.CreateFromTask(RefreshRatesAsync);

    }

    private async Task LoadAsync()
    {
        await Task.Run(() =>
        {
            var currencies = _dbService.GetAllCurrencies();
            // УБРАН Dispatcher.UIThread.Post — загружаем напрямую
            Currencies = new ObservableCollection<Currency>(currencies);
            StatusMessage = $"Загружено валют: {currencies.Count}";
        });
    }

    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCode) || string.IsNullOrWhiteSpace(NewName))
        { StatusMessage = "Ошибка: заполните код и название"; return; }
        if (NewSellRate <= 0 || NewBuyRate <= 0)
        { StatusMessage = "Ошибка: курсы должны быть > 0"; return; }

        var currency = new Currency { Code = NewCode.Trim().ToUpper(), Name = NewName.Trim(), SellRate = NewSellRate, BuyRate = NewBuyRate };
        await Task.Run(() =>
        {
            _dbService.AddCurrency(currency);
            _dbService.LogAudit(_authService.CurrentUserName, "ADD_CURRENCY", $"Добавлена валюта: {currency.Code}");
        });
        await LoadAsync();
        ClearFields();
        StatusMessage = "Валюта добавлена";
    }

    private async Task UpdateAsync()
    {
        if (SelectedCurrency is null) return;
        if (NewSellRate <= 0 || NewBuyRate <= 0)
        { StatusMessage = "Ошибка: курсы должны быть > 0"; return; }

        SelectedCurrency.SellRate = NewSellRate;
        SelectedCurrency.BuyRate = NewBuyRate;
        SelectedCurrency.Name = NewName.Trim();
        await Task.Run(() =>
        {
            _dbService.UpdateCurrency(SelectedCurrency);
            _dbService.LogAudit(_authService.CurrentUserName, "UPDATE_CURRENCY", $"Обновлена валюта: {SelectedCurrency.Code}");
        });
        await LoadAsync();
        StatusMessage = "Курсы обновлены";
    }

    private async Task DeleteAsync()
    {
        if (SelectedCurrency is null)
        {
            StatusMessage = "Выберите валюту для удаления";
            return;
        }

        try
        {
            // Проверяем СНАРУЖИ Task.Run
            bool isUsed = await Task.Run(() => _dbService.IsCurrencyUsedInDeals(SelectedCurrency.Code));
            if (isUsed)
            {
                StatusMessage = $"Невозможно удалить валюту: она используется в сделках.";
                return;
            }

            await Task.Run(() => _dbService.DeleteCurrency(SelectedCurrency.Code));
            StatusMessage = $"Валюта {SelectedCurrency.Code} удалена";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка удаления: {ex.Message}";
        }
    }

    private async Task RefreshRatesAsync()
    {
        StatusMessage = "Загрузка курсов ЦБ РФ...";
        try
        {
            await _dbService.UpdateRatesFromCbrAsync();
            await LoadAsync(); // Перезагружаем список валют
            StatusMessage = "Курсы успешно обновлены с ЦБ РФ";
            _dbService.LogAudit(_authService.CurrentUserName, "UPDATE_RATES", "Обновлены курсы валют с ЦБ РФ");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка обновления: {ex.Message}";
        }
    }


    private void ClearFields()
    {
        NewCode = string.Empty;
        NewName = string.Empty;
        NewSellRate = 0;
        NewBuyRate = 0;
        SelectedCurrency = null;
    }
}