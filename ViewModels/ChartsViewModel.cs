using KursMVVM.Services;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using ReactiveUI;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;

namespace KursMVVM.ViewModels;

public class ChartsViewModel : ViewModelBase
{
    private readonly DataBaseService _dbService;
    private int _selectedChartIndex;

    public int SelectedChartIndex
    {
        get => _selectedChartIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedChartIndex, value);
        }
    }

    public string[] ChartNames { get; } = new[]
    {
        "1. Объем операций по валютам",
        "2. Доля сделок по кассирам",
        "3. Динамика объема по датам",
        "4. Распределение сумм сделок"
    };

    public ObservableCollection<ISeries> BarSeries { get; } = new();
    public ObservableCollection<ISeries> PieSeries { get; } = new();
    public ObservableCollection<ISeries> LineSeries { get; } = new();
    public ObservableCollection<ISeries> HistogramSeries { get; } = new();

    public ObservableCollection<Axis> BarXAxes { get; } = new();
    public ObservableCollection<Axis> BarYAxes { get; } = new();
    public ObservableCollection<Axis> LineXAxes { get; } = new();
    public ObservableCollection<Axis> LineYAxes { get; } = new();
    public ObservableCollection<Axis> HistogramXAxes { get; } = new();
    public ObservableCollection<Axis> HistogramYAxes { get; } = new();

    public ReactiveCommand<Unit, Unit> LoadChartsCommand { get; }

    public ChartsViewModel(DataBaseService dbService)
    {
        _dbService = dbService;

        // Инициализация "заглушек" осей — без этого LiveCharts падает при создании
        BarXAxes.Add(new Axis());
        BarYAxes.Add(new Axis());
        LineXAxes.Add(new Axis());
        LineYAxes.Add(new Axis());
        HistogramXAxes.Add(new Axis());
        HistogramYAxes.Add(new Axis());

        LoadChartsCommand = ReactiveCommand.CreateFromTask(() => LoadChartAsync(SelectedChartIndex));
    }

    private async Task LoadChartAsync(int chartIndex)
    {
        await Task.Run(() =>
        {
            switch (chartIndex)
            {
                case 0: LoadBarChart(); break;
                case 1: LoadPieChart(); break;
                case 2: LoadLineChart(); break;
                case 3: LoadHistogram(); break;
            }
        });
    }

    private void LoadBarChart()
    {
        var data = _dbService.GetCurrencyVolumeData();
        var labels = data.Select(d => d.Currency).ToArray();
        var values = data.Select(d => d.Volume).ToArray();

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            BarSeries.Clear();
            BarXAxes.Clear();
            BarYAxes.Clear();

            BarSeries.Add(new ColumnSeries<double>
            {
                Values = values,
                Name = "Объем (руб.)",
                Fill = new SolidColorPaint(SKColor.Parse("#2980B9")),
                DataLabelsPaint = new SolidColorPaint(SKColor.Parse("#333")),
                DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                DataLabelsFormatter = point => $"{point.Coordinate:N0}"  // ← N0 = без десятичных
            });

            BarXAxes.Add(new Axis { Labels = labels, LabelsRotation = 45, TextSize = 12 });
            BarYAxes.Add(new Axis { Name = "Объем, руб.", TextSize = 12, Labeler = value => $"{value:N0}" });  // ← и тут N0
        });
    }

    private void LoadPieChart()
    {
        var data = _dbService.GetCashierDealData();
        var colors = new[] { "#2980B9", "#27AE60", "#F39C12", "#E74C3C", "#8E44AD", "#1ABC9C" };

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            PieSeries.Clear();
            for (int i = 0; i < data.Count; i++)
            {
                PieSeries.Add(new PieSeries<int>
                {
                    Values = new[] { data[i].DealCount },
                    Name = $"{data[i].Cashier} ({data[i].DealCount})",
                    Fill = new SolidColorPaint(SKColor.Parse(colors[i % colors.Length])),
                    DataLabelsPaint = null,  // ← убраны подписи с сегментов
                    DataLabelsPosition = PolarLabelsPosition.ChartCenter,
                });
            }
        });
    }

    private void LoadLineChart()
    {
        var data = _dbService.GetDailyVolumeData();
        var labels = data.Select(d => d.Date.ToString("dd.MM")).ToArray();
        var values = data.Select(d => d.Volume).ToArray();

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            LineSeries.Clear();
            LineXAxes.Clear();
            LineYAxes.Clear();

            LineSeries.Add(new LineSeries<double>
            {
                Values = values,
                Name = "Объем (руб.)",
                Fill = new SolidColorPaint(SKColor.Parse("#2980B9").WithAlpha(30)),
                Stroke = new SolidColorPaint(SKColor.Parse("#2980B9"), 3),
                GeometryFill = new SolidColorPaint(SKColor.Parse("#2980B9")),
                GeometryStroke = new SolidColorPaint(SKColor.Parse("#FFF"), 2),
                GeometrySize = 10
            });

            LineXAxes.Add(new Axis { Labels = labels, TextSize = 11, LabelsRotation = 30 });
            LineYAxes.Add(new Axis { Name = "Объем, руб.", TextSize = 12, Labeler = value => $"{value:N0}" });
        });
    }

    private void LoadHistogram()
    {
        var data = _dbService.GetAmountDistributionData();
        var labels = data.Select(d => d.Range).ToArray();
        var values = data.Select(d => d.Count).ToArray();

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            HistogramSeries.Clear();
            HistogramXAxes.Clear();
            HistogramYAxes.Clear();

            HistogramSeries.Add(new ColumnSeries<int>
            {
                Values = values,
                Name = "Кол-во сделок",
                Fill = new SolidColorPaint(SKColor.Parse("#27AE60")),
                DataLabelsPaint = new SolidColorPaint(SKColor.Parse("#333")),
                DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                DataLabelsFormatter = point => $"{point.Coordinate:N0}"  // ← N0
            });

            HistogramXAxes.Add(new Axis { Labels = labels, TextSize = 12 });
            HistogramYAxes.Add(new Axis { Name = "Кол-во", TextSize = 12, Labeler = value => $"{value:N0}" });
        });
    }
}