using System.Collections.Generic;

namespace KerkenezVoice.Languages
{
    public interface ILanguage
    {
        string Code { get; }
        string Name { get; }
        string EnglishName { get; }
        string FlagEmoji { get; }
        string GetString(string key);
        bool HasString(string key);
        IReadOnlyDictionary<string, string> GetAllStrings();
    }
}
