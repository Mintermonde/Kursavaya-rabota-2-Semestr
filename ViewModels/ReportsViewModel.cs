using System;
using System.Collections.Generic;
using System.Data;
using System.Reactive;
using System.Threading.Tasks;
using KursMVVM.Services;
using ReactiveUI;

namespace KursMVVM.ViewModels;

public class ReportsViewModel : ViewModelBase
{
    private readonly DataBaseService _dbService;
    private readonly AuthService _authService;
    private List<string> _reportLines = new();
    private List<string> _reportColumns = new();
    private int _selectedReportIndex;
    private string _reportTitle = "Выберите отчет";
    private DateTimeOffset _fromDate = DateTimeOffset.Now.AddMonths(-1);
    private DateTimeOffset _toDate = DateTimeOffset.Now;
    private bool _isDateRangeVisible;
    private string _statusMessage = string.Empty;

    public List<string> ReportLines
    {
        get => _reportLines;
        private set => this.RaiseAndSetIfChanged(ref _reportLines, value);
    }

    public List<string> ReportColumns
    {
        get => _reportColumns;
        private set => this.RaiseAndSetIfChanged(ref _reportColumns, value);
    }

    public int SelectedReportIndex
    {
        get => _selectedReportIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedReportIndex, value);
            IsDateRangeVisible = value == 4;
            UpdateReportTitle(value);
        }
    }

    public string ReportTitle
    {
        get => _reportTitle;
        private set => this.RaiseAndSetIfChanged(ref _reportTitle, value);
    }

    public DateTimeOffset FromDate
    {
        get => _fromDate;
        set => this.RaiseAndSetIfChanged(ref _fromDate, value);
    }

    public DateTimeOffset ToDate
    {
        get => _toDate;
        set => this.RaiseAndSetIfChanged(ref _toDate, value);
    }

    public bool IsDateRangeVisible
    {
        get => _isDateRangeVisible;
        private set => this.RaiseAndSetIfChanged(ref _isDateRangeVisible, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public string[] ReportNames { get; } = new[]
    {
        "1. Сводка по клиентам",
        "2. Сводка по валютам",
        "3. Сводка по кассирам",
        "4. Неактивные клиенты",
        "5. Прибыль пункта обмена"
    };

    public ReactiveCommand<Unit, Unit> GenerateReportCommand { get; }

    public ReportsViewModel(DataBaseService dbService, AuthService authService)
    {
        _dbService = dbService;
        _authService = authService;
        GenerateReportCommand = ReactiveCommand.CreateFromTask(GenerateReportAsync);
    }

    private void UpdateReportTitle(int index)
    {
        if (index >= 0 && index < ReportNames.Length)
            ReportTitle = ReportNames[index];
    }

    private async Task GenerateReportAsync()
    {
        StatusMessage = "Формирование отчета...";
        await Task.Run(() =>
        {
            DataTable data = SelectedReportIndex switch
            {
                0 => _dbService.GetClientReport(),
                1 => _dbService.GetCurrencyReport(),
                2 => _dbService.GetCashierReport(),
                3 => _dbService.GetInactiveClientsReport(),
                4 => _dbService.GetProfitReport(FromDate.DateTime, ToDate.DateTime),
                _ => new DataTable()
            };

            var columns = new List<string>();
            foreach (DataColumn col in data.Columns)
            {
                columns.Add(col.ColumnName);
            }
            ReportColumns = columns;

            var lines = new List<string>();
            foreach (DataRow row in data.Rows)
            {
                var values = new List<string>();
                foreach (DataColumn col in data.Columns)
                {
                    values.Add(row[col]?.ToString() ?? "");
                }
                lines.Add(string.Join(" | ", values));
            }

            ReportLines = lines;

            _dbService.LogAudit(_authService.CurrentUserName, "REPORT",
                $"Сформирован отчет: {ReportNames[SelectedReportIndex]}");
        });
        StatusMessage = $"Отчет сформирован: {ReportLines.Count} строк";
    }
}