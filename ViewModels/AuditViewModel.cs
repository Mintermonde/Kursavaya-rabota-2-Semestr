using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using KursMVVM.Models;
using KursMVVM.Services;
using ReactiveUI;

namespace KursMVVM.ViewModels;

public class AuditViewModel : ViewModelBase
{
    private readonly DataBaseService _dbService;
    private ObservableCollection<AuditLog> _logs = new();
    private string _statusMessage = string.Empty;

    public ObservableCollection<AuditLog> Logs
    {
        get => _logs;
        private set => this.RaiseAndSetIfChanged(ref _logs, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public ReactiveCommand<Unit, Unit> LoadCommand { get; }

    public AuditViewModel(DataBaseService dbService)
    {
        _dbService = dbService;
        LoadCommand = ReactiveCommand.CreateFromTask(LoadAsync);
        LoadCommand.Execute().Subscribe();
    }

    private async Task LoadAsync()
    {
        await Task.Run(() =>
        {
            var logs = _dbService.GetAuditLogs(500);
            // УБРАН Dispatcher.UIThread.Post — загружаем напрямую
            Logs = new ObservableCollection< AuditLog > (logs);
            StatusMessage = $"Загружено записей: {logs.Count}";
        });
    }
}