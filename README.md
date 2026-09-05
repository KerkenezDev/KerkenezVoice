# Kerkenez Voice

[![.NET 10](https://img.shields.io/badge/.NET-10.0%20WinForms-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-blue?logo=windows&logoColor=white)](https://microsoft.com/windows)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Kerkenez Suite](https://img.shields.io/badge/Suite-Kerkenez-0066cc)](https://github.com/KerkenezDev)

> **Modern, high-performance desktop Text-to-Speech (TTS) synthesizer for the Kerkenez Suite, powered by Kokoro-82M, ONNX Runtime, and pure C# DSP audio processing.**

Kerkenez Voice provides a sleek, responsive Windows desktop experience designed in 1-to-1 alignment with **Kerkenez Mail** (custom collapsible navigation sidebar, Segoe Fluent iconography, dual English/Turkish localization, DPI-aware layout, and unified suite configuration storage).

---

## Highlights

- 🚀 **Zero Python / Torch Dependency:** Fully self-contained C# runtime using ONNX Runtime with multi-threaded chunk processing.
- 🎨 **Kerkenez Suite Design:** Fluent collapsible sidebar navigation, card panels, smooth transitions, and responsive layout matching Kerkenez Mail.
- 🌐 **Dual Localization:** Full Turkish and English language switching on the fly.
- 📦 **Separated Suite Storage:** User settings and presets live in `%APPDATA%\Kerkenez\voice`, while heavy model weights and embeddings reside in `%LOCALAPPDATA%\Programs\Kerkenez\voice`.
- ⚡ **Single Portable Executable:** Distributable as a standalone `KerkenezVoice.exe` with automatic model downloader and verification.
- 🎛️ **Pure C# DSP Audio Pipeline:** 14 real-time studio-grade audio effects (Compressor, Limiter, Gain, Parametric EQ, Highpass/Lowpass Filters, Schroeder Reverb, Feedback Delay, Chorus, Phaser, Soft/Hard Distortion, Bitcrush, GSM Codec Emulation, Pitch Resampling).
- 🧬 **Voice Mixing & Custom Blend Lab:** Create novel speaker embeddings by blending any two voices with norm-preserving linear or spherical interpolation.
- 📖 **Pronunciation Lexicon:** Custom regex/case-insensitive pronunciation substitution rules dictionary.
- 📜 **Multispeaker & FX Scripting:** Multi-character audio dramatization with automatic speaker/effect tags (`[SpeakerName:FxPreset]: Dialogue...`).
- 📝 **Document Extraction:** Direct speech synthesis from `.txt`, `.pdf` (PdfPig), and `.epub` (VersOne.Epub) files.
- ⏱️ **Subtitle Generation:** Accurate `.srt` subtitle generation synchronized to synthesized phoneme timing.

---

## Architecture & Configuration

Kerkenez Voice follows the Kerkenez Suite separated storage standard:
- **User Configuration Directory:** `%APPDATA%\Kerkenez\voice\`
  - `config.json` — Synthesis preferences, FX parameters, window scaling, and locale.
  - `presets/` — Voice parameter configurations and speaker profiles.
  - `presets/fx/` — Audio DSP effect chains.
- **Permanent Model & Data Directory:** `%LOCALAPPDATA%\Programs\Kerkenez\voice\`
  - `models/` — Kokoro ONNX model weights (`kokoro-v1.0.onnx`, `voices-v1.0.bin`).
  - `custom_voices/` — User-created blended voice embeddings.
  - `cache/` — Audio synthesis cache chunks.

---

## Getting Started

### 🛠️ Build from Source
**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

Using the build script:
```cmd
build.bat
```

Or using the .NET CLI:
```cmd
dotnet publish KerkenezVoice.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

### 🏃 Running
Using the run script:
```cmd
run.bat
```
Or directly:
```cmd
dotnet run --project KerkenezVoice.csproj
```

---

## Third-Party Notices & Acknowledgments

- **Model:** [Kokoro-82M](https://huggingface.co/hexgrad/Kokoro-82M) by hexgrad (Apache 2.0).
- **Inference Runtime:** [KokoroSharp](https://github.com/nazdridoy/kokoro-tts) & [ONNX Runtime](https://github.com/microsoft/onnxruntime) (MIT).
- **Audio Engine:** [NAudio](https://github.com/naudio/NAudio) (MIT).
- **Document Parsers:** [PdfPig](https://github.com/UglyToad/PdfPig) (Apache 2.0) and [VersOne.Epub](https://github.com/versone/Epub) (MIT).

See [NOTICES](NOTICES) for full license texts and attribution details.

---

## License

Kerkenez Voice is released under the **MIT License**. See [LICENSE](LICENSE) for details.

Copyright (c) 2026 Ahmet İsmail Eraslan.
