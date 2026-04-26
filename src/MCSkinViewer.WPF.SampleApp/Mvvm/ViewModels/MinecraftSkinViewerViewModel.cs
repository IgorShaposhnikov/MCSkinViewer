namespace MCSkinViewer.WPF.SampleApp.Mvvm.ViewModels
{
    internal sealed class MinecraftSkinViewerViewModel : ViewModelBase
    {
        private string _skinPath = "pack://application:,,,/Assets/hel2xSkin.png";
        public string SkinPath
        {
            get => _skinPath; set 
            { 
                _skinPath = value; 
                OnPropertyChanged(); 
            }
        }

        private bool _isAutoRotate;
        public bool IsAutoRotate
        {
            get => _isAutoRotate; set 
            { 
                _isAutoRotate = value;
                OnPropertyChanged();
            }
        }
    }
}
