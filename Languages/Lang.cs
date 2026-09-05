namespace KerkenezVoice.Languages
{
    public static class Lang
    {
        public static string T(string key) => LanguageManager.Instance.Get(key);
        public static string Format(string key, params object[] args) => LanguageManager.Instance.Format(key, args);
    }
}
