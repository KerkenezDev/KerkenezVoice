using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace KerkenezVoice.Services
{
    public static class UninstallRegistrationService
    {
        private const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\KerkenezVoice";
        private const string DisplayName = "Kerkenez Voice";
        private const string Publisher = "ismlEraslan";
        private const string UrlInfoAbout = "https://github.com/KerkenezDev/KerkenezVoice";

        public static string CurrentVersion
        {
            get
            {
                try
                {
                    string prodVer = Application.ProductVersion;
                    if (!string.IsNullOrWhiteSpace(prodVer))
                    {
                        int plusIdx = prodVer.IndexOf('+');
                        string clean = plusIdx > 0 ? prodVer.Substring(0, plusIdx) : prodVer;
                        clean = clean.Trim();
                        if (!string.IsNullOrEmpty(clean)) return clean;
                    }
                }
                catch { }

                return "1.0.0";
            }
        }

        public static bool RegisterOrUpdate()
        {
            try
            {
                string exePath = Application.ExecutablePath;
                if (!File.Exists(exePath)) return false;

                string installLocation = Path.GetDirectoryName(exePath) ?? "";
                string uninstallCommand = $"\"{exePath}\" --uninstall";
                string quietUninstallCommand = $"\"{exePath}\" --uninstall --quiet";

                using var key = Registry.CurrentUser.CreateSubKey(UninstallKeyPath);
                if (key == null) return false;

                key.SetValue("DisplayName", DisplayName, RegistryValueKind.String);
                key.SetValue("DisplayVersion", CurrentVersion, RegistryValueKind.String);
                key.SetValue("Publisher", Publisher, RegistryValueKind.String);
                key.SetValue("DisplayIcon", exePath, RegistryValueKind.String);
                key.SetValue("InstallLocation", installLocation, RegistryValueKind.String);
                key.SetValue("UninstallString", uninstallCommand, RegistryValueKind.String);
                key.SetValue("QuietUninstallString", quietUninstallCommand, RegistryValueKind.String);
                key.SetValue("URLInfoAbout", UrlInfoAbout, RegistryValueKind.String);
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UninstallRegistrationService] Error: {ex.Message}");
                return false;
            }
        }

        public static bool Unregister()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(UninstallKeyPath, throwOnMissingSubKey: false);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UninstallRegistrationService] Error unregistering: {ex.Message}");
                return false;
            }
        }
    }
}
