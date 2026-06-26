using KursMVVM.Models;
using System;

namespace KursMVVM.Services;

/// <summary>
/// Сервис аутентификации и авторизации.
/// Хранит текущего пользователя и управляет доступом.
/// </summary>
public class AuthService
{
    private readonly DataBaseService _dbService;

    /// <summary>Текущий авторизованный пользователь (null если не вошел)</summary>
    public User? CurrentUser { get; private set; }

    /// <summary>Проверка, вошел ли администратор</summary>
    public bool IsAdmin => CurrentUser?.Role == UserRole.Administrator;

    /// <summary>Проверка, вошел ли кассир</summary>
    public bool IsCashier => CurrentUser?.Role == UserRole.Cashier;

    /// <summary>ID кассира текущего пользователя (null для админа)</summary>
    public int? CurrentCashierId => CurrentUser?.CashierId;

    public string CurrentUserName => CurrentUser?.Login ?? "Гость";

    public AuthService(DataBaseService dbService)
    {
        _dbService = dbService;
    }

    /// <summary>Авторизация пользователя</summary>
    public bool Login(string login, string password)
    {
        var user = _dbService.AuthenticateUser(login, password);
        if (user is null) return false;

        CurrentUser = user;
        _dbService.LogAudit(login, "LOGIN", $"Вход в систему, роль: {user.Role}");
        return true;
    }

    /// <summary>Выход из системы</summary>
    public event EventHandler? LoggedOut;

    public void Logout()
    {
        CurrentUser = null;
        LoggedOut?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Регистрация нового кассира</summary>
    public bool RegisterCashier(string login, string password, int cashierId)
    {
        try
        {
            _dbService.RegisterUser(login, password, UserRole.Cashier, cashierId);
            _dbService.LogAudit(CurrentUser?.Login ?? "system", "REGISTER", $"Регистрация кассира: {login}");
            return true;
        }
        catch
        {
            return false;
        }
    }
}
