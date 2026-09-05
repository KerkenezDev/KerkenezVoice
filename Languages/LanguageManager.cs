using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace KerkenezVoice.Languages
{
    public class LanguageChangedEventArgs : EventArgs
    {
        public ILanguage Language { get; }

        public LanguageChangedEventArgs(ILanguage language)
        {
            Language = language;
        }
    }

    public class LanguageManager
    {
        private static readonly Lazy<LanguageManager> _lazyInstance = new(() => new LanguageManager());
        public static LanguageManager Instance => _lazyInstance.Value;

        private readonly Dictionary<string, ILanguage> _languages = new(StringComparer.OrdinalIgnoreCase);
        private ILanguage _currentLanguage = null!;
        private ILanguage _fallbackLanguage = null!;

        public event EventHandler<LanguageChangedEventArgs>? LanguageChanged;

        private LanguageManager()
        {
            DiscoverLanguages();
        }

        private void DiscoverLanguages()
        {
            var langInterface = typeof(ILanguage);
            var assembly = Assembly.GetExecutingAssembly();

            try
            {
                var types = assembly.GetTypes()
                    .Where(t => langInterface.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                foreach (var type in types)
                {
                    try
                    {
                        if (Activator.CreateInstance(type) is ILanguage instance && !string.IsNullOrWhiteSpace(instance.Code))
                        {
                            _languages[instance.Code] = instance;
                        }
                    }
                    catch { }
                }
            }
            catch { }

            if (!_languages.ContainsKey("en"))
            {
                _languages["en"] = new EnglishLanguage();
            }
            if (!_languages.ContainsKey("tr"))
            {
                _languages["tr"] = new TurkishLanguage();
            }

            _fallbackLanguage = _languages["en"];
            _currentLanguage = _fallbackLanguage;
        }

        public IReadOnlyList<ILanguage> AvailableLanguages =>
            _languages.Values.OrderBy(l => l.Name).ToList();

        public ILanguage CurrentLanguage => _currentLanguage;

        public void SetLanguage(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                code = "en";
            }

            ILanguage target = _languages.TryGetValue(code, out var found) ? found : _fallbackLanguage;

            if (_currentLanguage.Code != target.Code)
            {
                _currentLanguage = target;
                LanguageChanged?.Invoke(this, new LanguageChangedEventArgs(target));
            }
        }

        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            if (_currentLanguage.HasString(key))
            {
                return _currentLanguage.GetString(key);
            }

            if (_fallbackLanguage.HasString(key))
            {
                return _fallbackLanguage.GetString(key);
            }

            return key;
        }

        public string Format(string key, params object[] args)
        {
            string format = Get(key);
            try
            {
                return string.Format(format, args);
            }
            catch
            {
                return format;
            }
        }
    }
}
