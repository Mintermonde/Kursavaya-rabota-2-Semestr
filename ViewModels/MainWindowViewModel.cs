using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using KursMVVM.Models;
using KursMVVM.Services;
using ReactiveUI;

namespace KursMVVM.ViewModels;

/// <summary>
/// Элемент бокового меню
/// </summary>
public class NavItem
{
    public string Title { get; }
    public string Icon { get; }
    public ViewModelBase Content { get; }
    public bool RequiresAdmin { get; }

    public NavItem(string title, string icon, ViewModelBase content, bool requiresAdmin = false)
    {
        Title = title;
        Icon = icon;
        Content = content;
        RequiresAdmin = requiresAdmin;
    }
}

/// <summary>
/// Главная ViewModel — управляет навигацией и ролевым доступом
/// </summary>
public class MainWindowViewModel : ViewModelBase
{
    private readonly DataBaseService _dbService;
    private readonly AuthService _authService;
    private ViewModelBase _currentContent = null!;
    private NavItem? _selectedNavItem;
    private string _windowTitle = "Пункт обмена валюты";

    public AuthService AuthService => _authService;

    public string WindowTitle
    {
        get => _windowTitle;
        private set => this.RaiseAndSetIfChanged(ref _windowTitle, value);
    }

    public ViewModelBase CurrentContent
    {
        get => _currentContent;
        private set => this.RaiseAndSetIfChanged(ref _currentContent, value);
    }

    public NavItem? SelectedNavItem
    {
        get => _selectedNavItem;
        set
        {
            System.Diagnostics.Debug.WriteLine($"=== SelectedNavItem SETTER: old={_selectedNavItem?.Title ?? "null"}, new={value?.Title ?? "null"} ===");
            this.RaiseAndSetIfChanged(ref _selectedNavItem, value);
            if (value != null)
            {
                System.Diagnostics.Debug.WriteLine($"=== Setting CurrentContent to {value.Content.GetType().Name} ===");
                CurrentContent = value.Content;
                System.Diagnostics.Debug.WriteLine($"=== Calling LoadContentData ===");
                LoadContentData(value.Content);
            }
        }
    }

    private void LoadContentData(ViewModelBase content)
    {
        switch (content)
        {
            case DealsViewModel vm: vm.LoadDealsCommand.Execute().Subscribe(); break;
            case ClientsViewModel vm: vm.LoadCommand.Execute().Subscribe(); break;
            case CashiersViewModel vm: vm.LoadCommand.Execute().Subscribe(); break;
            case CurrenciesViewModel vm: vm.LoadCommand.Execute().Subscribe(); break;
            case CalculatorViewModel vm: _ = vm.LoadDataSafeAsync(); break;
            case AuditViewModel vm: vm.LoadCommand.Execute().Subscribe(); break;
                // Charts загружаются через кнопку "Обновить"
        }
    }

    /// <summary>Все элементы навигации</summary>
    public List<NavItem> AllNavItems { get; } = new();

    /// <summary>Отфильтрованные по роли элементы</summary>
    public ObservableCollection<NavItem> VisibleNavItems { get; } = new();

    public ReactiveCommand<Unit, Unit> LogoutCommand { get; }
    public ReactiveCommand<Unit, Unit> ExitCommand { get; }

    public event EventHandler? RequestLogout;
    public event EventHandler? RequestExit;

    // ViewModels
    public DealsViewModel DealsVm { get; }
    public CalculatorViewModel CalculatorVm { get; }
    public ClientsViewModel ClientsVm { get; }
    public CashiersViewModel CashiersVm { get; }
    public CurrenciesViewModel CurrenciesVm { get; }
    public ReportsViewModel ReportsVm { get; }
    public ChartsViewModel ChartsVm { get; }
    public ImportExportViewModel ImportExportVm { get; }
    public AuditViewModel AuditVm { get; }

    public MainWindowViewModel(DataBaseService dbService, AuthService authService)
    {
        _dbService = dbService;
        _authService = authService;

        // Создаем ViewModels БЕЗ автозагрузки данных
        DealsVm = new DealsViewModel(dbService, authService);
        CalculatorVm = new CalculatorViewModel(dbService, authService);
        ClientsVm = new ClientsViewModel(dbService, authService);
        CashiersVm = new CashiersViewModel(dbService, authService);
        CurrenciesVm = new CurrenciesViewModel(dbService, authService);
        ReportsVm = new ReportsViewModel(dbService, authService);
        ChartsVm = new ChartsViewModel(dbService);
        ImportExportVm = new ImportExportViewModel(dbService, authService);
        AuditVm = new AuditViewModel(dbService);

        // Все элементы навигации
        AllNavItems.Add(new NavItem("Калькулятор", "🧮", CalculatorVm));
        AllNavItems.Add(new NavItem("Сделки", "💱", DealsVm));
        AllNavItems.Add(new NavItem("Клиенты", "👥", ClientsVm));
        AllNavItems.Add(new NavItem("Кассиры", "💼", CashiersVm));
        AllNavItems.Add(new NavItem("Валюты", "💰", CurrenciesVm, requiresAdmin: true));
        AllNavItems.Add(new NavItem("Отчеты", "📊", ReportsVm));
        AllNavItems.Add(new NavItem("Графики", "📈", ChartsVm));
        AllNavItems.Add(new NavItem("Импорт/Экспорт", "📤", ImportExportVm));
        AllNavItems.Add(new NavItem("Журнал аудита", "📋", AuditVm, requiresAdmin: true));

        LogoutCommand = ReactiveCommand.Create(() =>
        {
            _authService.Logout();
            RequestLogout?.Invoke(this, EventArgs.Empty);
        });

        ExitCommand = ReactiveCommand.Create(() =>
        {
            RequestExit?.Invoke(this, EventArgs.Empty);
        });

        ApplyRoleFilter();

        // Устанавливаем начальную страницу
        var defaultItem = VisibleNavItems.FirstOrDefault();
        if (defaultItem != null)
        {
            SelectedNavItem = defaultItem;
            CurrentContent = defaultItem.Content;
        }

        // Обновляем заголовок
        var roleText = authService.IsAdmin ? "Администратор" : "Кассир";
        var cashierName = authService.CurrentCashierId.HasValue
            ? dbService.GetCashierById(authService.CurrentCashierId.Value)?.FullName ?? ""
            : "";
        WindowTitle = $"Пункт обмена валюты — {roleText}: {authService.CurrentUserName} {cashierName}";

        System.Diagnostics.Debug.WriteLine($"=== MainWindowViewModel constructor END ===");
        System.Diagnostics.Debug.WriteLine($"=== CurrentContent type: {CurrentContent?.GetType().Name ?? "NULL"} ===");
        System.Diagnostics.Debug.WriteLine($"=== SelectedNavItem: {SelectedNavItem?.Title ?? "NULL"} ===");
        System.Diagnostics.Debug.WriteLine($"=== VisibleNavItems count: {VisibleNavItems.Count} ===");
    }

    private void ApplyRoleFilter()
    {
        VisibleNavItems.Clear();
        foreach (var item in AllNavItems)
        {
            if (!item.RequiresAdmin || _authService.IsAdmin)
            {
                VisibleNavItems.Add(item);
            }
        }
    }
}