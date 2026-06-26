using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using KursMVVM.Services;
using KursMVVM.ViewModels;
using KursMVVM.Views;
using System;

namespace KursMVVM;

public partial class App : Application
{
    private DataBaseService? _dbService;
    private AuthService? _authService;
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private LoginWindow? _loginWindow;

    public override void Initialize()
    {
        // Регистрируем кодировки перед загрузкой XAML
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;
            _dbService = new DataBaseService("kurs.db");
            _authService = new AuthService(_dbService);

            ShowLoginWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ShowLoginWindow()
    {
        if (_desktop is null || _dbService is null || _authService is null) return;

        // Если окно логина уже есть — показываем его
        if (_loginWindow != null)
        {
            _loginWindow.Show();
            _desktop.MainWindow = _loginWindow;
            return;
        }

        // Создаём новое окно логина
        _loginWindow = new LoginWindow
        {
            DataContext = new LoginViewModel(_authService, _dbService)
        };

        var loginVm = (LoginViewModel)_loginWindow.DataContext;
        loginVm.LoginSuccess += (s, e) =>
        {
            ShowMainWindow();
        };

        _desktop.MainWindow = _loginWindow;
        _loginWindow.Show();
    }

    private void ShowMainWindow()
    {
        if (_desktop is null || _dbService is null || _authService is null) return;

        try
        {
            var mainVm = new MainWindowViewModel(_dbService, _authService);
            var mainWindow = new MainWindow
            {
                DataContext = mainVm
            };

            mainVm.RequestLogout += (s, e) =>
            {
                // Скрываем MainWindow вместо закрытия
                mainWindow.Hide();

                // Сбрасываем авторизацию
                _authService?.Logout();

                // Показываем логин снова
                ShowLoginWindow();
            };

            mainVm.RequestExit += (s, e) =>
            {
                // Полный выход — закрываем всё
                _loginWindow?.Close();
                mainWindow.Close();
                _desktop?.Shutdown();
            };

            // НЕ закрываем LoginWindow, а скрываем
            _loginWindow?.Hide();

            mainWindow.Show();
            _desktop.MainWindow = mainWindow;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR: {ex}");
            throw;
        }
    }
}