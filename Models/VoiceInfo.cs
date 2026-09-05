namespace KerkenezVoice.Models
{
    public class LexiconRule
    {
        public string Original { get; set; } = string.Empty;
        public string Replacement { get; set; } = string.Empty;

        public LexiconRule() { }

        public LexiconRule(string original, string replacement)
        {
            Original = original;
            Replacement = replacement;
        }
    }

    public class LanguageInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;

        public override string ToString() => Name;
    }
}
