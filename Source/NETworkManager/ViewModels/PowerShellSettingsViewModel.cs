﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using MahApps.Metro.Controls.Dialogs;
using NETworkManager.Localization.Resources;
using NETworkManager.Models.PowerShell;
using NETworkManager.Settings;
using NETworkManager.Utilities;

namespace NETworkManager.ViewModels;

public class PowerShellSettingsViewModel : ViewModelBase1
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
                SettingsManager.Current.PowerShell_ApplicationFilePath = value;

            IsConfigured = !string.IsNullOrEmpty(value);

            _applicationFilePath = value;
            OnPropertyChanged();
        }
    }

    private string _command;

    public string Command
    {
        get => _command;
        set
        {
            if (value == _command)
                return;

            if (!_isLoading)
                SettingsManager.Current.PowerShell_Command = value;

            _command = value;
            OnPropertyChanged();
        }
    }

    private string _additionalCommandLine;

    public string AdditionalCommandLine
    {
        get => _additionalCommandLine;
        set
        {
            if (value == _additionalCommandLine)
                return;

            if (!_isLoading)
                SettingsManager.Current.PowerShell_AdditionalCommandLine = value;

            _additionalCommandLine = value;
            OnPropertyChanged();
        }
    }

    private List<ExecutionPolicy> _executionPolicies = new();

    public List<ExecutionPolicy> ExecutionPolicies
    {
        get => _executionPolicies;
        set
        {
            if (value == _executionPolicies)
                return;

            _executionPolicies = value;
            OnPropertyChanged();
        }
    }

    private ExecutionPolicy _executionPolicy;

    public ExecutionPolicy ExecutionPolicy
    {
        get => _executionPolicy;
        set
        {
            if (value == _executionPolicy)
                return;

            if (!_isLoading)
                SettingsManager.Current.PowerShell_ExecutionPolicy = value;

            _executionPolicy = value;
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

    #endregion

    #region Contructor, load settings

    public PowerShellSettingsViewModel(IDialogCoordinator instance)
    {
        _isLoading = true;

        _dialogCoordinator = instance;

        LoadSettings();

        _isLoading = false;
    }

    private void LoadSettings()
    {
        ApplicationFilePath = SettingsManager.Current.PowerShell_ApplicationFilePath;
        IsConfigured = File.Exists(ApplicationFilePath);
        Command = SettingsManager.Current.PowerShell_Command;
        AdditionalCommandLine = SettingsManager.Current.PowerShell_AdditionalCommandLine;

        LoadExecutionPolicies();
    }

    private void LoadExecutionPolicies()
    {
        ExecutionPolicies = Enum.GetValues(typeof(ExecutionPolicy)).Cast<ExecutionPolicy>().ToList();
        ExecutionPolicy =
            ExecutionPolicies.FirstOrDefault(x => x == SettingsManager.Current.PowerShell_ExecutionPolicy);
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
            Process.Start(SettingsManager.Current.PowerShell_ApplicationFilePath);
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