using System.Windows;
using KokoroGUI.Services;
using KokoroGUI.ViewModels;

namespace KokoroGUI.Views
{
    public partial class ModelDownloadWindow : Window
    {
        public ModelDownloadWindow(ModelDownloadViewModel viewModel)
        {
            InitializeComponent();
            WindowDarkModeHelper.EnableDarkMode(this);

            DataContext = viewModel;
            viewModel.DownloadCompleted += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    DialogResult = true;
                    Close();
                });
            };
        }
    }
}
