using System.Windows;
using KokoroGUI.Services;
using KokoroGUI.ViewModels;

namespace KokoroGUI.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow(SettingsViewModel viewModel)
        {
            InitializeComponent();
            WindowDarkModeHelper.EnableDarkMode(this);
            DataContext = viewModel;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
