using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using KursMVVM.ViewModels;

namespace KursMVVM;

/// <summary>
/// Локатор представлений для MVVM.
/// Автоматически сопоставляет ViewModel с соответствующим View
/// по соглашению об именовании (MyViewModel -> MyView).
/// </summary>
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? data)
    {
        if (data is null)
            return null;

        var viewModelName = data.GetType().Name;

        // Убираем суффикс "ViewModel" и добавляем "View"
        var viewName = viewModelName.Replace("ViewModel", "View");
        var fullTypeName = $"KursMVVM.Views.{viewName}";

        var type = Type.GetType(fullTypeName);
        if (type is not null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        // Fallback: текстовое представление
        return new TextBlock { Text = $"View not found: {viewName}" };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}