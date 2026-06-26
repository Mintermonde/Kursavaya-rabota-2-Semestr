using Avalonia.Controls;
using KursMVVM.ViewModels;
using System;

namespace KursMVVM.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainWindowViewModel vm)
        {
            vm.RequestLogout += OnLogout;
            vm.RequestExit += OnExit;
        }
    }

    private void OnLogout(object? sender, EventArgs e)
    {
        // Ничего не делаем — всё обрабатывается в App.axaml.cs
    }

    private void OnExit(object? sender, EventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.RequestLogout -= OnLogout;
            vm.RequestExit -= OnExit;
        }
        base.OnClosed(e);
    }
}