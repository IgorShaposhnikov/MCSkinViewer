namespace MCSkinViewer.WPF.SampleApp.Mvvm.ViewModels
{
    public sealed class MainViewModel : ViewModelBase
    {
        public ViewModelBase? CurrentViewModel { get; } = new MinecraftSkinViewerViewModel();
    }
}
