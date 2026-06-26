using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KursMVVM.Models;

/// <summary>
/// Роль пользователя в системе
/// </summary>
public enum UserRole
{
    Administrator,
    Cashier
}

/// <summary>
/// Модель пользователя системы (администратор или кассир)
/// </summary>
public class User : INotifyPropertyChanged
{
    private int _userId;
    private string _login = string.Empty;
    private string _passwordHash = string.Empty;
    private UserRole _role;
    private int? _cashierId;

    /// <summary>ID пользователя</summary>
    public int UserId
    {
        get => _userId;
        set { _userId = value; OnPropertyChanged(); }
    }

    /// <summary>Логин пользователя (уникальный)</summary>
    public string Login
    {
        get => _login;
        set { _login = value; OnPropertyChanged(); }
    }

    /// <summary>Хэш пароля (BCrypt)</summary>
    public string PasswordHash
    {
        get => _passwordHash;
        set { _passwordHash = value; OnPropertyChanged(); }
    }

    /// <summary>Роль пользователя</summary>
    public UserRole Role
    {
        get => _role;
        set { _role = value; OnPropertyChanged(); }
    }

    /// <summary>Связь с кассиром (null для администратора)</summary>
    public int? CashierId
    {
        get => _cashierId;
        set { _cashierId = value; OnPropertyChanged(); }
    }

    /// <summary>Отображаемая роль</summary>
    public string RoleDisplay => Role == UserRole.Administrator ? "Администратор" : "Кассир";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override string ToString() => $"[{Login}] {RoleDisplay}";
}
