namespace KerkenezVoice.Languages
{
    public class TurkishLanguage : BaseLanguage
    {
        public override string Code => "tr";
        public override string Name => "Türkçe";
        public override string EnglishName => "Turkish";
        public override string FlagEmoji => "🇹🇷";

        protected override void InitTranslations()
        {
            // Navigation
            Set(StringKeys.NavSynthesize, "Ses Sentezle");
            Set(StringKeys.NavEbookVoicer, "E-Kitap Seslendirici");
            Set(StringKeys.NavCustomVoices, "Özel Sesler");
            Set(StringKeys.NavAudioFx, "Ses Efektleri");
            Set(StringKeys.NavLexicon, "Sözlük & Telaffuz");
            Set(StringKeys.NavSettings, "Ayarlar");
            Set(StringKeys.NavLiveLogs, "Canlı Günlük");
            Set(StringKeys.NavTipExpandSidebar, "Kenar çubuğunu genişlet");
            Set(StringKeys.NavTipCollapseSidebar, "Kenar çubuğunu daralt");

            // Shell & Window
            Set(StringKeys.AppTitle, "Kerkenez Voice");
            Set(StringKeys.MainShortcutsPromptTitle, "Masaüstü Kısayolları Oluştur");
            Set(StringKeys.MainShortcutsPromptDesc, "Kerkenez Voice için Başlat Menüsü ve Masaüstü kısayolları oluşturulsun mu?");
            Set(StringKeys.StatusStartingUp, "Kerkenez Voice başlatılıyor...");
            Set(StringKeys.StatusReady, "Hazır");
            Set(StringKeys.StatusReadyVoice, "Hazır | Etkin Ses: {0}");
            Set(StringKeys.StatusSynthesizing, "Ses sentezleniyor: {0} (%{1:0.0})");
            Set(StringKeys.StatusPlaying, "Önizleme sesi çalınıyor...");
            Set(StringKeys.StatusModelsRequired, "Model dosyaları eksik. Lütfen modelleri indirin.");
            Set(StringKeys.StatusMetrics, "Ses: {0} | İş Parçacığı: {1} | 24kHz");

            // Synthesize
            Set(StringKeys.SynthTitle, "Metin Seslendirme (TTS)");
            Set(StringKeys.SynthInputSource, "Girdi Kaynağı");
            Set(StringKeys.SynthInputModeText, "Doğrudan Metin");
            Set(StringKeys.SynthInputModeFile, "Belge Yükle");
            Set(StringKeys.SynthDirectTextPlaceholder, "Yüksek kaliteli ses üretmek için metni buraya yazın...");
            Set(StringKeys.SynthSelectFile, "Belge Dosyası Yolu:");
            Set(StringKeys.SynthBrowse, "Gözat...");
            Set(StringKeys.SynthSupportedDocs, "Desteklenen belge formatları: .txt, .pdf, .epub");
            Set(StringKeys.SynthConfiguration, "Seslendirme ve Sentez Ayarları");
            Set(StringKeys.SynthPreset, "Hazır Ayar:");
            Set(StringKeys.SynthSavePreset, "Ayrı Kaydet");
            Set(StringKeys.SynthRefreshPresets, "Yenile");
            Set(StringKeys.SynthLanguage, "Dil:");
            Set(StringKeys.SynthVoice, "Ses:");
            Set(StringKeys.SynthSpeed, "Hız: {0:0.00}x");
            Set(StringKeys.SynthPitch, "Perde: {0:0} st");
            Set(StringKeys.SynthVolume, "Ses Düzeyi: {0:0.00}x");
            Set(StringKeys.SynthFormat, "Format:");
            Set(StringKeys.SynthThreads, "Paralel İş Parçacığı: {0}");
            Set(StringKeys.SynthCombine, "Çıktı Parçalarını Birleştir");
            Set(StringKeys.SynthSeparate, "Tek Tek Parçaları Kaydet");
            Set(StringKeys.SynthSubtitles, "Altyazı Çıkar (.srt)");
            Set(StringKeys.SynthNormalize, "Sesi Normalize Et");
            Set(StringKeys.SynthTrim, "Sessizliği Kırp");
            Set(StringKeys.SynthApplyFx, "Ses Efektlerini (FX) Uygula");
            Set(StringKeys.SynthFxPreset, "FX Hazır Ayarı:");
            Set(StringKeys.SynthBtnPreview, "🔊 Sesi Önizle");
            Set(StringKeys.SynthBtnGenerate, "🎙️ Sesi Sentezle");
            Set(StringKeys.SynthBtnCancel, "⏹️ İptal");
            Set(StringKeys.SynthBtnOpenFolder, "📁 Çıktı Klasörünü Aç");
            Set(StringKeys.SynthStatusComplete, "Seslendirme başarıyla tamamlandı!");
            Set(StringKeys.SynthStatusCancelled, "Seslendirme kullanıcı tarafından iptal edildi.");
            Set(StringKeys.SynthStatusError, "Sentezleme hatası: {0}");

            // Custom Voices
            Set(StringKeys.VoiceMixTitle, "Özel Ses Karıştırma Stüdyosu");
            Set(StringKeys.VoiceMixVoiceA, "Ses A:");
            Set(StringKeys.VoiceMixVoiceB, "Ses B:");
            Set(StringKeys.VoiceMixOperation, "İşlem:");
            Set(StringKeys.VoiceMixRatio, "Karışım Oranı (A: %{0:0}, B: %{1:0}):");
            Set(StringKeys.VoiceMixBtnPreview, "🔊 Karışımı Önizle");
            Set(StringKeys.VoiceMixNewName, "Yeni Ses Adı:");
            Set(StringKeys.VoiceMixBtnCreate, "Özel Sesi Oluştur ve Kaydet");
            Set(StringKeys.VoiceMixListTitle, "Kayıtlı Özel Sesler");
            Set(StringKeys.VoiceMixBtnDelete, "Sesi Sil");
            Set(StringKeys.VoiceMixCreatedSuccess, "Özel ses '{0}' başarıyla kaydedildi!");

            // Audio FX
            Set(StringKeys.FxTitle, "Stüdyo Ses Efektleri Hattı (Saf C# DSP)");
            Set(StringKeys.FxPreset, "FX Hazır Ayarı:");
            Set(StringKeys.FxSavePreset, "Ayrı Kaydet");
            Set(StringKeys.FxRefreshPresets, "Yenile");
            Set(StringKeys.FxApplyMaster, "Ses Efektleri Ana Anahtarı");
            Set(StringKeys.FxDynamicsTitle, "Dinamik İşleme");
            Set(StringKeys.FxComp, "Dinamik Kompresör");
            Set(StringKeys.FxCompThreshold, "Eşik: {0:0} dB");
            Set(StringKeys.FxCompRatio, "Oran: {0:0}:1");
            Set(StringKeys.FxLimiter, "Tepe Sınırlayıcı (Limiter)");
            Set(StringKeys.FxLimiterThreshold, "Eşik: {0:0} dB");
            Set(StringKeys.FxGain, "Kazanç (Gain)");
            Set(StringKeys.FxGainDb, "Artış: {0:+0.0;-0.0;0.0} dB");
            Set(StringKeys.FxEqTitle, "Ekolayzır ve Filtreler");
            Set(StringKeys.FxBass, "Bas EQ: {0:+0;-0;0} dB");
            Set(StringKeys.FxTreble, "Tiz EQ: {0:+0;-0;0} dB");
            Set(StringKeys.FxHpf, "Yüksek Geçiren Filtre: {0:0} Hz");
            Set(StringKeys.FxLpf, "Alçak Geçiren Filtre: {0:0} Hz");
            Set(StringKeys.FxSpatialTitle, "Mekansal ve Zaman Efektleri");
            Set(StringKeys.FxReverb, "Schroeder Reverb");
            Set(StringKeys.FxReverbRoom, "Oda Boyutu: %{0:0}");
            Set(StringKeys.FxReverbWet, "Islak Düzey: %{0:0}");
            Set(StringKeys.FxDelay, "Stereo Gecikme (Delay)");
            Set(StringKeys.FxDelayTime, "Gecikme Süresi: {0:0.00}s");
            Set(StringKeys.FxDelayFeedback, "Geri Bildirim: %{0:0}");
            Set(StringKeys.FxDelayMix, "Karışım: %{0:0}");
            Set(StringKeys.FxModTitle, "Modülasyon, Perde ve Doku");
            Set(StringKeys.FxChorus, "Koro (Chorus)");
            Set(StringKeys.FxChorusRate, "Hız: {0:0.0} Hz");
            Set(StringKeys.FxPhaser, "Faz Kaydırıcı (Phaser)");
            Set(StringKeys.FxPhaserRate, "Hız: {0:0.0} Hz");
            Set(StringKeys.FxDistortion, "Analog Bozulma (Distortion)");
            Set(StringKeys.FxDistortionDrive, "Sürüş: %{0:0}");
            Set(StringKeys.FxClipping, "Yumuşak Doygunluk (Clipping)");
            Set(StringKeys.FxPitchShift, "Perde Kaydırma: {0:+0;-0;0} yarım ton");
            Set(StringKeys.FxBitcrush, "Dijital Bit Ezici (Bitcrush)");
            Set(StringKeys.FxGsm, "GSM Telefon Kodek Emülasyonu");

            // Lexicon
            Set(StringKeys.LexTitle, "Telaffuz Sözlüğü Kuralları");
            Set(StringKeys.LexOriginal, "Kelime / Kısaltma:");
            Set(StringKeys.LexReplacement, "Okunuşu:");
            Set(StringKeys.LexBtnAdd, "Kural Ekle");
            Set(StringKeys.LexListTitle, "Etkin Telaffuz Kuralları");
            Set(StringKeys.LexNote, "Not: Değişiklikler ses sentezinden önce büyük/küçük harf duyarsız olarak uygulanır.");
            Set(StringKeys.LexBtnDelete, "Sil");

            // Settings
            Set(StringKeys.SettingsTitle, "Uygulama Ayarları");
            Set(StringKeys.SettingsSecLanguage, "Dil ve Bölge Tercihleri");
            Set(StringKeys.SettingsLanguageDesc, "Kullanıcı arayüzü görüntüleme dilini seçin.");
            Set(StringKeys.SettingsSecModel, "Yapay Zeka Modeli ve Ağırlık Yönetimi");
            Set(StringKeys.SettingsModelPath, "Model Klasörü: %LOCALAPPDATA%\\Programs\\Kerkenez\\voice\\models");
            Set(StringKeys.SettingsBtnDownloadModels, "Modelleri İndir (Kokoro-82M)");
            Set(StringKeys.SettingsModelsReady, "Model ağırlıkları mevcut ve doğrulandı.");
            Set(StringKeys.SettingsSecVoiceDefaults, "Varsayılan Ses Ayarları");
            Set(StringKeys.SettingsDefaultVoice, "Varsayılan Ses:");
            Set(StringKeys.SettingsDefaultFormat, "Varsayılan Dışa Aktarma Formatı:");
            Set(StringKeys.SettingsSecThreads, "Paralel İşleme");
            Set(StringKeys.SettingsThreadsDesc, "Parça sentezleme için iş parçacığı sayısı (Yüksek değer uzun metinleri hızlandırır, daha fazla RAM kullanır).");
            Set(StringKeys.SettingsSecOutput, "Varsayılan Ses Çıktı Klasörü");
            Set(StringKeys.SettingsOutDir, "Hedef Dizin:");
            Set(StringKeys.SettingsBtnBrowseOutDir, "Gözat...");
            Set(StringKeys.SettingsSecUi, "🖥️  Arayüz ve Düzen");
            Set(StringKeys.SettingsCollapseSidebar, "Kenar çubuğunu varsayılan olarak daraltılmış başlat (kompakt simge rayı)");
            Set(StringKeys.SettingsEbookSplitter, "E-kitap Bölüm Listesi Genişliği (px):");
            Set(StringKeys.SettingsWindowScale, "Pencere Boyut Ölçeği (Ekran alanının %'si):");
            Set(StringKeys.SettingsScalingHeader, "Varsayılan Başlatma Pencere Ölçeği (Ekrana Göre):");
            Set(StringKeys.SettingsScalingDesc, "Uygulama açılışında geçerli monitörün kullanılabilir masaüstü alanının (çalışma alanı) hedef oranı (Varsayılan: %60 genişlik × %56 yükseklik).");
            Set(StringKeys.SettingsWidthScale, "Genişlik Ölçeği (%):");
            Set(StringKeys.SettingsHeightScale, "Yükseklik Ölçeği (%):");
            Set(StringKeys.SettingsResizeActive, "Aktif Pencereyi Yeniden Boyutlandır");
            Set(StringKeys.SettingsPresets, "Hazır Ayarlar:");
            Set(StringKeys.SettingsPresetDefault, "%60 × %56 (Varsayılan)");
            Set(StringKeys.SettingsPresetCompact, "%50 × %50 (Kompakt)");
            Set(StringKeys.SettingsPresetLarge, "%75 × %70 (Geniş)");
            Set(StringKeys.SettingsPresetMax, "%95 × %90 (Neredeyse Tam Ekran)");
            Set(StringKeys.SettingsLaunchDimensions, "Hesaplanan açılış boyutu: {0} × {1} px mevcut ekranda ({2} × {3})");
            Set(StringKeys.SettingsAddShortcuts, "Masaüstü ve Başlat Menüsü Kısayolları Ekle");
            Set(StringKeys.SettingsShortcutsSuccess, "Masaüstü ve Başlat Menüsü kısayolları başarıyla oluşturuldu.");
            Set(StringKeys.SettingsShortcutsError, "Kısayollar oluşturulamadı. Dosya izinlerini kontrol edin.");
            Set(StringKeys.SettingsSecShortcuts, "Sistem Entegrasyonu ve Bakım");
            Set(StringKeys.SettingsCreateShortcuts, "Masaüstü ve Başlat Menüsü Kısayolları Oluştur");
            Set(StringKeys.SettingsBtnSave, "💾 Ayarları Kaydet");
            Set(StringKeys.SettingsBtnReset, "↺ Varsayılanlara Sıfırla");
            Set(StringKeys.SettingsResetConfirm, "Tüm ayarları fabrika varsayılanlarına sıfırlamak istediğinize emin misiniz?");
            Set(StringKeys.SettingsSaved, "Ayarlar başarıyla kaydedildi!");
            Set(StringKeys.CommonSuccess, "Başarılı");
            Set(StringKeys.CommonWarning, "Uyarı");
            Set(StringKeys.CommonDefault, "Varsayılan");
            Set(StringKeys.CommonBrowse, "Gözat...");

            // Logs
            Set(StringKeys.LogsTitle, "Canlı Motor ve Sentez Etkinlik Günlükleri");
            Set(StringKeys.LogsBtnCopy, "Günlükleri Kopyala");
            Set(StringKeys.LogsBtnClear, "Günlüğü Temizle");
            Set(StringKeys.LogsCopied, "Günlükler panoya kopyalandı!");

            // Ebook Voicer Studio
            Set(StringKeys.EbookTitle, "E-Kitap Seslendirici & Sesli Kitap Stüdyosu");
            Set(StringKeys.EbookSubtitle, "EPUB, PDF ve TXT e-kitaplarını gürültü temizleme ve bölüm kontrolüyle doğal sesli kitaplara dönüştürün");
            Set(StringKeys.EbookBtnOpen, "📂 E-Kitap Aç...");
            Set(StringKeys.EbookNoBookLoaded, "Yüklü e-kitap yok");
            Set(StringKeys.EbookSelectBookPrompt, "Bölümleri incelemek, gürültüleri temizlemek ve seslendirmek için bir EPUB, PDF veya TXT dosyası açın.");
            Set(StringKeys.EbookAuthorUnknown, "Bilinmeyen Yazar");
            Set(StringKeys.EbookChapters, "Bölümler ve Kısımlar");
            Set(StringKeys.EbookSelectAll, "Tümünü Seç");
            Set(StringKeys.EbookSelectNone, "Seçimi Kaldır");
            Set(StringKeys.EbookCleanOptions, "Gürültü Temizleme Filtreleri");
            Set(StringKeys.EbookOptHyphenation, "Satır Sonu Tirelemelerini Düzelt");
            Set(StringKeys.EbookOptPageNumbers, "Sayfa Numarası ve Başlıkları Kaldır");
            Set(StringKeys.EbookOptCitations, "Kaynak Alıntılarını [1] Kaldır");
            Set(StringKeys.EbookOptUrls, "Web Bağlantılarını Kaldır");
            Set(StringKeys.EbookOptLigatures, "Harf Birleşimleri & Tireleri Düzelt");
            Set(StringKeys.EbookOptFootnotes, "Dipnot İşaretlerini Kaldır");
            Set(StringKeys.EbookBtnReapplyCleaning, "↺ Metni Yeniden Temizle");
            Set(StringKeys.EbookBtnResetOriginal, "Orijinaline Döndür");
            Set(StringKeys.EbookTextEditorTitle, "Bölüm İçeriği Önizleme ve Düzenleyici");
            Set(StringKeys.EbookVoiceSelected, "🎙️ Seçili Bölümleri Seslendir");
            Set(StringKeys.EbookBtnCancel, "⏹ İptal Et");
            Set(StringKeys.EbookBtnOpenFolder, "📂 Çıktı Klasörünü Aç");
            Set(StringKeys.EbookOptExportIndividual, "Bölümleri ayrı ses dosyaları olarak kaydet");
            Set(StringKeys.EbookOptExportCombined, "Tek bir birleşik sesli kitap dosyası oluştur");
            Set(StringKeys.EbookOptGenerateSubtitles, "Altyazı üret (.srt)");
            Set(StringKeys.EbookOverallProgress, "Genel Sesli Kitap İlerlemesi");
            Set(StringKeys.EbookCurrentChapter, "Mevcut Bölüm");
            Set(StringKeys.EbookStatusReady, "Seslendirmeye hazır.");
            Set(StringKeys.EbookStatusParsing, "E-kitap ayrıştırılıyor ve temizleniyor...");
            Set(StringKeys.EbookStatusSynthesizing, "{0} / {1}. bölüm seslendiriliyor: '{2}'...");
            Set(StringKeys.EbookStatusComplete, "Sesli kitap sentezleme başarıyla tamamlandı!");
            Set(StringKeys.EbookStatusCancelled, "Sesli kitap sentezi iptal edildi.");
        }
    }
}
