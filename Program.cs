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

            if (args != null && args.Any(a => a.Equals("--test-ebook", StringComparison.OrdinalIgnoreCase)))
            {
                RunEbookValidationTests();
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

        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);

        private static void RunEbookValidationTests()
        {
            AttachConsole(-1);
            var sb = new System.Text.StringBuilder();
            void Log(string s) { Console.WriteLine(s); sb.AppendLine(s); }

            Log("==================================================");
            Log("Running Kerkenez Voice Ebook Voicer Diagnostics...");
            Log("==================================================");

            int passed = 0;
            int total = 0;

            void AssertTest(string testName, bool condition, string details = "")
            {
                total++;
                if (condition)
                {
                    passed++;
                    Log($"[PASS] {testName}");
                }
                else
                {
                    Log($"[FAIL] {testName}: {details}");
                }
            }

            // 1. Hyphenation test
            var opts = new KerkenezVoice.Models.EbookCleaningOptions();
            string hyphenInput = "This is an inter-\r\nnational phenom-\n  enon.";
            string hyphenCleaned = EbookParserService.CleanText(hyphenInput, opts);
            AssertTest("Fix Broken Hyphenation", hyphenCleaned.Contains("international") && hyphenCleaned.Contains("phenomenon"), $"Got: '{hyphenCleaned}'");

            // 2. Page numbers and running headers test
            string pageInput = "Page 14\nHere is a sentence.\n14 of 50\nAnother sentence.\n— 15 —\nFinal sentence.";
            string pageCleaned = EbookParserService.CleanText(pageInput, opts);
            AssertTest("Strip Page Numbers & Headers", !pageCleaned.Contains("Page 14") && !pageCleaned.Contains("14 of 50") && !pageCleaned.Contains("— 15 —") && pageCleaned.Contains("Here is a sentence."), $"Got: '{pageCleaned}'");

            // 3. Citations test
            string citeInput = "According to previous studies [1, 2, 4], and recent work (Smith et al., 2020), this is true.";
            string citeCleaned = EbookParserService.CleanText(citeInput, opts);
            AssertTest("Remove Citations", !citeCleaned.Contains("[1, 2, 4]") && !citeCleaned.Contains("(Smith et al., 2020)"), $"Got: '{citeCleaned}'");

            // 4. URLs test
            string urlInput = "For more details, visit https://github.com/KerkenezVoice or www.example.com today.";
            string urlCleaned = EbookParserService.CleanText(urlInput, opts);
            AssertTest("Remove Web URLs", !urlCleaned.Contains("https://github.com/KerkenezVoice") && !urlCleaned.Contains("www.example.com"), $"Got: '{urlCleaned}'");

            // 5. Ligatures and dashes test
            string ligInput = "The ﬁrst ﬂight was an œconomic afﬁliation—a monumental day…";
            string ligCleaned = EbookParserService.CleanText(ligInput, opts);
            AssertTest("Normalize Ligatures & Dashes", ligCleaned.Contains("first flight") && ligCleaned.Contains("oeconomic affiliation, a"), $"Got: '{ligCleaned}'");

            // 6. Sentence punctuation spacing test (e.g. "back.She", "feet.What", "that?Eh?")
            string punctInput = "wings appear from her back.She then starts to flap.What is that?Eh? Certainly true.Is it acting?My story.But this.Her";
            string punctCleaned = EbookParserService.CleanText(punctInput, opts);
            AssertTest("Punctuation Spacing Normalization",
                punctCleaned.Contains("back. She") &&
                punctCleaned.Contains("flap. What") &&
                punctCleaned.Contains("that? Eh? Certainly") &&
                punctCleaned.Contains("true. Is") &&
                punctCleaned.Contains("acting? My") &&
                punctCleaned.Contains("story. But") &&
                punctCleaned.Contains("this. Her"),
                $"Got: '{punctCleaned}'");

            // 7. Multiple dots & sound effects separation (e.g. "......FLAPBlack")
            string dotsInput = "chan\" with a smile......FLAPBlack wings and but......Angel? No";
            string dotsCleaned = EbookParserService.CleanText(dotsInput, opts);
            AssertTest("Ellipsis & Sound Effect Separation",
                dotsCleaned.Contains("smile... FLAP Black") &&
                dotsCleaned.Contains("but... Angel?"),
                $"Got: '{dotsCleaned}'");

            // 8. PDF glued word separation (e.g. "airand", "setsbehind")
            string gluedInput = "float in the airand then drop while the sun setsbehind her.";
            string gluedCleaned = EbookParserService.CleanText(gluedInput, opts);
            AssertTest("Glued Word Separation",
                gluedCleaned.Contains("air and") &&
                gluedCleaned.Contains("sets behind"),
                $"Got: '{gluedCleaned}'");

            // 9. Soft line unbreak within paragraphs
            string softInput = "The black feathers float in the air\nand then drop down to my feet.";
            string softCleaned = EbookParserService.CleanText(softInput, opts);
            AssertTest("Soft Line Unbreak", softCleaned.Contains("in the air and then drop"), $"Got: '{softCleaned}'");

            // 10. Ebook Chapter Parsing (TXT)
            string tempBookPath = Path.Combine(Path.GetTempPath(), $"TestEbook_{Guid.NewGuid():N}.txt");
            try
            {
                string bookContent = @"# Chapter 1: The Beginning
It was the best of times, it was the worst of times.

# Chapter 2: The Journey
They set out across the open seas toward the uncharted horizon.

# Chapter 3: The Resolution
Finally they reached the distant harbor and found peace at last.
";
                File.WriteAllText(tempBookPath, bookContent, System.Text.Encoding.UTF8);

                var parser = new EbookParserService();
                var bookMeta = parser.ParseEbook(tempBookPath);

                AssertTest("Parse TXT Chapters Count", bookMeta.Chapters.Count == 3, $"Expected 3, got {bookMeta.Chapters.Count}");
                AssertTest("Parse TXT Chapter 1 Title", bookMeta.Chapters[0].Title.Contains("The Beginning"), $"Got: '{bookMeta.Chapters[0].Title}'");
                AssertTest("Parse TXT Chapter 2 Title", bookMeta.Chapters[1].Title.Contains("The Journey"), $"Got: '{bookMeta.Chapters[1].Title}'");
                AssertTest("Parse TXT Chapter 3 Title", bookMeta.Chapters[2].Title.Contains("The Resolution"), $"Got: '{bookMeta.Chapters[2].Title}'");
                AssertTest("Calculate Word Counts & Duration", bookMeta.TotalWordCount > 0 && bookMeta.TotalDuration > TimeSpan.Zero, $"Words: {bookMeta.TotalWordCount}, Duration: {bookMeta.TotalDuration}");
            }
            finally
            {
                if (File.Exists(tempBookPath)) File.Delete(tempBookPath);
            }

            // 11. Exact User Screenshot Test (High School DxD Light Novel PDF Text)
            string dxdInput = "chan\" with a smile......FLAPBlack wings appear from\n" +
                              "her back.She then starts to flap her wings. The black\n" +
                              "feathers float in the airand then drop down to my\n" +
                              "feet.What is that?Eh? Certainly Yuuma-chan is cute\n" +
                              "like an angel, but......Angel? No, there's no way that\n" +
                              "can be true.Is it some kind of acting?My beautiful\n" +
                              "girlfriend who is flapping her wings while the sun\n" +
                              "setsbehind her. It looks like a scene from a fantasy\n" +
                              "story.But there is no way I can believe something like\n" +
                              "this.Her cute looking eyes change into cold scary";

            string dxdCleaned = EbookParserService.CleanText(dxdInput, opts);
            AssertTest("High School DxD Ebook Cleaning Verification",
                dxdCleaned.Contains("smile... FLAP Black") &&
                dxdCleaned.Contains("back. She") &&
                dxdCleaned.Contains("air and then drop") &&
                dxdCleaned.Contains("feet. What is that? Eh? Certainly") &&
                dxdCleaned.Contains("but... Angel?") &&
                dxdCleaned.Contains("there's no way") &&
                dxdCleaned.Contains("true. Is it some kind of acting? My beautiful") &&
                dxdCleaned.Contains("sets behind her") &&
                dxdCleaned.Contains("story. But there is") &&
                dxdCleaned.Contains("this. Her cute"),
                $"Got: '{dxdCleaned}'");

            // 12. Exact Latest Screenshot Test (DxD Table of Contents & Prologue Cleanup)
            string dxdTocInput = "Table of Contents\n\nLife.0 Life.1 I Quit Being a Human Life.2 I Start as a Devil Life.3 I Made a Friend Life.4 I'm Saving My Friend!\nNew Life.\nAfterword Credits\n\nThe same colour as that person's hair—.\n\nThat's what I thought while I looked at my hand covered in blood.\n\nRed, Crimson red hair which is more brilliant than strawberry-blonde.\n\nYes, that person's long and beautiful crimson hair has the same colour as the colour my hand is covered in.";

            string dxdTocCleaned = EbookParserService.CleanText(dxdTocInput, opts);
            AssertTest("High School DxD TOC & Prologue Cleaning Verification",
                dxdTocCleaned.Contains("hair.") &&
                !dxdTocCleaned.Contains("hair,.") &&
                !dxdTocCleaned.Contains("hair—.") &&
                dxdTocCleaned.Contains("strawberry-blonde") &&
                dxdTocCleaned.Contains("blood.\n\nRed") &&
                dxdTocCleaned.Contains("Credits\n\nThe"),
                $"Got: '{dxdTocCleaned}'");

            Log("==================================================");
            Log($"Test Results: {passed}/{total} Passed ({(double)passed / total * 100:0.0}%)");
            Log("==================================================");

            File.WriteAllText("ebook_test_log.txt", sb.ToString());
        }
    }
}
