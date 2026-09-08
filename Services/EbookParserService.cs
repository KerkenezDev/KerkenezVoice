using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using VersOne.Epub;
using KerkenezVoice.Models;

namespace KerkenezVoice.Services
{
    public class EbookParserService
    {
        public EbookMetadata ParseEbook(string filePath, EbookCleaningOptions? options = null)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Ebook file not found.", filePath);

            options ??= new EbookCleaningOptions();
            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            EbookMetadata metadata;
            switch (ext)
            {
                case ".epub":
                    metadata = ParseEpub(filePath);
                    break;
                case ".pdf":
                    metadata = ParsePdf(filePath);
                    break;
                default:
                    metadata = ParseTxt(filePath);
                    break;
            }

            metadata.FilePath = filePath;

            // Apply cleaning pipeline and calculate statistics for each chapter
            for (int i = 0; i < metadata.Chapters.Count; i++)
            {
                var ch = metadata.Chapters[i];
                ch.Index = i + 1;
                ch.CleanedText = CleanText(ch.RawText, options);
                ch.RecalculateStats();
            }

            // Remove completely empty chapters
            metadata.Chapters.RemoveAll(c => string.IsNullOrWhiteSpace(c.CleanedText));

            // If no chapters remained or none found, ensure at least one fallback chapter
            if (metadata.Chapters.Count == 0)
            {
                var fallback = new EbookChapter
                {
                    Index = 1,
                    Title = "Full Content",
                    RawText = "",
                    CleanedText = "No readable text could be extracted from this document."
                };
                fallback.RecalculateStats();
                metadata.Chapters.Add(fallback);
            }
            else
            {
                // Re-index chapters
                for (int i = 0; i < metadata.Chapters.Count; i++)
                {
                    metadata.Chapters[i].Index = i + 1;
                }
            }

            return metadata;
        }

        #region Format-Specific Parsers

        private EbookMetadata ParseEpub(string filePath)
        {
            var metadata = new EbookMetadata
            {
                Format = EbookFormat.Epub,
                FilePath = filePath
            };

            var book = EpubReader.ReadBook(filePath);
            metadata.Title = string.IsNullOrWhiteSpace(book.Title) ? Path.GetFileNameWithoutExtension(filePath) : book.Title.Trim();
            metadata.Author = string.IsNullOrWhiteSpace(book.Author) ? "Unknown Author" : book.Author.Trim();
            metadata.CoverImage = book.CoverImage;

            // Build chapter list from reading order
            int chapterNumber = 1;
            foreach (var item in book.ReadingOrder)
            {
                if (string.IsNullOrWhiteSpace(item.Content))
                    continue;

                string rawHtml = item.Content;
                string title = ExtractHtmlTitle(rawHtml);
                string text = ConvertHtmlToPlainText(rawHtml);

                if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 20)
                    continue; // Skip trivial cover/spacer files

                if (string.IsNullOrWhiteSpace(title))
                {
                    title = $"Chapter {chapterNumber}";
                }

                metadata.Chapters.Add(new EbookChapter
                {
                    Index = chapterNumber++,
                    Title = title,
                    RawText = text
                });
            }

            // If reading order produced nothing, check book.Content.Html
            if (metadata.Chapters.Count == 0 && book.Content?.Html != null)
            {
                foreach (var entry in book.Content.Html.Local)
                {
                    if (string.IsNullOrWhiteSpace(entry.Content))
                        continue;

                    string title = ExtractHtmlTitle(entry.Content);
                    string text = ConvertHtmlToPlainText(entry.Content);

                    if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 20)
                        continue;

                    if (string.IsNullOrWhiteSpace(title))
                        title = $"Chapter {chapterNumber}";

                    metadata.Chapters.Add(new EbookChapter
                    {
                        Index = chapterNumber++,
                        Title = title,
                        RawText = text
                    });
                }
            }

            return metadata;
        }

        private EbookMetadata ParsePdf(string filePath)
        {
            var metadata = new EbookMetadata
            {
                Format = EbookFormat.Pdf,
                FilePath = filePath,
                Title = Path.GetFileNameWithoutExtension(filePath)
            };

            using var document = PdfDocument.Open(filePath);
            if (!string.IsNullOrWhiteSpace(document.Information?.Title))
                metadata.Title = document.Information.Title.Trim();
            if (!string.IsNullOrWhiteSpace(document.Information?.Author))
                metadata.Author = document.Information.Author.Trim();

            int totalPages = document.NumberOfPages;
            if (totalPages == 0)
                return metadata;

            // Check if document has bookmarks (TOC)
            bool hasBookmarks = document.TryGetBookmarks(out var bookmarks);
            var bookmarkList = new List<(string Title, int PageNumber)>();

            if (hasBookmarks && bookmarks != null)
            {
                foreach (var node in bookmarks.GetNodes())
                {
                    if (node is UglyToad.PdfPig.Outline.DocumentBookmarkNode docNode &&
                        !string.IsNullOrWhiteSpace(docNode.Title) &&
                        docNode.PageNumber >= 1 && docNode.PageNumber <= totalPages)
                    {
                        bookmarkList.Add((docNode.Title.Trim(), docNode.PageNumber));
                    }
                }
            }

            if (bookmarkList.Count > 0)
            {
                // Sort bookmarks by starting page
                var sorted = bookmarkList.OrderBy(b => b.PageNumber).ToList();

                for (int i = 0; i < sorted.Count; i++)
                {
                    int startPage = sorted[i].PageNumber;
                    int endPage = (i + 1 < sorted.Count) ? Math.Max(startPage, sorted[i + 1].PageNumber - 1) : totalPages;

                    var sb = new StringBuilder();
                    for (int p = startPage; p <= endPage; p++)
                    {
                        var page = document.GetPage(p);
                        string pageText = ExtractPageText(page);
                        if (!string.IsNullOrWhiteSpace(pageText))
                        {
                            sb.AppendLine(pageText);
                            sb.AppendLine();
                        }
                    }

                    string raw = sb.ToString();
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        metadata.Chapters.Add(new EbookChapter
                        {
                            Index = i + 1,
                            Title = sorted[i].Title,
                            RawText = raw
                        });
                    }
                }
            }
            else
            {
                // Fallback: Check for chapter headings across pages or chunk into segments of ~10 pages
                var pageTexts = new List<string>();
                for (int p = 1; p <= totalPages; p++)
                {
                    var page = document.GetPage(p);
                    pageTexts.Add(ExtractPageText(page));
                }

                // Chunk pages into reasonable chapters (e.g. max 10-15 pages or ~3000 words per chapter)
                int currentChapter = 1;
                var currentSb = new StringBuilder();
                int currentWords = 0;
                int startPage = 1;
                string? currentChapterTitle = null;

                for (int p = 0; p < pageTexts.Count; p++)
                {
                    string text = pageTexts[p];
                    int words = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;

                    // Check if current page (or its first 3 lines) starts with a prominent chapter header or recognized section
                    var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    Match headerMatch = Match.Empty;
                    foreach (var line in lines.Take(3))
                    {
                        var m = Regex.Match(line.Trim(), @"^(?:(?:CHAPTER|BÖLÜM|Section|Part|Act)\s+([0-9IVXLCDM]+|[A-Za-z0-9]+[^\r\n]*)|Life\.\s*\d+[^\r\n]*|Prologue[^\r\n]*|Epilogue[^\r\n]*|Interlude[^\r\n]*|Afterword[^\r\n]*|Table of Contents)", RegexOptions.IgnoreCase);
                        if (m.Success)
                        {
                            headerMatch = m;
                            break;
                        }
                    }
                    bool isChapterHeader = headerMatch.Success;

                    bool shouldSplit = (isChapterHeader && currentWords > 0) || (currentWords + words > 3500 && currentWords > 1200);
                    if (shouldSplit)
                    {
                        string chText = currentSb.ToString();
                        string chTitle = currentChapterTitle ?? (startPage == p ? $"Page {startPage}" : $"Pages {startPage}–{p}");

                        metadata.Chapters.Add(new EbookChapter
                        {
                            Index = currentChapter++,
                            Title = CleanChapterTitle(chTitle),
                            RawText = chText
                        });

                        currentSb.Clear();
                        currentWords = 0;
                        startPage = p + 1;
                        currentChapterTitle = null;
                    }

                    if (isChapterHeader && currentChapterTitle == null)
                    {
                        currentChapterTitle = ExtractFirstLineTitle(headerMatch.Value);
                    }

                    currentSb.AppendLine(text);
                    currentSb.AppendLine();
                    currentWords += words;
                }

                if (currentSb.Length > 0)
                {
                    string chTitle = currentChapterTitle ?? (startPage == totalPages ? $"Page {startPage}" : $"Pages {startPage}–{totalPages}");

                    metadata.Chapters.Add(new EbookChapter
                    {
                        Index = currentChapter,
                        Title = CleanChapterTitle(chTitle),
                        RawText = currentSb.ToString()
                    });
                }
            }

            return metadata;
        }

        private static string ExtractFirstLineTitle(string text)
        {
            string firstLine = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? text;
            firstLine = Regex.Replace(firstLine, @"\s+", " ").Trim();

            var m = Regex.Match(firstLine, @"^(Life\.\s*\d+|Table of Contents|Prologue|Epilogue|Afterword|Interlude|(?:CHAPTER|BÖLÜM|Section|Part|Act)\s+[0-9IVXLCDM]+)(?:\s*[-—:.]?\s*([^\r\n]+))?", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                string prefix = m.Groups[1].Value.Trim();
                string suffix = m.Groups[2].Value.Trim();
                if (!string.IsNullOrEmpty(suffix) && suffix.Length <= 40)
                    firstLine = $"{prefix}: {suffix}";
                else
                    firstLine = prefix;
            }

            if (firstLine.Length > 55) firstLine = firstLine.Substring(0, 52) + "...";
            return firstLine;
        }

        private static string CleanChapterTitle(string title)
        {
            title = Regex.Replace(title.Trim(), @"\s+", " ");
            if (title.Length > 55) title = title.Substring(0, 52) + "...";
            return title;
        }

        private EbookMetadata ParseTxt(string filePath)
        {
            var metadata = new EbookMetadata
            {
                Format = EbookFormat.Txt,
                FilePath = filePath,
                Title = Path.GetFileNameWithoutExtension(filePath)
            };

            string allText = File.ReadAllText(filePath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(allText))
                return metadata;

            // Check for chapter delimiters in plain text
            // Patterns: "Chapter 1", "CHAPTER I", "BÖLÜM 1", "# Chapter Title", "Section 1"
            var chapterPattern = new Regex(@"(?m)^(?:\s*(?:CHAPTER|Chapter|BÖLÜM|Bölüm|SECTION|Section|PART|Part)\s+([0-9IVXLCDM]+[^\r\n]*)|#\s+(.+))$");
            var matches = chapterPattern.Matches(allText);

            if (matches.Count > 1)
            {
                // Document has explicit chapter headers
                int lastIndex = 0;
                string currentTitle = "Introduction";

                for (int i = 0; i < matches.Count; i++)
                {
                    var match = matches[i];
                    int matchIndex = match.Index;

                    if (matchIndex > lastIndex)
                    {
                        string body = allText.Substring(lastIndex, matchIndex - lastIndex).Trim();
                        if (body.Length > 30)
                        {
                            metadata.Chapters.Add(new EbookChapter
                            {
                                Index = metadata.Chapters.Count + 1,
                                Title = currentTitle,
                                RawText = body
                            });
                        }
                    }

                    currentTitle = match.Value.Trim().TrimStart('#', ' ');
                    lastIndex = matchIndex + match.Length;
                }

                // Append remainder after the last header
                if (lastIndex < allText.Length)
                {
                    string body = allText.Substring(lastIndex).Trim();
                    if (body.Length > 0)
                    {
                        metadata.Chapters.Add(new EbookChapter
                        {
                            Index = metadata.Chapters.Count + 1,
                            Title = currentTitle,
                            RawText = body
                        });
                    }
                }
            }
            else
            {
                // Fallback: Split by word count (~3000 words per chapter) or keep as single document
                var words = allText.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length <= 3500)
                {
                    metadata.Chapters.Add(new EbookChapter
                    {
                        Index = 1,
                        Title = metadata.Title,
                        RawText = allText
                    });
                }
                else
                {
                    // Split into ~3000 word parts at paragraph breaks
                    var paragraphs = allText.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
                    int partIndex = 1;
                    var currentSb = new StringBuilder();
                    int currentWords = 0;

                    foreach (var p in paragraphs)
                    {
                        int pWords = p.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
                        if (currentWords + pWords > 3000 && currentWords > 500)
                        {
                            metadata.Chapters.Add(new EbookChapter
                            {
                                Index = partIndex,
                                Title = $"Part {partIndex}",
                                RawText = currentSb.ToString()
                            });
                            partIndex++;
                            currentSb.Clear();
                            currentWords = 0;
                        }

                        currentSb.AppendLine(p);
                        currentSb.AppendLine();
                        currentWords += pWords;
                    }

                    if (currentSb.Length > 0)
                    {
                        metadata.Chapters.Add(new EbookChapter
                        {
                            Index = partIndex,
                            Title = $"Part {partIndex}",
                            RawText = currentSb.ToString()
                        });
                    }
                }
            }

            return metadata;
        }

        private static string ExtractPageText(Page page)
        {
            try
            {
                var words = page.GetWords().ToList();
                if (words.Count > 0)
                {
                    // Group words into lines based on vertical midpoint proximity
                    var lines = new List<List<Word>>();
                    var sortedWords = words
                        .OrderByDescending(w => (w.BoundingBox.Top + w.BoundingBox.Bottom) / 2.0)
                        .ThenBy(w => w.BoundingBox.Left)
                        .ToList();

                    foreach (var word in sortedWords)
                    {
                        double wordMidY = (word.BoundingBox.Top + word.BoundingBox.Bottom) / 2.0;
                        var matchingLine = lines.FirstOrDefault(l =>
                        {
                            double lineMidY = (l[0].BoundingBox.Top + l[0].BoundingBox.Bottom) / 2.0;
                            return Math.Abs(lineMidY - wordMidY) <= Math.Max(3.5, word.BoundingBox.Height * 0.4);
                        });

                        if (matchingLine != null)
                            matchingLine.Add(word);
                        else
                            lines.Add(new List<Word> { word });
                    }

                    lines = lines
                        .OrderByDescending(l => l.Average(w => (w.BoundingBox.Top + w.BoundingBox.Bottom) / 2.0))
                        .ToList();

                    var sb = new StringBuilder();
                    for (int i = 0; i < lines.Count; i++)
                    {
                        var lineWords = lines[i].OrderBy(w => w.BoundingBox.Left).ToList();
                        string lineStr = string.Join(" ", lineWords.Select(w => w.Text)).Trim();
                        if (string.IsNullOrWhiteSpace(lineStr)) continue;

                        if (sb.Length > 0)
                        {
                            double prevBottom = lines[i - 1].Min(w => w.BoundingBox.Bottom);
                            double currTop = lines[i].Max(w => w.BoundingBox.Top);
                            double gap = prevBottom - currTop;
                            double avgHeight = lines[i].Average(w => w.BoundingBox.Height);

                            if (gap > avgHeight * 1.5)
                                sb.Append("\n\n");
                            else
                                sb.Append("\n");
                        }

                        sb.Append(lineStr);
                    }

                    string result = sb.ToString();
                    if (!string.IsNullOrWhiteSpace(result))
                        return result;
                }
            }
            catch { }

            return page.Text ?? string.Empty;
        }

        #endregion

        #region Artifact Cleaning Pipeline

        public static string CleanText(string rawText, EbookCleaningOptions options)
        {
            if (string.IsNullOrWhiteSpace(rawText))
                return string.Empty;

            // 1. Normalize line endings to \n
            string text = rawText.Replace("\r\n", "\n").Replace("\r", "\n");

            // 2. Strip HTML artifacts if present
            if (text.Contains('<') && text.Contains('>'))
            {
                text = ConvertHtmlToPlainText(text);
            }

            // 3. Normalize Unicode ligatures, special quotes, dashes, and whitespace
            if (options.NormalizeUnicodeAndLigatures)
            {
                text = NormalizeLigaturesAndPunctuation(text);
            }

            // 4. Fix broken line-break hyphenation
            if (options.FixHyphenation)
            {
                // Connector words after hyphen: parenthetical pause, not word hyphenation
                text = Regex.Replace(text, @"(\b[A-Za-z]{2,})[-\u00AD]\n\s*(and|the|to|in|on|at|by|for|with|from|as|but|nor|so|yet|behind|over|under|into|onto|he|she|it|they|we|you|is|are|was|were)\b", "$1, $2", RegexOptions.IgnoreCase);

                // Preserve hyphenated compound words (e.g. "strawberry-\nblonde" -> "strawberry-blonde")
                text = Regex.Replace(text, @"\b(strawberry|user|well|dark|blue|red|light|cold|warm|half|self|all|cross|multi|anti|ever|full|high|low|long|short|open|free|hard|soft|deep|wide)[-\u00AD]\n\s*([a-z]{3,}\b)", "$1-$2", RegexOptions.IgnoreCase);

                // Otherwise reconnect true broken hyphenated words (e.g. "inter-\n  national" -> "international", "phenom-\nenon" -> "phenomenon")
                text = Regex.Replace(text, @"(\b[A-Za-z]{2,})[-\u00AD]\n\s*([a-z]{2,}\b)", "$1$2");

                // Fix already fused known compound words
                text = Regex.Replace(text, @"\b(strawberryblonde)\b", "strawberry-blonde", RegexOptions.IgnoreCase);
            }

            // 5. Remove Web URLs
            if (options.RemoveUrls)
            {
                text = Regex.Replace(text, @"https?:\/\/[^\s\)]+|www\.[^\s\)]+", "", RegexOptions.IgnoreCase);
            }

            // 6. Remove Academic / Reference Citations
            if (options.RemoveCitations)
            {
                // Numeric citations like [1], [1, 2], [1-4], [12, 15, 18]
                text = Regex.Replace(text, @"\[\d+(?:\s*[-–,]\s*\d+)*\]", "");
                // Author-year citations like (Smith et al., 2020) or (Brown, 2019)
                text = Regex.Replace(text, @"\([A-Z][a-zA-Z\s.-]+(?:et\s+al\.?)?,\s*\d{4}[a-z]?\)", "");
            }

            // 7. Remove Footnote markers
            if (options.RemoveFootnotes)
            {
                text = Regex.Replace(text, @"\^\[?\d+\]?", "");
                text = Regex.Replace(text, @"\[[a-z]\]", "");
            }

            // 8. Strip standalone page numbers, headers, and footer lines
            if (options.StripPageNumbersAndHeaders)
            {
                var lines = text.Split('\n');
                var cleanedLines = new List<string>(lines.Length);

                foreach (var line in lines)
                {
                    string trimmed = line.Trim();

                    // Standalone page number e.g. "12", "Page 12", "12 of 345", "12 / 345"
                    if (Regex.IsMatch(trimmed, @"^(?:Page\s+)?\d+(?:\s*(?:of|\/)\s*\d+)?$", RegexOptions.IgnoreCase))
                        continue;

                    // Standalone decorated number: "- 12 -", "— 12 —"
                    if (Regex.IsMatch(trimmed, @"^[-—–]\s*\d+\s*[-—–]$"))
                        continue;

                    // Standalone Roman numerals on page margins: "IV", "xii"
                    if (Regex.IsMatch(trimmed, @"^(?=[MDCLXVI])M*(C[MD]|D?C{0,3})(X[CL]|L?X{0,3})(I[XV]|V?I{0,3})$", RegexOptions.IgnoreCase) && trimmed.Length <= 6)
                        continue;

                    cleanedLines.Add(line);
                }

                text = string.Join("\n", cleanedLines);
            }

            // 9. Unbreak soft line breaks within paragraphs
            // Single newline between lines that don't end in sentence punctuation becomes a space
            text = Regex.Replace(text, "(?<![.!?…:;\"'”’\\n\\r])\\n(?!\\n|\\s*[-*•#\\d+\\.])", " ");

            // 10. Sentence & Punctuation Spacing Normalization
            // Clean up colliding punctuation (e.g. ",.", ".,", ",;", ";,", ",?", ",!")
            text = Regex.Replace(text, @"[,;]\s*\.", ".");
            text = Regex.Replace(text, @"\.\s*[,;]", ".");
            text = Regex.Replace(text, @"[,;]\s*([!?])", "$1");
            text = Regex.Replace(text, @"([!?])\s*[,;]", "$1");

            // Insert missing space after terminal punctuation followed by a capital letter
            text = Regex.Replace(text, @"([.!?])([A-Z])", "$1 $2");

            // Insert missing space after question mark or exclamation followed by lowercase letter
            text = Regex.Replace(text, @"([!?])([a-z])", "$1 $2");

            // Insert missing space after terminal punctuation + quote followed by a letter (e.g. ."She)
            text = Regex.Replace(text, @"([.!?][""'”’])([A-Za-z])", "$1 $2");

            // Insert missing space after closing double quote or closing bracket followed by a letter (e.g. "word"Next, [ref]Word)
            text = Regex.Replace(text, @"([""”\)\]\}])([A-Za-z])", "$1 $2");

            // Insert missing space before opening bracket following a word
            text = Regex.Replace(text, @"([A-Za-z0-9])([\(\[\{])", "$1 $2");

            // Insert missing space after comma, colon, semicolon followed directly by a letter
            text = Regex.Replace(text, @"([,;:])([A-Za-z])", "$1 $2");

            // Normalize multiple dots / ellipses run into words or letters (e.g. "smile......FLAP" -> "smile... FLAP")
            text = Regex.Replace(text, @"\.{2,}([A-Za-z0-9])", "... $1");
            text = Regex.Replace(text, @"([A-Za-z0-9])\.{2,}", "$1... ");
            text = Regex.Replace(text, @"\.{4,}", "... ");

            // Separate sound-effect all-caps run into capitalized words (e.g. "FLAPBlack" -> "FLAP Black")
            text = Regex.Replace(text, @"\b([A-Z]{2,})([A-Z][a-z]{2,})\b", "$1 $2");

            // Separate CamelCase words stuck together across lines or sentences (e.g. "ContentsLife", "CreditsThe", "NewLife", "setsBehind")
            text = Regex.Replace(text, @"(?<!\b(?:Mc|Mac|von|van|de|di))\b([A-Za-z]*[a-z]{2,})([A-Z][a-z]+)\b", "$1 $2");

            // Separate words stuck to chapter markers (e.g. "ContentsLife.0" -> "Contents Life.0")
            text = Regex.Replace(text, @"([A-Za-z])(Life\.\s*\d+)", "$1 $2", RegexOptions.IgnoreCase);

            // Fix common glued words from PDF line transitions
            text = Regex.Replace(text, @"\b(air|sun|moon|sky|sea|fire|water|wind|light|dark|cold|warm|back|head|eyes|face|hand|hands|arms|wings|body|feet|voice|tears)(and|the|is|in|on|at|to|for|with|of|by|from|a|an|was|were)\b", "$1 $2", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\b(sets|stands|looks|walks|runs|falls|comes|goes|drops|dropped|float|floats|turn|turns|glance|glances)(behind|before|after|over|under|into|onto|toward|away|down|up|around)\b", "$1 $2", RegexOptions.IgnoreCase);

            // 11. Final whitespace normalization:
            // Remove excessive spaces within lines
            text = Regex.Replace(text, @"[ \t]+", " ");
            // Trim spaces before punctuation
            text = Regex.Replace(text, @"\s+([,.;:!?])", "$1");
            // Collapse 3 or more consecutive newlines to 2 (standard paragraph break)
            text = Regex.Replace(text, @"\n{3,}", "\n\n");

            return text.Trim();
        }

        private static string NormalizeLigaturesAndPunctuation(string text)
        {
            // Strip dashes directly preceding or following terminal punctuation (e.g. "hair—." -> "hair.")
            text = Regex.Replace(text, @"[—–-]\s*([.!?])", "$1");
            text = Regex.Replace(text, @"([.!?])\s*[—–-]", "$1");

            var sb = new StringBuilder(text);

            // Ligatures
            sb.Replace("ﬁ", "fi");
            sb.Replace("ﬂ", "fl");
            sb.Replace("ﬀ", "ff");
            sb.Replace("ﬃ", "ffi");
            sb.Replace("ﬄ", "ffl");
            sb.Replace("œ", "oe");
            sb.Replace("Œ", "Oe");
            sb.Replace("æ", "ae");
            sb.Replace("Æ", "Ae");

            // Smart Quotes
            sb.Replace("“", "\"");
            sb.Replace("”", "\"");
            sb.Replace("‘", "'");
            sb.Replace("’", "'");
            sb.Replace("«", "\"");
            sb.Replace("»", "\"");

            // Dashes (Replace em-dash / en-dash with comma pause for natural TTS cadence)
            sb.Replace("—", ", ");
            sb.Replace("–", ", ");

            // Ellipsis
            sb.Replace("…", "...");

            // Invisible & non-breaking spaces
            sb.Replace("\u00A0", " ");
            sb.Replace("\u200B", "");
            sb.Replace("\uFEFF", "");

            return sb.ToString();
        }

        private static string ConvertHtmlToPlainText(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return string.Empty;

            // Remove script and style elements completely
            string clean = Regex.Replace(html, @"<script[^>]*>[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
            clean = Regex.Replace(clean, @"<style[^>]*>[\s\S]*?</style>", "", RegexOptions.IgnoreCase);

            // Add paragraph / line breaks for block HTML elements
            clean = Regex.Replace(clean, @"<(?:br|hr)\s*\/?>", "\n", RegexOptions.IgnoreCase);
            clean = Regex.Replace(clean, @"<\/(?:p|div|h[1-6]|li|tr|blockquote)>", "\n\n", RegexOptions.IgnoreCase);

            // Strip remaining HTML tags
            clean = Regex.Replace(clean, @"<[^>]+>", " ");

            // Decode HTML entities (e.g. &amp;, &quot;, &#39;)
            clean = WebUtility.HtmlDecode(clean);

            return clean;
        }

        private static string ExtractHtmlTitle(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return string.Empty;

            // Try <title> tag
            var titleMatch = Regex.Match(html, @"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (titleMatch.Success && !string.IsNullOrWhiteSpace(titleMatch.Groups[1].Value))
            {
                string title = WebUtility.HtmlDecode(Regex.Replace(titleMatch.Groups[1].Value, @"<[^>]+>", "")).Trim();
                if (!string.IsNullOrWhiteSpace(title) && title.Length < 100)
                    return title;
            }

            // Try <h1> or <h2>
            var headerMatch = Regex.Match(html, @"<h[1-2][^>]*>(.*?)</h[1-2]>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (headerMatch.Success && !string.IsNullOrWhiteSpace(headerMatch.Groups[1].Value))
            {
                string header = WebUtility.HtmlDecode(Regex.Replace(headerMatch.Groups[1].Value, @"<[^>]+>", "")).Trim();
                if (!string.IsNullOrWhiteSpace(header) && header.Length < 100)
                    return header;
            }

            return string.Empty;
        }

        #endregion
    }
}
