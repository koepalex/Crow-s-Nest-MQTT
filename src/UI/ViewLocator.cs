using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CrowsNestMqtt.UI.Views;
using CrowsNestMqtt.BusinessLogic.Services;
using CrowsNestMqtt.UI.ViewModels;
using ReactiveUI;
using System.Reactive;

namespace CrowsNestMqtt.UI;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        // // var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        // // var type = Type.GetType(name);

        // // if (type != null)
        // // {
        // //     return (Control)Activator.CreateInstance(type)!;
        // // }

        return new MainWindow
        {
            DataContext = new MainViewModel(new CommandParserService())
        };
    }

    public bool Match(object? data)
    {
        return data is ReactiveObject;
    }
}