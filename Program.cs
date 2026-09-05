using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using KerkenezVoice.Languages;
using KerkenezVoice.Services;
using KerkenezVoice.UI;

namespace KerkenezVoice
{
    static class Program
    {
        private const string MainUiMutexName = @"Global\KerkenezVoice_MainUI_Mutex";

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        [STAThread]
        static void Main(string[] args)
        {
            // Ensure temp folder cleanup on process exit
            AppDomain.CurrentDomain.ProcessExit += (s, e) => ConfigService.CleanTempFolder();

            // 1. Handle --uninstall switch
            if (args != null && args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase) ||
                                              a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase) ||
                                              a.Equals("-uninstall", StringComparison.OrdinalIgnoreCase)))
            {
                bool isQuiet = args.Any(a => a.Equals("--quiet", StringComparison.OrdinalIgnoreCase) ||
                                             a.Equals("/quiet", StringComparison.OrdinalIgnoreCase) ||
                                             a.Equals("-quiet", StringComparison.OrdinalIgnoreCase) ||
                                             a.Equals("--silent", StringComparison.OrdinalIgnoreCase) ||
                                             a.Equals("/silent", StringComparison.OrdinalIgnoreCase) ||
                                             a.Equals("-silent", StringComparison.OrdinalIgnoreCase) ||
                                             a.Equals("-s", StringComparison.OrdinalIgnoreCase) ||
                                             a.Equals("-q", StringComparison.OrdinalIgnoreCase));

                if (!isQuiet)
                {
                    ApplicationConfiguration.Initialize();
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                }
                HandleUninstall(isQuiet);
                return;
            }

            UninstallRegistrationService.RegisterOrUpdate();

            // Global unhandled exception catching
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => HandleFatalException(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex) HandleFatalException(ex);
            };

            // Ensure AUMID is registered for persistent Windows Action Center notifications
            NotificationService.EnsureRegistered();

            // 2. Normal Execution (Main GUI Application)
            RunMainUiMode();
        }

        private static void RunMainUiMode()
        {
            bool createdNewMain = false;
            Mutex? mainMutex = null;
            try
            {
                mainMutex = new Mutex(true, MainUiMutexName, out createdNewMain);
            }
            catch (AbandonedMutexException)
            {
                createdNewMain = true;
            }
            catch { }

            if (!createdNewMain)
            {
                bool focused = FocusExistingMainWindow();
                if (focused)
                {
                    return;
                }
                // If no other process has an active visible window, allow launch
            }

            try
            {
                ApplicationConfiguration.Initialize();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                var configService = new ConfigService();
                LanguageManager.Instance.SetLanguage(configService.Settings.Language);

                Application.Run(new MainForm(configService));
            }
            catch (Exception ex)
            {
                HandleFatalException(ex);
            }
            finally
            {
                try { mainMutex?.ReleaseMutex(); } catch { }
                mainMutex?.Dispose();
            }
        }

        private static void HandleFatalException(Exception ex)
        {
            try
            {
                string logPath = Path.Combine(ConfigService.AppDataFolder, "crash.log");
                File.WriteAllText(logPath, $"[{DateTime.Now}] Crash:\n{ex}\n");
            }
            catch { }

            MessageBox.Show(
                $"Kerkenez Voice encountered an unhandled error:\n\n{ex.Message}\n\nStack trace:\n{ex.StackTrace}",
                "Kerkenez Voice Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private static bool FocusExistingMainWindow()
        {
            try
            {
                var current = Process.GetCurrentProcess();
                var existing = Process.GetProcessesByName(current.ProcessName)
                    .FirstOrDefault(p => p.Id != current.Id && p.MainWindowHandle != IntPtr.Zero);

                if (existing != null)
                {
                    IntPtr handle = existing.MainWindowHandle;
                    ShowWindow(handle, SW_RESTORE);
                    SetForegroundWindow(handle);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static void HandleUninstall(bool isQuiet)
        {
            try
            {
                if (!isQuiet)
                {
                    var result = MessageBox.Show(
                        "Are you sure you want to uninstall Kerkenez Voice?\n\n" +
                        "This will remove Desktop and Start Menu shortcuts, delete Windows startup entries, " +
                        "and remove Installed Apps registration.",
                        "Uninstall Kerkenez Voice",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result != DialogResult.Yes)
                    {
                        return;
                    }
                }

                // 1. Remove Windows Installed Apps registry entry
                UninstallRegistrationService.Unregister();

                // 2. Remove Desktop and Start Menu shortcuts
                ShortcutService.DeleteShortcuts();

                // 3. Clean temp folder
                ConfigService.CleanTempFolder();

                if (!isQuiet)
                {
                    MessageBox.Show(
                        "Kerkenez Voice has been uninstalled successfully.",
                        "Uninstall Complete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                if (!isQuiet)
                {
                    MessageBox.Show(
                        $"An error occurred during uninstallation:\n{ex.Message}",
                        "Uninstall Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }
    }
}
