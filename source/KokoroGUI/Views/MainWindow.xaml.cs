using System;
using System.Windows;
using System.Windows.Controls;
using KokoroGUI.Services;
using KokoroGUI.ViewModels;

namespace KokoroGUI.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly ModelManagerService _modelManager;

        public MainWindow(MainViewModel viewModel, ModelManagerService modelManager)
        {
            InitializeComponent();
            WindowDarkModeHelper.EnableDarkMode(this);

            _viewModel = viewModel;
            _modelManager = modelManager;
            DataContext = _viewModel;

            _viewModel.RequestTextInputDialog += OnRequestTextInputDialog;

            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Check if models exist. If not, open first-launch model downloader dialog!
            if (!_modelManager.AreModelsPresent())
            {
                var downloadVm = new ModelDownloadViewModel(_modelManager);
                var downloadWindow = new ModelDownloadWindow(downloadVm)
                {
                    Owner = this
                };

                _ = downloadVm.StartDownloadAsync();
                bool? result = downloadWindow.ShowDialog();

                if (result != true && !_modelManager.AreModelsPresent())
                {
                    MessageBox.Show(
                        "Model files are required to use Kokoro TTS. The application will close.",
                        "Models Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                    Close();
                    return;
                }
            }

            // Initialize Kokoro engine
            await _viewModel.InitializeEngineAsync();
        }

        private string? OnRequestTextInputDialog(string prompt, string title)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 350,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Icon = Icon,
                Background = (System.Windows.Media.Brush)FindResource("WindowBackgroundBrush")
            };

            WindowDarkModeHelper.EnableDarkMode(dialog);

            var panel = new StackPanel { Margin = new Thickness(15) };
            var textBlock = new TextBlock
            {
                Text = prompt,
                FontFamily = (System.Windows.Media.FontFamily)FindResource("AppFont"),
                Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"),
                Margin = new Thickness(0, 0, 0, 10)
            };
            var textBox = new TextBox
            {
                Style = (Style)FindResource("CustomTextBoxStyle"),
                Margin = new Thickness(0, 0, 0, 15)
            };

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var okBtn = new Button
            {
                Content = "OK",
                Style = (Style)FindResource("CustomButtonStyle"),
                Width = 65,
                Margin = new Thickness(0, 0, 8, 0),
                IsDefault = true
            };
            var cancelBtn = new Button
            {
                Content = "Cancel",
                Style = (Style)FindResource("CancelButtonStyle"),
                Width = 65,
                IsCancel = true
            };

            string? result = null;
            okBtn.Click += (s, e) =>
            {
                result = textBox.Text;
                dialog.DialogResult = true;
                dialog.Close();
            };
            cancelBtn.Click += (s, e) =>
            {
                dialog.DialogResult = false;
                dialog.Close();
            };

            btnPanel.Children.Add(okBtn);
            btnPanel.Children.Add(cancelBtn);

            panel.Children.Add(textBlock);
            panel.Children.Add(textBox);
            panel.Children.Add(btnPanel);

            dialog.Content = panel;
            textBox.Focus();

            if (dialog.ShowDialog() == true)
            {
                return result;
            }
            return null;
        }
    }
}
