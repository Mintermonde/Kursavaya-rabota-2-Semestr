using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using KursMVVM.Models;
using KursMVVM.Services;
using ReactiveUI;

namespace KursMVVM.ViewModels;

/// <summary>
/// ViewModel окна авторизации
/// </summary>
public class LoginViewModel : ViewModelBase
{
    private readonly AuthService _authService;
    private readonly DataBaseService _dbService;
    private string _login = string.Empty;
    private string _password = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isRegisterMode;
    private string _newLogin = string.Empty;
    private string _newPassword = string.Empty;
    private string _confirmPassword = string.Empty;
    private Cashier? _selectedCashier;

    public string Login
    {
        get => _login;
        set => this.RaiseAndSetIfChanged(ref _login, value);
    }

    public string Password
    {
        get => _password;
        set => this.RaiseAndSetIfChanged(ref _password, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public bool IsRegisterMode
    {
        get => _isRegisterMode;
        set => this.RaiseAndSetIfChanged(ref _isRegisterMode, value);
    }

    public string NewLogin
    {
        get => _newLogin;
        set => this.RaiseAndSetIfChanged(ref _newLogin, value);
    }

    public string NewPassword
    {
        get => _newPassword;
        set => this.RaiseAndSetIfChanged(ref _newPassword, value);
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => this.RaiseAndSetIfChanged(ref _confirmPassword, value);
    }

    public Cashier? SelectedCashier
    {
        get => _selectedCashier;
        set => this.RaiseAndSetIfChanged(ref _selectedCashier, value);
    }

    public ObservableCollection<Cashier> Cashiers { get; } = new();

    public ReactiveCommand<Unit, Unit> LoginCommand { get; }
    public ReactiveCommand<Unit, Unit> RegisterCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleModeCommand { get; }

    public event EventHandler? LoginSuccess;

    public LoginViewModel(AuthService authService, DataBaseService dbService)
    {
        _authService = authService;
        _dbService = dbService;

        LoginCommand = ReactiveCommand.CreateFromTask(LoginAsync);
        RegisterCommand = ReactiveCommand.CreateFromTask(RegisterAsync);
        ToggleModeCommand = ReactiveCommand.Create(() =>
        {
            IsRegisterMode = !IsRegisterMode;
            StatusMessage = string.Empty;
        });

        LoadCashiersAsync();
    }

    private async void LoadCashiersAsync()
    {
        await Task.Run(() =>
        {
            var cashiers = _dbService.GetAllCashiers();
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                foreach (var c in cashiers) Cashiers.Add(c);
                if (Cashiers.Count > 0)
                    SelectedCashier = Cashiers[0];
            });
        });
    }

    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Login) || string.IsNullOrWhiteSpace(Password))
        {
            StatusMessage = "Введите логин и пароль";
            return;
        }

        var success = await Task.Run(() => _authService.Login(Login.Trim(), Password));
        if (success)
        {
            StatusMessage = "Успешный вход";
            // Переключаемся на UI-поток перед вызовом события
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                LoginSuccess?.Invoke(this, EventArgs.Empty);
            });
        }
        else
        {
            StatusMessage = "Неверный логин или пароль";
        }
    }

    private async Task RegisterAsync()
    {
        if (string.IsNullOrWhiteSpace(NewLogin) || string.IsNullOrWhiteSpace(NewPassword))
        {
            StatusMessage = "Заполните все поля";
            return;
        }

        if (NewPassword != ConfirmPassword)
        {
            StatusMessage = "Пароли не совпадают";
            return;
        }

        if (SelectedCashier is null)
        {
            StatusMessage = "Выберите кассира";
            return;
        }

        await Task.Run(() =>
        {
            try
            {
                _dbService.RegisterUser(NewLogin.Trim(), NewPassword, UserRole.Cashier, SelectedCashier.CashierId);
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    StatusMessage = "Кассир зарегистрирован. Войдите в систему.";
                    IsRegisterMode = false;
                    NewLogin = string.Empty;
                    NewPassword = string.Empty;
                    ConfirmPassword = string.Empty;
                });
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    StatusMessage = "Логин уже занят");
            }
            catch (Exception ex)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    StatusMessage = $"Ошибка: {ex.Message}");
            }
        });
    }
}
