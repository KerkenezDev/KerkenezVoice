using System.Text.Json.Serialization;

namespace KokoroGUI.Models
{
    public class AppSettings
    {
        [JsonPropertyName("appearance")]
        public string Appearance { get; set; } = "Dark";

        [JsonPropertyName("scaling")]
        public string Scaling { get; set; } = "100%";

        [JsonPropertyName("lang_code")]
        public string LangCode { get; set; } = "a";

        [JsonPropertyName("voice")]
        public string Voice { get; set; } = "af_heart";

        [JsonPropertyName("filename")]
        public string Filename { get; set; } = "output";

        [JsonPropertyName("format")]
        public string Format { get; set; } = "wav";

        [JsonPropertyName("out_dir")]
        public string OutDir { get; set; } = "audio_output";

        [JsonPropertyName("speed")]
        public double Speed { get; set; } = 1.0;

        [JsonPropertyName("volume")]
        public double Volume { get; set; } = 1.0;

        [JsonPropertyName("pitch")]
        public double Pitch { get; set; } = 0.0;

        [JsonPropertyName("num_threads")]
        public int NumThreads { get; set; } = 1;

        [JsonPropertyName("split_pattern")]
        public string SplitPattern { get; set; } = @"\n+";

        [JsonPropertyName("separate")]
        public bool Separate { get; set; } = false;

        [JsonPropertyName("combine")]
        public bool Combine { get; set; } = true;

        [JsonPropertyName("export_subtitles")]
        public bool ExportSubtitles { get; set; } = false;

        [JsonPropertyName("caching")]
        public bool Caching { get; set; } = true;

        [JsonPropertyName("jit_enabled")]
        public bool JitEnabled { get; set; } = false;

        [JsonPropertyName("normalize")]
        public bool Normalize { get; set; } = false;

        [JsonPropertyName("trim")]
        public bool Trim { get; set; } = false;

        [JsonPropertyName("apply_fx")]
        public bool ApplyFx { get; set; } = true;

        [JsonPropertyName("reverb_enabled")]
        public bool ReverbEnabled { get; set; } = false;

        [JsonPropertyName("reverb_room_size")]
        public double ReverbRoomSize { get; set; } = 0.5;

        [JsonPropertyName("reverb_wet_level")]
        public double ReverbWetLevel { get; set; } = 0.3;

        [JsonPropertyName("reverb_damping")]
        public double ReverbDamping { get; set; } = 0.5;

        [JsonPropertyName("reverb_dry_level")]
        public double ReverbDryLevel { get; set; } = 1.0;

        [JsonPropertyName("reverb_width")]
        public double ReverbWidth { get; set; } = 1.0;

        [JsonPropertyName("eq_bass")]
        public double EqBass { get; set; } = 0.0;

        [JsonPropertyName("eq_treble")]
        public double EqTreble { get; set; } = 0.0;

        [JsonPropertyName("comp_enabled")]
        public bool CompEnabled { get; set; } = false;

        [JsonPropertyName("comp_threshold")]
        public double CompThreshold { get; set; } = -20.0;

        [JsonPropertyName("comp_ratio")]
        public double CompRatio { get; set; } = 4.0;

        [JsonPropertyName("comp_attack")]
        public double CompAttack { get; set; } = 1.0;

        [JsonPropertyName("comp_release")]
        public double CompRelease { get; set; } = 100.0;

        [JsonPropertyName("distortion_enabled")]
        public bool DistortionEnabled { get; set; } = false;

        [JsonPropertyName("distortion_drive")]
        public double DistortionDrive { get; set; } = 25.0;

        [JsonPropertyName("chorus_enabled")]
        public bool ChorusEnabled { get; set; } = false;

        [JsonPropertyName("chorus_rate")]
        public double ChorusRate { get; set; } = 1.0;

        [JsonPropertyName("chorus_depth")]
        public double ChorusDepth { get; set; } = 0.25;

        [JsonPropertyName("chorus_mix")]
        public double ChorusMix { get; set; } = 0.5;

        [JsonPropertyName("phaser_enabled")]
        public bool PhaserEnabled { get; set; } = false;

        [JsonPropertyName("phaser_rate")]
        public double PhaserRate { get; set; } = 1.0;

        [JsonPropertyName("phaser_depth")]
        public double PhaserDepth { get; set; } = 0.5;

        [JsonPropertyName("phaser_mix")]
        public double PhaserMix { get; set; } = 0.5;

        [JsonPropertyName("clipping_enabled")]
        public bool ClippingEnabled { get; set; } = false;

        [JsonPropertyName("clipping_thresh")]
        public double ClippingThresh { get; set; } = -6.0;

        [JsonPropertyName("bitcrush_enabled")]
        public bool BitcrushEnabled { get; set; } = false;

        [JsonPropertyName("bitcrush_depth")]
        public double BitcrushDepth { get; set; } = 8.0;

        [JsonPropertyName("gsm_enabled")]
        public bool GsmEnabled { get; set; } = false;

        [JsonPropertyName("highpass_enabled")]
        public bool HighpassEnabled { get; set; } = false;

        [JsonPropertyName("highpass_freq")]
        public double HighpassFreq { get; set; } = 50.0;

        [JsonPropertyName("lowpass_enabled")]
        public bool LowpassEnabled { get; set; } = false;

        [JsonPropertyName("lowpass_freq")]
        public double LowpassFreq { get; set; } = 10000.0;

        [JsonPropertyName("delay_enabled")]
        public bool DelayEnabled { get; set; } = false;

        [JsonPropertyName("delay_time")]
        public double DelayTime { get; set; } = 0.5;

        [JsonPropertyName("delay_feedback")]
        public double DelayFeedback { get; set; } = 0.0;

        [JsonPropertyName("delay_mix")]
        public double DelayMix { get; set; } = 0.5;

        [JsonPropertyName("pitch_shift_enabled")]
        public bool PitchShiftEnabled { get; set; } = false;

        [JsonPropertyName("pitch_shift_semitones")]
        public double PitchShiftSemitones { get; set; } = 0.0;

        [JsonPropertyName("limiter_enabled")]
        public bool LimiterEnabled { get; set; } = false;

        [JsonPropertyName("limiter_threshold")]
        public double LimiterThreshold { get; set; } = -1.0;

        [JsonPropertyName("limiter_release")]
        public double LimiterRelease { get; set; } = 100.0;

        [JsonPropertyName("gain_enabled")]
        public bool GainEnabled { get; set; } = false;

        [JsonPropertyName("gain_db")]
        public double GainDb { get; set; } = 0.0;

        [JsonPropertyName("lexicon")]
        public Dictionary<string, string> Lexicon { get; set; } = new();
    }
}
