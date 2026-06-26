using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KursMVVM.Models;

/// <summary>
/// Модель сделки по покупке/продаже валюты.
/// Каждая сделка обязательно связана с одним клиентом, одним кассиром
/// и двумя валютами (продаваемой и покупаемой).
/// </summary>
public class Deal : INotifyPropertyChanged
{
    private int _dealId;
    private string _soldCurrencyCode = string.Empty;
    private string? _soldCurrencyName;
    private string _boughtCurrencyCode = string.Empty;
    private string? _boughtCurrencyName;
    private int _cashierId;
    private string? _cashierName;
    private int _clientId;
    private string? _clientName;
    private DateTime _dealDate;
    private TimeSpan _dealTime;
    private double _soldAmount;
    private double _boughtAmount;

    /// <summary>Номер сделки (первичный ключ, автоинкремент)</summary>
    public int DealId
    {
        get => _dealId;
        set { _dealId = value; OnPropertyChanged(); }
    }

    /// <summary>Код продаваемой (отдаваемой) валюты</summary>
    public string SoldCurrencyCode
    {
        get => _soldCurrencyCode;
        set { _soldCurrencyCode = value; OnPropertyChanged(); }
    }

    /// <summary>Название продаваемой валюты (вычисляемое поле)</summary>
    public string? SoldCurrencyName
    {
        get => _soldCurrencyName;
        set { _soldCurrencyName = value; OnPropertyChanged(); }
    }

    /// <summary>Код покупаемой (получаемой) валюты</summary>
    public string BoughtCurrencyCode
    {
        get => _boughtCurrencyCode;
        set { _boughtCurrencyCode = value; OnPropertyChanged(); }
    }

    /// <summary>Название покупаемой валюты (вычисляемое поле)</summary>
    public string? BoughtCurrencyName
    {
        get => _boughtCurrencyName;
        set { _boughtCurrencyName = value; OnPropertyChanged(); }
    }

    /// <summary>Номер кассира (внешний ключ)</summary>
    public int CashierId
    {
        get => _cashierId;
        set { _cashierId = value; OnPropertyChanged(); }
    }

    /// <summary>Ф.И.О. кассира (вычисляемое поле)</summary>
    public string? CashierName
    {
        get => _cashierName;
        set { _cashierName = value; OnPropertyChanged(); }
    }

    /// <summary>Номер клиента (внешний ключ)</summary>
    public int ClientId
    {
        get => _clientId;
        set { _clientId = value; OnPropertyChanged(); }
    }

    /// <summary>Ф.И.О. клиента (вычисляемое поле)</summary>
    public string? ClientName
    {
        get => _clientName;
        set { _clientName = value; OnPropertyChanged(); }
    }

    /// <summary>Дата сделки (должна быть <= текущей даты)</summary>
    public DateTime DealDate
    {
        get => _dealDate;
        set { _dealDate = value; OnPropertyChanged(); }
    }

    /// <summary>Время сделки</summary>
    public TimeSpan DealTime
    {
        get => _dealTime;
        set { _dealTime = value; OnPropertyChanged(); }
    }

    /// <summary>Сумма проданной (отданной) валюты (> 0)</summary>
    public double SoldAmount
    {
        get => _soldAmount;
        set { _soldAmount = value; OnPropertyChanged(); }
    }

    /// <summary>Сумма купленной (полученной) валюты (> 0)</summary>
    public double BoughtAmount
    {
        get => _boughtAmount;
        set { _boughtAmount = value; OnPropertyChanged(); }
    }

    /// <summary>Отображаемая дата и время сделки</summary>
    public string FormattedDateTime => $"{DealDate:dd.MM.yyyy} {DealTime:hh\\:mm}";

    /// <summary>Форматированная сумма проданной валюты с названием</summary>
    public string FormattedSoldAmount => $"{SoldAmount:N2} {SoldCurrencyName ?? SoldCurrencyCode}";

    /// <summary>Форматированная сумма купленной валюты с названием</summary>
    public string FormattedBoughtAmount => $"{BoughtAmount:N2} {BoughtCurrencyName ?? BoughtCurrencyCode}";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override string ToString() =>
        $"Сделка #{DealId}: {ClientName} — {FormattedSoldAmount} → {FormattedBoughtAmount}";
}
