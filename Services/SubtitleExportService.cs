using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace KerkenezVoice.Services
{
    public class SubtitleSegment
    {
        public string Text { get; set; } = string.Empty;
        public double Duration { get; set; }
    }

    public class SubtitleExportService
    {
        public bool GenerateSrt(IList<SubtitleSegment> segments, string outputPath)
        {
            try
            {
                using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
                double currentTime = 0.0;

                for (int i = 0; i < segments.Count; i++)
                {
                    double start = currentTime;
                    double end = currentTime + segments[i].Duration;

                    writer.WriteLine((i + 1).ToString());
                    writer.WriteLine($"{FormatTime(start)} --> {FormatTime(end)}");
                    writer.WriteLine(segments[i].Text.Trim());
                    writer.WriteLine();

                    currentTime = end;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to generate SRT: {ex.Message}");
                return false;
            }
        }

        private static string FormatTime(double totalSeconds)
        {
            int hours = (int)(totalSeconds / 3600);
            int minutes = (int)((totalSeconds % 3600) / 60);
            int seconds = (int)(totalSeconds % 60);
            int millis = (int)Math.Round((totalSeconds - Math.Truncate(totalSeconds)) * 1000);
            if (millis >= 1000) millis = 999;

            return $"{hours:D2}:{minutes:D2}:{seconds:D2},{millis:D3}";
        }
    }
}
