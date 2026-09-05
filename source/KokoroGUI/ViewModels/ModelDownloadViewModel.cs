using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using KokoroGUI.Services;

namespace KokoroGUI.ViewModels
{
    public class ModelDownloadViewModel : ViewModelBase
    {
        private readonly ModelManagerService _modelManager;
        private string _statusMessage = "Starting download...";
        private string _fileName = string.Empty;
        private double _progressPercent = 0.0;
        private string _downloadDetails = string.Empty;
        private bool _isDownloading = true;
        private bool _hasError = false;
        private CancellationTokenSource _cts = new();

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string FileName
        {
            get => _fileName;
            set => SetProperty(ref _fileName, value);
        }

        public double ProgressPercent
        {
            get => _progressPercent;
            set => SetProperty(ref _progressPercent, value);
        }

        public string DownloadDetails
        {
            get => _downloadDetails;
            set => SetProperty(ref _downloadDetails, value);
        }

        public bool IsDownloading
        {
            get => _isDownloading;
            set => SetProperty(ref _isDownloading, value);
        }

        public bool HasError
        {
            get => _hasError;
            set => SetProperty(ref _hasError, value);
        }

        public RelayCommand CancelCommand { get; }

        public event Action? DownloadCompleted;

        public ModelDownloadViewModel(ModelManagerService modelManager)
        {
            _modelManager = modelManager;
            CancelCommand = new RelayCommand(() =>
            {
                _cts.Cancel();
            });
        }

        public async Task StartDownloadAsync()
        {
            IsDownloading = true;
            HasError = false;

            var progress = new Progress<(string file, long downloaded, long total, double percent)>(info =>
            {
                FileName = $"Downloading: {info.file}";
                ProgressPercent = info.percent;
                double mbDownloaded = info.downloaded / (1024.0 * 1024.0);
                double mbTotal = info.total / (1024.0 * 1024.0);
                DownloadDetails = info.total > 0
                    ? $"{mbDownloaded:F1} MB / {mbTotal:F1} MB ({info.percent:F1}%)"
                    : $"{mbDownloaded:F1} MB downloaded";
                StatusMessage = "Downloading model weights from repository...";
            });

            try
            {
                await _modelManager.DownloadModelsAsync(progress, _cts.Token);
                StatusMessage = "Download complete!";
                IsDownloading = false;
                DownloadCompleted?.Invoke();
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Download cancelled.";
                HasError = true;
                IsDownloading = false;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Download failed: {ex.Message}";
                HasError = true;
                IsDownloading = false;
            }
        }
    }
}
