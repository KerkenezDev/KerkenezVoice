using System.Text.Json.Serialization;

namespace KokoroGUI.Models
{
    public class VoicePreset
    {
        [JsonPropertyName("voice")]
        public string Voice { get; set; } = "af_heart";

        [JsonPropertyName("speed")]
        public double Speed { get; set; } = 1.0;

        [JsonPropertyName("volume")]
        public double Volume { get; set; } = 1.0;

        [JsonPropertyName("pitch")]
        public double Pitch { get; set; } = 0.0;

        [JsonPropertyName("split_pattern")]
        public string SplitPattern { get; set; } = @"\n+";

        [JsonPropertyName("normalize")]
        public bool Normalize { get; set; } = false;

        [JsonPropertyName("trim")]
        public bool Trim { get; set; } = false;

        [JsonPropertyName("format")]
        public string Format { get; set; } = "wav";

        [JsonPropertyName("apply_fx")]
        public bool ApplyFx { get; set; } = true;

        [JsonPropertyName("fx_preset")]
        public string? FxPreset { get; set; }
    }
}
