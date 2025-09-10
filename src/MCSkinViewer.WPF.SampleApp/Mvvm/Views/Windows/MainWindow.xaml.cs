using MCSkinViewer.WPF.SampleApp.Mvvm.ViewModels;
using System.Windows;

namespace MCSkinViewer.WPF.SampleApp.Mvvm.Views.Windows;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}