using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using iTextSharp.text;
using KursMVVM.Models;
using KursMVVM.Services;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;

namespace KursMVVM.ViewModels;

public class DealsViewModel : ViewModelBase
{
    private readonly DataBaseService _dbService;
    private readonly AuthService _authService;
    private ObservableCollection<Deal> _deals = new();
    private Deal? _selectedDeal;
    private string _statusMessage = string.Empty;

    public ObservableCollection<Deal> Deals
    {
        get => _deals;
        private set => this.RaiseAndSetIfChanged(ref _deals, value);
    }

    public Deal? SelectedDeal
    {
        get => _selectedDeal;
        set => this.RaiseAndSetIfChanged(ref _selectedDeal, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public bool IsAdmin => _authService.IsAdmin;

    public ReactiveCommand<Unit, Unit> LoadDealsCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteDealCommand { get; }

    public DealsViewModel(DataBaseService dbService, AuthService authService)
    {
        _dbService = dbService;
        _authService = authService;

        LoadDealsCommand = ReactiveCommand.CreateFromTask(LoadDealsAsync);
        DeleteDealCommand = ReactiveCommand.CreateFromTask(DeleteDealWithConfirmAsync);

        // АВТОЗАГРУЗКА при создании ViewModel
        _ = LoadDealsAsync();
    }

    private async Task LoadDealsAsync()
    {
        // Загружаем БЕЗ Task.Run для начальной загрузки
        List < Deal > deals;
        if (_authService.IsCashier && _authService.CurrentCashierId.HasValue)
        {
            deals = _dbService.GetDealsByCashier(_authService.CurrentCashierId.Value);
        }
        else
        {
            deals = _dbService.GetAllDeals();
        }

        // Очищаем и добавляем — не создаём новую коллекцию!
        Deals.Clear();
        foreach (var d in deals) Deals.Add(d);
        StatusMessage = $"Загружено сделок: {deals.Count}";
    }

    private async Task DeleteDealWithConfirmAsync()
    {
        if (SelectedDeal is null) return;

        var message = $"Удалить сделку #{SelectedDeal.DealId}?\n{SelectedDeal.ClientName} — {SelectedDeal.FormattedSoldAmount}";

        var result = await ShowConfirmDialogAsync("Подтверждение удаления", message);
        if (!result) return;

        await Task.Run(() =>
        {
            _dbService.DeleteDeal(SelectedDeal.DealId);
            _dbService.LogAudit(_authService.CurrentUserName, "DELETE_DEAL",
                $"Удалена сделка #{SelectedDeal.DealId}");
        });

        await LoadDealsAsync();
    }

    private async Task<bool> ShowConfirmDialogAsync(string title, string message)
    {
        var tcs = new TaskCompletionSource<bool>();

        Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
        {
            var dialog = new Window
            {
                Title = title,
                Width = 400,
                Height = 180,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var panel = new StackPanel { Margin = new Avalonia.Thickness(20) };
            panel.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(0, 0, 0, 20)
            });

            var buttons = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Spacing = 10
            };

            var yesBtn = new Button
            {
                Content = "Удалить",
                Background = Avalonia.Media.Brushes.Crimson,
                Foreground = Avalonia.Media.Brushes.White,
                Padding = new Avalonia.Thickness(16, 8)
            };
            yesBtn.Click += (s, e) => { tcs.TrySetResult(true); dialog.Close(); };

            var noBtn = new Button
            {
                Content = "Отмена",
                Padding = new Avalonia.Thickness(16, 8)
            };
            noBtn.Click += (s, e) => { tcs.TrySetResult(false); dialog.Close(); };

            buttons.Children.Add(yesBtn);
            buttons.Children.Add(noBtn);
            panel.Children.Add(buttons);

            dialog.Content = panel;

            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            {
                await dialog.ShowDialog(lifetime.MainWindow!);
            }
            else
            {
                tcs.TrySetResult(false);
            }
        });

        return await tcs.Task;
    }
}