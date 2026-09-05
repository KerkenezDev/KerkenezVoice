using System;
using System.Collections.Generic;

namespace KerkenezVoice.Languages
{
    public abstract class BaseLanguage : ILanguage
    {
        public abstract string Code { get; }
        public abstract string Name { get; }
        public abstract string EnglishName { get; }
        public virtual string FlagEmoji => "";

        protected readonly Dictionary<string, string> _translations = new(StringComparer.OrdinalIgnoreCase);

        protected BaseLanguage()
        {
            InitTranslations();
        }

        protected abstract void InitTranslations();

        protected void Set(string key, string value)
        {
            _translations[key] = value;
        }

        public virtual string GetString(string key)
        {
            if (_translations.TryGetValue(key, out var value))
            {
                return value;
            }
            return key;
        }

        public bool HasString(string key)
        {
            return _translations.ContainsKey(key);
        }

        public IReadOnlyDictionary<string, string> GetAllStrings()
        {
            return _translations;
        }

        public override string ToString() => $"{FlagEmoji} {Name} ({Code})".Trim();
    }
}
