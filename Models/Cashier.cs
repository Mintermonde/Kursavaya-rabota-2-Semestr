using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KursMVVM.Models;

/// <summary>
/// Модель кассира обменного пункта.
/// Кассир может обслуживать несколько сделок (1:N).
/// </summary>
public class Cashier : INotifyPropertyChanged
{
    private int _cashierId;
    private string _fullName = string.Empty;

    /// <summary>Номер кассира (первичный ключ, автоинкремент)</summary>
    public int CashierId
    {
        get => _cashierId;
        set { _cashierId = value; OnPropertyChanged(); }
    }

    /// <summary>Ф.И.О. кассира</summary>
    public string FullName
    {
        get => _fullName;
        set { _fullName = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override string ToString() => $"[{CashierId}] {FullName}";
}
