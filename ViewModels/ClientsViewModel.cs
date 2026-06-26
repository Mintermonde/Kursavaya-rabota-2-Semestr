using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using KursMVVM.Models;
using KursMVVM.Services;
using ReactiveUI;

namespace KursMVVM.ViewModels;

public class ClientsViewModel : ViewModelBase
{
    private readonly DataBaseService _dbService;
    private readonly AuthService _authService;
    private ObservableCollection<Client> _clients = new();
    private Client? _selectedClient;
    private string _newFullName = string.Empty;
    private string _newPassport = string.Empty;
    private string _statusMessage = string.Empty;

    public ObservableCollection<Client> Clients
    {
        get => _clients;
        private set => this.RaiseAndSetIfChanged(ref _clients, value);
    }

    public Client? SelectedClient
    {
        get => _selectedClient;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedClient, value);
            if (value != null)
            {
                NewFullName = value.FullName;
                NewPassport = value.PassportNumber;
            }
        }
    }

    public string NewFullName
    {
        get => _newFullName;
        set => this.RaiseAndSetIfChanged(ref _newFullName, value);
    }

    public string NewPassport
    {
        get => _newPassport;
        set => this.RaiseAndSetIfChanged(ref _newPassport, value);
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

    public ClientsViewModel(DataBaseService dbService, AuthService authService)
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
            var clients = _dbService.GetAllClients();
            // УБРАН Dispatcher.UIThread.Post — загружаем напрямую
            Clients = new ObservableCollection< Client > (clients);
            StatusMessage = $"Загружено клиентов: {clients.Count}";
        });
    }

    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(NewFullName) || string.IsNullOrWhiteSpace(NewPassport))
        {
            StatusMessage = "Ошибка: заполните ФИО и номер паспорта";
            return;
        }

        var client = new Client { FullName = NewFullName.Trim(), PassportNumber = NewPassport.Trim() };
        await Task.Run(() =>
        {
            _dbService.AddClient(client);
            _dbService.LogAudit(_authService.CurrentUserName, "ADD_CLIENT", $"Добавлен клиент: {client.FullName}");
        });
        await LoadAsync();
        ClearFields();
        StatusMessage = "Клиент добавлен";
    }

    private async Task UpdateAsync()
    {
        if (SelectedClient is null) return;
        if (string.IsNullOrWhiteSpace(NewFullName) || string.IsNullOrWhiteSpace(NewPassport))
        {
            StatusMessage = "Ошибка: заполните ФИО и номер паспорта";
            return;
        }

        SelectedClient.FullName = NewFullName.Trim();
        SelectedClient.PassportNumber = NewPassport.Trim();
        await Task.Run(() =>
        {
            _dbService.UpdateClient(SelectedClient);
            _dbService.LogAudit(_authService.CurrentUserName, "UPDATE_CLIENT", $"Обновлен клиент #{SelectedClient.ClientId}");
        });
        await LoadAsync();
        StatusMessage = "Клиент обновлен";
    }

    private async Task DeleteAsync()
    {
        if (SelectedClient is null) return;
        await Task.Run(() =>
        {
            _dbService.DeleteClient(SelectedClient.ClientId);
            _dbService.LogAudit(_authService.CurrentUserName, "DELETE_CLIENT", $"Удален клиент #{SelectedClient.ClientId}");
        });
        await LoadAsync();
        ClearFields();
        StatusMessage = "Клиент удален";
    }

    private void ClearFields()
    {
        NewFullName = string.Empty;
        NewPassport = string.Empty;
        SelectedClient = null;
    }
}