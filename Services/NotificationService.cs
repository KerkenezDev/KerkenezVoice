using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Windows.Forms;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace KerkenezVoice.Services
{
    public static class NotificationService
    {
        private const string AppDisplayName = "Kerkenez Voice";
        private static string? _cachedAppId;
        private static bool _isRegistered = false;
        private static readonly object _lock = new();

        [System.Runtime.InteropServices.DllImport("shell32.dll", SetLastError = true)]
        private static extern void SetCurrentProcessExplicitAppUserModelID([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string AppID);

        public static string ResolveAppId()
        {
            if (_cachedAppId != null) return _cachedAppId;

            try
            {
                string startMenuLnk = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                    "Kerkenez Voice.lnk");

                if (File.Exists(startMenuLnk))
                {
                    Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                    if (shellType != null)
                    {
                        dynamic shell = Activator.CreateInstance(shellType)!;
                        dynamic shortcut = shell.CreateShortcut(startMenuLnk);
                        string target = (string)shortcut.TargetPath;
                        if (!string.IsNullOrWhiteSpace(target) && File.Exists(target))
                        {
                            return _cachedAppId = target;
                        }
                    }
                }
                else
                {
                    ShortcutService.CreateShortcuts(createDesktop: false, createStartMenu: true);
                }
            }
            catch { }

            return _cachedAppId = Application.ExecutablePath;
        }

        public static void EnsureRegistered()
        {
            if (_isRegistered) return;
            lock (_lock)
            {
                if (_isRegistered) return;
                try
                {
                    string appId = ResolveAppId();
                    SetCurrentProcessExplicitAppUserModelID(appId);
                    _isRegistered = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[NotificationService] SetCurrentProcessExplicitAppUserModelID error: {ex.Message}");
                }
            }
        }

        public static void ShowNotification(string title, string message)
        {
            try
            {
                EnsureRegistered();
                string appId = ResolveAppId();

                string safeTitle = SecurityElement.Escape(title) ?? "";
                string safeMessage = SecurityElement.Escape(message) ?? "";

                string xml = $@"
<toast scenario=""default"">
    <visual>
        <binding template=""ToastGeneric"">
            <text>{safeTitle}</text>
            <text>{safeMessage}</text>
            <text placement=""attribution"">{AppDisplayName}</text>
        </binding>
    </visual>
</toast>";

                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(xml);

                var toast = new ToastNotification(xmlDoc);
                ToastNotificationManager.CreateToastNotifier(appId).Show(toast);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationService] Toast error: {ex.Message}");
            }
        }
    }
}
