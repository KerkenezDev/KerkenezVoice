using System;
using System.Collections.ObjectModel;
using KokoroGUI.Models;

namespace KokoroGUI.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly AppSettings _settings;
        private readonly Action _onSaveCallback;

        public ObservableCollection<string> AppearanceModes { get; } = new() { "System", "Dark", "Light" };
        public ObservableCollection<string> ScalingOptions { get; } = new() { "80%", "90%", "100%", "110%", "120%" };

        public string SelectedAppearance
        {
            get => _settings.Appearance;
            set
            {
                if (_settings.Appearance != value)
                {
                    _settings.Appearance = value;
                    OnPropertyChanged();
                    _onSaveCallback();
                }
            }
        }

        public string SelectedScaling
        {
            get => _settings.Scaling;
            set
            {
                if (_settings.Scaling != value)
                {
                    _settings.Scaling = value;
                    OnPropertyChanged();
                    _onSaveCallback();
                }
            }
        }

        public bool CachingEnabled
        {
            get => _settings.Caching;
            set
            {
                if (_settings.Caching != value)
                {
                    _settings.Caching = value;
                    OnPropertyChanged();
                    _onSaveCallback();
                }
            }
        }

        public bool JitEnabled
        {
            get => _settings.JitEnabled;
            set
            {
                if (_settings.JitEnabled != value)
                {
                    _settings.JitEnabled = value;
                    OnPropertyChanged();
                    _onSaveCallback();
                }
            }
        }

        public SettingsViewModel(AppSettings settings, Action onSaveCallback)
        {
            _settings = settings;
            _onSaveCallback = onSaveCallback;
        }
    }
}
