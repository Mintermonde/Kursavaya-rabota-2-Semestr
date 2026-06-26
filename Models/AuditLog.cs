using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KursMVVM.Models;

/// <summary>
/// Запись журнала аудита — фиксирует все действия пользователей
/// </summary>
public class AuditLog : INotifyPropertyChanged
{
    private int _logId;
    private DateTime _timestamp;
    private string _username = string.Empty;
    private string _action = string.Empty;
    private string _details = string.Empty;

    /// <summary>ID записи</summary>
    public int LogId
    {
        get => _logId;
        set { _logId = value; OnPropertyChanged(); }
    }

    /// <summary>Дата и время действия</summary>
    public DateTime Timestamp
    {
        get => _timestamp;
        set { _timestamp = value; OnPropertyChanged(); }
    }

    /// <summary>Имя пользователя</summary>
    public string Username
    {
        get => _username;
        set { _username = value; OnPropertyChanged(); }
    }

    /// <summary>Тип действия (INSERT/UPDATE/DELETE/LOGIN/LOGOUT)</summary>
    public string Action
    {
        get => _action;
        set { _action = value; OnPropertyChanged(); }
    }

    /// <summary>Детали действия</summary>
    public string Details
    {
        get => _details;
        set { _details = value; OnPropertyChanged(); }
    }

    /// <summary>Отформатированная дата/время</summary>
    public string FormattedTimestamp => Timestamp.ToString("dd.MM.yyyy HH:mm:ss");

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
