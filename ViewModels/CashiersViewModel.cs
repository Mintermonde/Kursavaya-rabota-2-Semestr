using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using KursMVVM.Models;
using KursMVVM.Services;
using ReactiveUI;

namespace KursMVVM.ViewModels;

public class CashiersViewModel : ViewModelBase
{
    private readonly DataBaseService _dbService;
    private readonly AuthService _authService;
    private ObservableCollection<Cashier> _cashiers = new();
    private Cashier? _selectedCashier;
    private string _newFullName = string.Empty;
    private string _statusMessage = string.Empty;

    public ObservableCollection<Cashier> Cashiers
    {
        get => _cashiers;
        private set => this.RaiseAndSetIfChanged(ref _cashiers, value);
    }

    public Cashier? SelectedCashier
    {
        get => _selectedCashier;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedCashier, value);
            if (value != null) NewFullName = value.FullName;
        }
    }

    public string NewFullName
    {
        get => _newFullName;
        set => this.RaiseAndSetIfChanged(ref _newFullName, value);
    }

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

    public CashiersViewModel(DataBaseService dbService, AuthService authService)
    {
        _dbService = dbService;
        _authService = authService;

        LoadCommand = ReactiveCommand.CreateFromTask(LoadAsync);
        AddCommand = ReactiveCommand.CreateFromTask(AddAsync);
        UpdateCommand = ReactiveCommand.CreateFromTask(UpdateAsync);
        DeleteCommand = ReactiveCommand.CreateFromTask(DeleteAsync);
        ClearCommand = ReactiveCommand.Create(ClearFields);
    }

    private async Task LoadAsync()
    {
        await Task.Run(() =>
        {
            var cashiers = _dbService.GetAllCashiers();
            // УБРАН Dispatcher.UIThread.Post — загружаем напрямую
            Cashiers = new ObservableCollection<Cashier>(cashiers);
            StatusMessage = $"Загружено кассиров: {cashiers.Count}";
        });
    }

    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(NewFullName)) { StatusMessage = "Ошибка: введите ФИО"; return; }

        var cashier = new Cashier { FullName = NewFullName.Trim() };
        await Task.Run(() =>
        {
            _dbService.AddCashier(cashier);
            _dbService.LogAudit(_authService.CurrentUserName, "ADD_CASHIER", $"Добавлен кассир: {cashier.FullName}");
        });
        await LoadAsync();
        ClearFields();
        StatusMessage = "Кассир добавлен";
    }

    private async Task UpdateAsync()
    {
        if (SelectedCashier is null) return;
        if (string.IsNullOrWhiteSpace(NewFullName)) { StatusMessage = "Ошибка: введите ФИО"; return; }

        SelectedCashier.FullName = NewFullName.Trim();
        await Task.Run(() =>
        {
            _dbService.UpdateCashier(SelectedCashier);
            _dbService.LogAudit(_authService.CurrentUserName, "UPDATE_CASHIER", $"Обновлен кассир #{SelectedCashier.CashierId}");
        });
        await LoadAsync();
        StatusMessage = "Кассир обновлен";
    }

    private async Task DeleteAsync()
    {
        if (SelectedCashier is null) return;
        await Task.Run(() =>
        {
            _dbService.DeleteCashier(SelectedCashier.CashierId);
            _dbService.LogAudit(_authService.CurrentUserName, "DELETE_CASHIER", $"Удален кассир #{SelectedCashier.CashierId}");
        });
        await LoadAsync();
        ClearFields();
        StatusMessage = "Кассир удален";
    }

    private void ClearFields()
    {
        NewFullName = string.Empty;
        SelectedCashier = null;
    }
}