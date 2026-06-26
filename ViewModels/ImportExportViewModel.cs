using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CsvHelper;
using CsvHelper.Configuration;
using KursMVVM.Models;
using KursMVVM.Services;
using OfficeOpenXml;
using ReactiveUI;

namespace KursMVVM.ViewModels;

public class ImportExportViewModel : ViewModelBase
{
    private readonly DataBaseService _dbService;
    private readonly AuthService _authService;
    private string _statusMessage = string.Empty;
    private string _logText = string.Empty;
    private string _selectedFilePath = string.Empty;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public string LogText
    {
        get => _logText;
        private set => this.RaiseAndSetIfChanged(ref _logText, value);
    }

    public string SelectedFilePath
    {
        get => _selectedFilePath;
        private set => this.RaiseAndSetIfChanged(ref _selectedFilePath, value);
    }

    public ReactiveCommand<Unit, Unit> PickImportFileCommand { get; }
    public ReactiveCommand<string, Unit> ImportCommand { get; }
    public ReactiveCommand<string, Unit> ExportCommand { get; }

    public ImportExportViewModel(DataBaseService dbService, AuthService authService)
    {
        _dbService = dbService;
        _authService = authService;
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        PickImportFileCommand = ReactiveCommand.CreateFromTask(PickImportFileAsync);
        ImportCommand = ReactiveCommand.CreateFromTask<string>(ImportAsync);
        ExportCommand = ReactiveCommand.CreateFromTask<string>(ExportAsync);
    }

    private void Log(string message)
    {
        LogText += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
    }

    private async Task PickImportFileAsync()
    {
        var file = await ShowOpenFileDialogAsync("Выберите файл для импорта",
            new[] { "json", "xml", "csv", "xlsx" });
        if (file != null)
        {
            SelectedFilePath = file;
            var ext = Path.GetExtension(file).ToLowerInvariant();
            StatusMessage = $"Выбран файл: {Path.GetFileName(file)}";
        }
    }

    private async Task<string?> ShowOpenFileDialogAsync(string title, string[] extensions)
    {
        var tcs = new TaskCompletionSource<string?>();

        Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime
                && lifetime.MainWindow != null)
            {
                var filters = extensions.Select(ext => new FilePickerFileType($"*.{ext}")
                {
                    Patterns = new[] { $"*.{ext}" }
                }).ToList();
                filters.Add(new FilePickerFileType("Все файлы") { Patterns = new[] { "*" } });

                var options = new FilePickerOpenOptions
                {
                    Title = title,
                    AllowMultiple = false,
                    FileTypeFilter = filters
                };

                var results = await lifetime.MainWindow.StorageProvider.OpenFilePickerAsync(options);
                tcs.TrySetResult(results.Count > 0 ? results[0].Path.LocalPath : null);
            }
            else
            {
                tcs.TrySetResult(null);
            }
        });

        return await tcs.Task;
    }

    private async Task<string?> ShowSaveFileDialogAsync(string title, string defaultExt, string suggestedName)
    {
        var tcs = new TaskCompletionSource<string?>();

        Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime
                && lifetime.MainWindow != null)
            {
                var options = new FilePickerSaveOptions
                {
                    Title = title,
                    DefaultExtension = defaultExt,
                    SuggestedFileName = suggestedName,
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType($"*.{defaultExt.TrimStart('.')}")
                        {
                            Patterns = new[] { $"*.{defaultExt.TrimStart('.')}" }
                        }
                    }
                };

                var result = await lifetime.MainWindow.StorageProvider.SaveFilePickerAsync(options);
                tcs.TrySetResult(result?.Path.LocalPath);
            }
            else
            {
                tcs.TrySetResult(null);
            }
        });

        return await tcs.Task;
    }

    private async Task ImportAsync(string format)
    {
        string? filePath;

        if (!string.IsNullOrEmpty(SelectedFilePath) &&
            Path.GetExtension(SelectedFilePath).Equals($".{format}", StringComparison.OrdinalIgnoreCase))
        {
            filePath = SelectedFilePath;
        }
        else
        {
            filePath = await ShowOpenFileDialogAsync($"Выберите .{format} файл",
                new[] { format, format == "xlsx" ? "xls" : format });
        }

        if (filePath is null) { StatusMessage = "Файл не выбран"; return; }

        StatusMessage = $"Импорт {format.ToUpper()}...";

        try
        {
            switch (format.ToLower())
            {
                case "json": await ImportJsonAsync(filePath); break;
                case "xml": await ImportXmlAsync(filePath); break;
                case "csv": await ImportCsvAsync(filePath); break;
                case "xlsx": await ImportExcelAsync(filePath); break;
            }
            _dbService.LogAudit(_authService.CurrentUserName, "IMPORT", $"Импорт {format}: {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка импорта: {ex.Message}";
            Log($"Ошибка импорта {format}: {ex.Message}");
        }
    }

    private async Task ExportAsync(string format)
    {
        var filePath = await ShowSaveFileDialogAsync($"Экспорт {format.ToUpper()}",
            $".{(format == "excel" ? "xlsx" : format)}",
            $"export.{(format == "excel" ? "xlsx" : format)}");

        if (filePath is null) { StatusMessage = "Файл не выбран"; return; }

        StatusMessage = $"Экспорт {format.ToUpper()}...";

        try
        {
            switch (format.ToLower())
            {
                case "json": await ExportJsonAsync(filePath); break;
                case "xml": await ExportXmlAsync(filePath); break;
                case "csv": await ExportCsvAsync(filePath); break;
                case "excel": await ExportExcelAsync(filePath); break;
            }
            _dbService.LogAudit(_authService.CurrentUserName, "EXPORT", $"Экспорт {format}: {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка экспорта: {ex.Message}";
            Log($"Ошибка экспорта {format}: {ex.Message}");
        }
    }

    #region Import implementations

    private async Task ImportJsonAsync(string filePath)
    {
        await Task.Run(() =>
        {
            var json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<JsonImportData>(json);
            if (data is null) { Log("Ошибка: пустой JSON"); return; }

            var clients = data.клиенты?.Select(c => new Client
            {
                ClientId = c.номер_клиента,
                FullName = c.фио,
                PassportNumber = c.номер_паспорта
            }).ToList() ?? new();

            var currencies = data.валюты?.Select(v => new Currency
            {
                Code = v.код_валюты,
                Name = v.название_валюты,
                SellRate = v.курс_продажи,
                BuyRate = v.курс_покупки
            }).ToList() ?? new();

            var cashiers = data.кассиры?.Select(k => new Cashier
            {
                CashierId = k.номер_кассира,
                FullName = k.фио
            }).ToList() ?? new();

            var deals = data.сделки?.Select(s => new Deal
            {
                SoldCurrencyCode = s.код_проданной_валюты,
                BoughtCurrencyCode = s.код_купленной_валюты,
                CashierId = s.номер_кассира,
                ClientId = s.номер_клиента,
                DealDate = DateTime.Parse(s.дата_сделки),
                DealTime = TimeSpan.Parse(s.время_сделки),
                SoldAmount = s.сумма_проданной_валюты,
                BoughtAmount = s.сумма_купленной_валюты
            }).ToList() ?? new();

            _dbService.BulkImport(clients, currencies, cashiers, deals);
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                StatusMessage = $"JSON: {clients.Count} клиентов, {currencies.Count} валют, {cashiers.Count} кассиров, {deals.Count} сделок");
            Log($"Импорт JSON завершен: {filePath}");
        });
    }

    private async Task ImportXmlAsync(string filePath)
    {
        await Task.Run(() =>
        {
            var doc = XDocument.Load(filePath);
            var clients = new List<Client>();
            var currencies = new List<Currency>();
            var cashiers = new List<Cashier>();
            var deals = new List<Deal>();

            foreach (var elem in doc.Descendants("клиент"))
                clients.Add(new Client
                {
                    ClientId = (int)elem.Element("номер_клиента")!,
                    FullName = (string)elem.Element("фио")!,
                    PassportNumber = (string)elem.Element("номер_паспорта")!
                });

            foreach (var elem in doc.Descendants("валюта"))
                currencies.Add(new Currency
                {
                    Code = (string)elem.Element("код_валюты")!,
                    Name = (string)elem.Element("название_валюты")!,
                    SellRate = (double)elem.Element("курс_продажи")!,
                    BuyRate = (double)elem.Element("курс_покупки")!
                });

            foreach (var elem in doc.Descendants("кассир"))
                cashiers.Add(new Cashier { CashierId = (int)elem.Element("номер_кассира")!, FullName = (string)elem.Element("фио")! });

            foreach (var elem in doc.Descendants("сделка"))
                deals.Add(new Deal
                {
                    SoldCurrencyCode = (string)elem.Element("код_проданной_валюты")!,
                    BoughtCurrencyCode = (string)elem.Element("код_купленной_валюты")!,
                    CashierId = (int)elem.Element("номер_кассира")!,
                    ClientId = (int)elem.Element("номер_клиента")!,
                    DealDate = DateTime.Parse((string)elem.Element("дата_сделки")!),
                    DealTime = TimeSpan.Parse((string)elem.Element("время_сделки")!),
                    SoldAmount = (double)elem.Element("сумма_проданной_валюты")!,
                    BoughtAmount = (double)elem.Element("сумма_купленной_валюты")!
                });

            _dbService.BulkImport(clients, currencies, cashiers, deals);
            Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusMessage = "XML импортирован");
            Log($"Импорт XML: {filePath}");
        });
    }

    private async Task ImportCsvAsync(string filePath)
    {
        await Task.Run(() =>
        {
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture));
            var records = csv.GetRecords<dynamic>();
            int count = 0;

            foreach (var record in records)
            {
                var dict = (IDictionary<string, object>)record;
                if (dict.ContainsKey("фио"))
                {
                    var client = new Client
                    {
                        FullName = dict["фио"]?.ToString() ?? "",
                        PassportNumber = dict.ContainsKey("номер_паспорта") ? dict["номер_паспорта"]?.ToString() ?? "" : ""
                    };
                    _dbService.AddClient(client);
                    count++;
                }
            }
            Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusMessage = $"CSV: импортировано {count} записей");
            Log($"Импорт CSV: {count} записей");
        });
    }

    private async Task ImportExcelAsync(string filePath)
    {
        await Task.Run(() =>
        {
            using var package = new ExcelPackage(new FileInfo(filePath));
            var ws = package.Workbook.Worksheets["Сделки"];
            if (ws is null) { Log("Ошибка: лист 'Сделки' не найден"); return; }

            int count = 0;
            for (int row = 2; row <= ws.Dimension.End.Row; row++)
            {
                var deal = new Deal
                {
                    SoldCurrencyCode = ws.Cells[row, 1].Text,
                    BoughtCurrencyCode = ws.Cells[row, 2].Text,
                    CashierId = int.Parse(ws.Cells[row, 3].Text),
                    ClientId = int.Parse(ws.Cells[row, 4].Text),
                    DealDate = ws.Cells[row, 5].GetValue<DateTime>(),
                    DealTime = ws.Cells[row, 6].GetValue<TimeSpan>(),
                    SoldAmount = double.Parse(ws.Cells[row, 7].Text),
                    BoughtAmount = double.Parse(ws.Cells[row, 8].Text)
                };
                _dbService.AddDeal(deal);
                count++;
            }
            Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusMessage = $"Excel: импортировано {count} сделок");
            Log($"Импорт Excel: {count} сделок");
        });
    }

    #endregion

    #region Export implementations

    private async Task ExportJsonAsync(string filePath)
    {
        await Task.Run(() =>
        {
            var data = new
            {
                клиенты = _dbService.GetAllClients().Select(c => new { номер_клиента = c.ClientId, фио = c.FullName, номер_паспорта = c.PassportNumber }),
                валюты = _dbService.GetAllCurrencies().Select(v => new { код_валюты = v.Code, название_валюты = v.Name, курс_продажи = v.SellRate, курс_покупки = v.BuyRate }),
                кассиры = _dbService.GetAllCashiers().Select(k => new { номер_кассира = k.CashierId, фио = k.FullName }),
                сделки = _dbService.GetAllDeals().Select(s => new
                {
                    код_проданной_валюты = s.SoldCurrencyCode,
                    код_купленной_валюты = s.BoughtCurrencyCode,
                    номер_кассира = s.CashierId,
                    номер_клиента = s.ClientId,
                    дата_сделки = s.DealDate.ToString("yyyy-MM-dd"),
                    время_сделки = s.DealTime.ToString(),
                    сумма_проданной_валюты = s.SoldAmount,
                    сумма_купленной_валюты = s.BoughtAmount
                })
            };

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
            Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusMessage = "JSON экспортирован");
            Log($"Экспорт JSON: {filePath}");
        });
    }

    private async Task ExportXmlAsync(string filePath)
    {
        await Task.Run(() =>
        {
            var root = new XElement("обменный_пункт");

            var clientsEl = new XElement("клиенты");
            foreach (var c in _dbService.GetAllClients())
                clientsEl.Add(new XElement("клиент",
                    new XElement("номер_клиента", c.ClientId),
                    new XElement("фио", c.FullName),
                    new XElement("номер_паспорта", c.PassportNumber)));
            root.Add(clientsEl);

            var currenciesEl = new XElement("валюты");
            foreach (var v in _dbService.GetAllCurrencies())
                currenciesEl.Add(new XElement("валюта",
                    new XElement("код_валюты", v.Code),
                    new XElement("название_валюты", v.Name),
                    new XElement("курс_продажи", v.SellRate),
                    new XElement("курс_покупки", v.BuyRate)));
            root.Add(currenciesEl);

            var cashiersEl = new XElement("кассиры");
            foreach (var k in _dbService.GetAllCashiers())
                cashiersEl.Add(new XElement("кассир",
                    new XElement("номер_кассира", k.CashierId),
                    new XElement("фио", k.FullName)));
            root.Add(cashiersEl);

            var dealsEl = new XElement("сделки");
            foreach (var s in _dbService.GetAllDeals())
                dealsEl.Add(new XElement("сделка",
                    new XElement("код_проданной_валюты", s.SoldCurrencyCode),
                    new XElement("код_купленной_валюты", s.BoughtCurrencyCode),
                    new XElement("номер_кассира", s.CashierId),
                    new XElement("номер_клиента", s.ClientId),
                    new XElement("дата_сделки", s.DealDate.ToString("yyyy-MM-dd")),
                    new XElement("время_сделки", s.DealTime.ToString()),
                    new XElement("сумма_проданной_валюты", s.SoldAmount),
                    new XElement("сумма_купленной_валюты", s.BoughtAmount)));
            root.Add(dealsEl);

            new XDocument(root).Save(filePath);
            Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusMessage = "XML экспортирован");
            Log($"Экспорт XML: {filePath}");
        });
    }

    private async Task ExportCsvAsync(string filePath)
    {
        await Task.Run(() =>
        {
            using var writer = new StreamWriter(filePath);
            using var csv = new CsvWriter(writer, new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture));
            csv.WriteRecords(_dbService.GetAllClients().Select(c => new { номер_клиента = c.ClientId, фио = c.FullName, номер_паспорта = c.PassportNumber }));
            Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusMessage = "CSV экспортирован");
            Log($"Экспорт CSV: {filePath}");
        });
    }

    private async Task ExportExcelAsync(string filePath)
    {
        await Task.Run(() =>
        {
            using var package = new ExcelPackage();

            var ws = package.Workbook.Worksheets.Add("Сделки");
            var deals = _dbService.GetAllDeals();
            ws.Cells[1, 1].Value = "Код проданной";
            ws.Cells[1, 2].Value = "Код купленной";
            ws.Cells[1, 3].Value = "№ кассира";
            ws.Cells[1, 4].Value = "№ клиента";
            ws.Cells[1, 5].Value = "Дата";
            ws.Cells[1, 6].Value = "Время";
            ws.Cells[1, 7].Value = "Сумма проданной";
            ws.Cells[1, 8].Value = "Сумма купленной";

            for (int i = 0; i < deals.Count; i++)
            {
                ws.Cells[i + 2, 1].Value = deals[i].SoldCurrencyCode;
                ws.Cells[i + 2, 2].Value = deals[i].BoughtCurrencyCode;
                ws.Cells[i + 2, 3].Value = deals[i].CashierId;
                ws.Cells[i + 2, 4].Value = deals[i].ClientId;
                ws.Cells[i + 2, 5].Value = deals[i].DealDate;
                ws.Cells[i + 2, 6].Value = deals[i].DealTime.ToString();
                ws.Cells[i + 2, 7].Value = deals[i].SoldAmount;
                ws.Cells[i + 2, 8].Value = deals[i].BoughtAmount;
            }
            ws.Cells[ws.Dimension.Address].AutoFitColumns();

            var ws2 = package.Workbook.Worksheets.Add("Клиенты");
            ws2.Cells[1, 1].Value = "№";
            ws2.Cells[1, 2].Value = "ФИО";
            ws2.Cells[1, 3].Value = "Паспорт";
            var clients = _dbService.GetAllClients();
            for (int i = 0; i < clients.Count; i++)
            {
                ws2.Cells[i + 2, 1].Value = clients[i].ClientId;
                ws2.Cells[i + 2, 2].Value = clients[i].FullName;
                ws2.Cells[i + 2, 3].Value = clients[i].PassportNumber;
            }
            ws2.Cells[ws2.Dimension.Address].AutoFitColumns();

            package.SaveAs(new FileInfo(filePath));
            Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusMessage = "Excel экспортирован");
            Log($"Экспорт Excel: {filePath}");
        });
    }

    #endregion

    private class JsonImportData
    {
        public List<JsonClient> клиенты { get; set; } = new();
        public List<JsonCurrency> валюты { get; set; } = new();
        public List<JsonCashier> кассиры { get; set; } = new();
        public List<JsonDeal> сделки { get; set; } = new();
    }

    private class JsonClient { public int номер_клиента { get; set; } public string фио { get; set; } = ""; public string номер_паспорта { get; set; } = ""; }
    private class JsonCurrency { public string код_валюты { get; set; } = ""; public string название_валюты { get; set; } = ""; public double курс_продажи { get; set; } public double курс_покупки { get; set; } }
    private class JsonCashier { public int номер_кассира { get; set; } public string фио { get; set; } = ""; }
    private class JsonDeal { public string код_проданной_валюты { get; set; } = ""; public string код_купленной_валюты { get; set; } = ""; public int номер_кассира { get; set; } public int номер_клиента { get; set; } public string дата_сделки { get; set; } = ""; public string время_сделки { get; set; } = ""; public double сумма_проданной_валюты { get; set; } public double сумма_купленной_валюты { get; set; } }
}
