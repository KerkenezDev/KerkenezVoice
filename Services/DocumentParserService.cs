using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using VersOne.Epub;

namespace KerkenezVoice.Services
{
    public class DocumentParserService
    {
        public string ExtractText(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File does not exist.", filePath);

            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            if (ext == ".pdf")
            {
                return ExtractFromPdf(filePath);
            }
            else if (ext == ".epub")
            {
                return ExtractFromEpub(filePath);
            }
            else
            {
                // Default to text-based (.txt, etc.)
                return File.ReadAllText(filePath, Encoding.UTF8);
            }
        }

        private string ExtractFromPdf(string filePath)
        {
            var sb = new StringBuilder();
            using var document = PdfDocument.Open(filePath);
            foreach (var page in document.GetPages())
            {
                string text = page.Text;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.AppendLine(text);
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }

        private string ExtractFromEpub(string filePath)
        {
            var sb = new StringBuilder();
            var book = EpubReader.ReadBook(filePath);
            foreach (var textContent in book.ReadingOrder)
            {
                if (!string.IsNullOrWhiteSpace(textContent.Content))
                {
                    string plainText = StripHtmlTags(textContent.Content);
                    if (!string.IsNullOrWhiteSpace(plainText))
                    {
                        sb.AppendLine(plainText);
                        sb.AppendLine();
                    }
                }
            }
            return sb.ToString();
        }

        private string StripHtmlTags(string html)
        {
            string clean = Regex.Replace(html, "<.*?>", " ");
            clean = System.Net.WebUtility.HtmlDecode(clean);
            clean = Regex.Replace(clean, @"\s+", " ").Trim();
            return clean;
        }
    }
}
