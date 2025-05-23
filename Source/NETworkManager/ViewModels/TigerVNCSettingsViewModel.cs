﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using MahApps.Metro.Controls.Dialogs;
using NETworkManager.Localization.Resources;
using NETworkManager.Settings;
using NETworkManager.Utilities;

namespace NETworkManager.ViewModels;

public class TigerVNCSettingsViewModel : ViewModelBase1
{
    #region Variables

    private readonly IDialogCoordinator _dialogCoordinator;

    private readonly bool _isLoading;

    private string _applicationFilePath;

    public string ApplicationFilePath
    {
        get => _applicationFilePath;
        set
        {
            if (value == _applicationFilePath)
                return;

            if (!_isLoading)
                SettingsManager.Current.TigerVNC_ApplicationFilePath = value;

            IsConfigured = !string.IsNullOrEmpty(value);

            _applicationFilePath = value;
            OnPropertyChanged();
        }
    }

    private bool _isConfigured;

    public bool IsConfigured
    {
        get => _isConfigured;
        set
        {
            if (value == _isConfigured)
                return;

            _isConfigured = value;
            OnPropertyChanged();
        }
    }

    private int _port;

    public int Port
    {
        get => _port;
        set
        {
            if (value == _port)
                return;

            if (!_isLoading)
                SettingsManager.Current.TigerVNC_Port = value;

            _port = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region Contructor, load settings

    public TigerVNCSettingsViewModel(IDialogCoordinator instance)
    {
        _isLoading = true;

        _dialogCoordinator = instance;

        LoadSettings();

        _isLoading = false;
    }

    private void LoadSettings()
    {
        ApplicationFilePath = SettingsManager.Current.TigerVNC_ApplicationFilePath;
        IsConfigured = File.Exists(ApplicationFilePath);
        Port = SettingsManager.Current.TigerVNC_Port;
    }

    #endregion

    #region ICommands & Actions

    public ICommand BrowseFileCommand => new RelayCommand(_ => BrowseFileAction());

    private void BrowseFileAction()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = GlobalStaticConfiguration.ApplicationFileExtensionFilter
        };

        if (openFileDialog.ShowDialog() == DialogResult.OK)
            ApplicationFilePath = openFileDialog.FileName;
    }

    public ICommand ConfigureCommand => new RelayCommand(_ => ConfigureAction());

    private void ConfigureAction()
    {
        Configure().ConfigureAwait(false);
    }

    #endregion

    #region Methods

    private async Task Configure()
    {
        try
        {
            Process.Start(SettingsManager.Current.TigerVNC_ApplicationFilePath);
        }
        catch (Exception ex)
        {
            var settings = AppearanceManager.MetroDialog;

            settings.AffirmativeButtonText = Strings.OK;

            await _dialogCoordinator.ShowMessageAsync(this, Strings.Error, ex.Message,
                MessageDialogStyle.Affirmative, settings);
        }
    }

    public void SetFilePathFromDragDrop(string filePath)
    {
        ApplicationFilePath = filePath;

        OnPropertyChanged(nameof(ApplicationFilePath));
    }

    #endregion
}