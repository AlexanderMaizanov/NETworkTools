﻿using NETworkManager.Settings;

namespace NETworkManager.ViewModels;

public class SettingsStatusViewModel : ViewModelBase1
{
    #region Variables

    private readonly bool _isLoading;

    private bool _showWindowOnNetworkChange;

    public bool ShowWindowOnNetworkChange
    {
        get => _showWindowOnNetworkChange;
        set
        {
            if (value == _showWindowOnNetworkChange)
                return;

            if (!_isLoading)
                SettingsManager.Current.Status_ShowWindowOnNetworkChange = value;

            _showWindowOnNetworkChange = value;
            OnPropertyChanged();
        }
    }

    private int _windowCloseTime;

    public int WindowCloseTime
    {
        get => _windowCloseTime;
        set
        {
            if (value == _windowCloseTime)
                return;

            if (!_isLoading)
                SettingsManager.Current.Status_WindowCloseTime = value;

            _windowCloseTime = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region Contructor, load settings

    public SettingsStatusViewModel()
    {
        _isLoading = true;

        LoadSettings();

        _isLoading = false;
    }

    private void LoadSettings()
    {
        ShowWindowOnNetworkChange = SettingsManager.Current.Status_ShowWindowOnNetworkChange;
        WindowCloseTime = SettingsManager.Current.Status_WindowCloseTime;
    }

    #endregion
}