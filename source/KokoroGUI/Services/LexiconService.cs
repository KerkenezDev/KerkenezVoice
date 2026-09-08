using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace KokoroGUI.Services
{
    public class LexiconService
    {
        private readonly ConcurrentDictionary<string, Regex> _regexCache = new();

        public string ApplyLexicon(string text, IDictionary<string, string>? lexicon)
        {
            if (string.IsNullOrEmpty(text) || lexicon == null || lexicon.Count == 0)
                return text;

            foreach (var kv in lexicon)
            {
                string src = kv.Key;
                string dest = kv.Value ?? string.Empty;

                if (string.IsNullOrWhiteSpace(src)) continue;

                try
                {
                    var regex = _regexCache.GetOrAdd(src, s => new Regex(Regex.Escape(s), RegexOptions.IgnoreCase | RegexOptions.Compiled));
                    text = regex.Replace(text, dest);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lexicon replacement error for '{src}': {ex.Message}");
                }
            }

            return text;
        }
    }
}
