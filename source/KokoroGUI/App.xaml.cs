using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using KokoroGUI.Services;
using KokoroGUI.ViewModels;
using KokoroGUI.Views;

namespace KokoroGUI
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                LogException(args.ExceptionObject as Exception);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                LogException(args.Exception);
                args.Handled = true;
            };

            try
            {
                var modelManager = new ModelManagerService();
                var audioProcessing = new AudioProcessingService();
                var docParser = new DocumentParserService();
                var lexiconService = new LexiconService();
                var subtitleExport = new SubtitleExportService();
                var playbackService = new AudioPlaybackService();
                var voiceMixing = new VoiceMixingService(modelManager);

                var engineService = new KokoroEngineService(
                    modelManager,
                    audioProcessing,
                    lexiconService,
                    subtitleExport,
                    playbackService
                );

                var mainViewModel = new MainViewModel(
                    modelManager,
                    engineService,
                    voiceMixing,
                    docParser,
                    playbackService
                );

                var mainWindow = new MainWindow(mainViewModel, modelManager);
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                LogException(ex);
                MessageBox.Show(
                    $"Startup Error: {ex.Message}\n\nStack:\n{ex.StackTrace}",
                    "Kokoro GUI Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                Shutdown(-1);
            }
        }

        private static void LogException(Exception? ex)
        {
            if (ex == null) return;
            try
            {
                string appData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "KokoroTTS"
                );
                Directory.CreateDirectory(appData);
                string logPath = Path.Combine(appData, "crash.log");
                File.AppendAllText(logPath, $"[{DateTime.Now}] {ex}\n\n");
            }
            catch { }
        }
    }
}
