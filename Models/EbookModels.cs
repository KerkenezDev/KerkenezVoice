using System;
using System.Collections.Generic;
using System.Linq;

namespace KerkenezVoice.Models
{
    public enum EbookFormat
    {
        Epub,
        Pdf,
        Txt
    }

    public enum EbookChapterStatus
    {
        Queued,
        Synthesizing,
        Complete,
        Failed,
        Skipped
    }

    public class EbookCleaningOptions
    {
        public bool FixHyphenation { get; set; } = true;
        public bool StripPageNumbersAndHeaders { get; set; } = true;
        public bool RemoveCitations { get; set; } = true;
        public bool RemoveUrls { get; set; } = true;
        public bool NormalizeUnicodeAndLigatures { get; set; } = true;
        public bool RemoveFootnotes { get; set; } = true;

        public EbookCleaningOptions Clone()
        {
            return (EbookCleaningOptions)this.MemberwiseClone();
        }
    }

    public class EbookChapter
    {
        public int Index { get; set; }
        public string Title { get; set; } = string.Empty;
        public string RawText { get; set; } = string.Empty;
        public string CleanedText { get; set; } = string.Empty;
        public int WordCount { get; set; }
        public TimeSpan EstimatedDuration { get; set; }
        public bool IsSelected { get; set; } = true;
        public EbookChapterStatus Status { get; set; } = EbookChapterStatus.Queued;
        public string StatusMessage { get; set; } = "Queued";
        public string OutputPath { get; set; } = string.Empty;

        public void RecalculateStats(double speechRateMultiplier = 1.0)
        {
            string text = string.IsNullOrWhiteSpace(CleanedText) ? RawText : CleanedText;
            if (string.IsNullOrWhiteSpace(text))
            {
                WordCount = 0;
                EstimatedDuration = TimeSpan.Zero;
                return;
            }

            var words = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            WordCount = words.Length;

            // Average conversational audiobook narration is ~150 words per minute
            double effectiveWpm = Math.Max(50.0, 150.0 * Math.Clamp(speechRateMultiplier, 0.5, 2.5));
            double minutes = WordCount / effectiveWpm;
            EstimatedDuration = TimeSpan.FromMinutes(minutes);
        }
    }

    public class EbookMetadata
    {
        public string Title { get; set; } = "Untitled Document";
        public string Author { get; set; } = "Unknown Author";
        public byte[]? CoverImage { get; set; }
        public EbookFormat Format { get; set; } = EbookFormat.Txt;
        public string FilePath { get; set; } = string.Empty;
        public List<EbookChapter> Chapters { get; set; } = new();

        public int TotalWordCount => Chapters.Count > 0 ? Chapters.Sum(c => c.WordCount) : 0;
        public TimeSpan TotalDuration => Chapters.Count > 0 ? TimeSpan.FromSeconds(Chapters.Sum(c => c.EstimatedDuration.TotalSeconds)) : TimeSpan.Zero;
        public int SelectedChapterCount => Chapters.Count(c => c.IsSelected);
    }
}
