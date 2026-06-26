using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KursMVVM.Models;

/// <summary>
/// Модель валюты.
/// Любая валюта может участвовать в нескольких сделках (1:N).
/// </summary>
public class Currency : INotifyPropertyChanged
{
    private string _code = string.Empty;
    private string _name = string.Empty;
    private double _sellRate;
    private double _buyRate;

    /// <summary>Код валюты (первичный ключ, например: USD, EUR)</summary>
    public string Code
    {
        get => _code;
        set { _code = value; OnPropertyChanged(); }
    }

    /// <summary>Название валюты (например: Доллар США)</summary>
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    /// <summary>Курс продажи в рублях (сколько рублей получаем за 1 единицу валюты)</summary>
    public double SellRate
    {
        get => _sellRate;
        set { _sellRate = value; OnPropertyChanged(); }
    }

    /// <summary>Курс покупки в рублях (сколько рублей отдаем за 1 единицу валюты)</summary>
    public double BuyRate
    {
        get => _buyRate;
        set { _buyRate = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override string ToString() => $"[{Code}] {Name}";
}
