using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KursMVVM.Models;

/// <summary>
/// Модель клиента обменного пункта.
/// Клиент может совершать несколько сделок (1:N).
/// </summary>
public class Client : INotifyPropertyChanged
{
    private int _clientId;
    private string _fullName = string.Empty;
    private string _passportNumber = string.Empty;

    /// <summary>Номер клиента (первичный ключ, автоинкремент)</summary>
    public int ClientId
    {
        get => _clientId;
        set { _clientId = value; OnPropertyChanged(); }
    }

    /// <summary>Ф.И.О. клиента</summary>
    public string FullName
    {
        get => _fullName;
        set { _fullName = value; OnPropertyChanged(); }
    }

    /// <summary>Номер паспорта клиента (уникальный)</summary>
    public string PassportNumber
    {
        get => _passportNumber;
        set { _passportNumber = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override string ToString() => $"[{ClientId}] {FullName}";
}
