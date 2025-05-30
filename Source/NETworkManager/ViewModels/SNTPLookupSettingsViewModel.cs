﻿using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using MahApps.Metro.Controls.Dialogs;
using NETworkManager.Localization.Resources;
using NETworkManager.Models.Network;
using NETworkManager.Settings;
using NETworkManager.Utilities;
using NETworkManager.Views;

namespace NETworkManager.ViewModels;

public class SNTPLookupSettingsViewModel : ViewModelBase1
{
    #region Variables

    private readonly bool _isLoading;

    private readonly ServerConnectionInfo _profileDialogDefaultValues =
        new("time.example.com", 123, TransportProtocol.Tcp);

    private readonly IDialogCoordinator _dialogCoordinator;

    private readonly ICollectionView _sntpServers;

    public ICollectionView SNTPServers
    {
        get => _sntpServers;
        private init
        {
            if (value == _sntpServers)
                return;

            _sntpServers = value;
            OnPropertyChanged();
        }
    }

    private ServerConnectionInfoProfile _selectedSNTPServer = new();

    public ServerConnectionInfoProfile SelectedSNTPServer
    {
        get => _selectedSNTPServer;
        set
        {
            if (value == _selectedSNTPServer)
                return;

            _selectedSNTPServer = value;
            OnPropertyChanged();
        }
    }

    private List<string> ServerInfoProfileNames =>
        SettingsManager.Current.SNTPLookup_SNTPServers.Select(x => x.Name).ToList();

    private int _timeout;

    public int Timeout
    {
        get => _timeout;
        set
        {
            if (value == _timeout)
                return;

            if (!_isLoading)
                SettingsManager.Current.SNTPLookup_Timeout = value;

            _timeout = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region Constructor, load settings

    public SNTPLookupSettingsViewModel(IDialogCoordinator instance)
    {
        _isLoading = true;

        _dialogCoordinator = instance;

        SNTPServers = CollectionViewSource.GetDefaultView(SettingsManager.Current.SNTPLookup_SNTPServers);
        SNTPServers.SortDescriptions.Add(new SortDescription(nameof(ServerConnectionInfoProfile.Name),
            ListSortDirection.Ascending));

        LoadSettings();

        _isLoading = false;
    }

    private void LoadSettings()
    {
        Timeout = SettingsManager.Current.SNTPLookup_Timeout;
    }

    #endregion

    #region ICommand & Actions

    public ICommand AddServerCommand => new RelayCommand(_ => AddServerAction());

    private void AddServerAction()
    {
        AddServer().ConfigureAwait(false);
    }

    public ICommand EditServerCommand => new RelayCommand(_ => EditServerAction());

    private void EditServerAction()
    {
        EditServer().ConfigureAwait(false);
    }

    public ICommand DeleteServerCommand => new RelayCommand(_ => DeleteServerAction(), DeleteServer_CanExecute);

    private bool DeleteServer_CanExecute(object obj)
    {
        return SNTPServers.Cast<ServerConnectionInfoProfile>().Count() > 1;
    }

    private void DeleteServerAction()
    {
        DeleteServer().ConfigureAwait(false);
    }

    #endregion

    #region Methods

    private async Task AddServer()
    {
        var customDialog = new CustomDialog
        {
            Title = Strings.AddSNTPServer
        };

        var viewModel = new ServerConnectionInfoProfileViewModel(instance =>
            {
                _dialogCoordinator.HideMetroDialogAsync(this, customDialog);

                SettingsManager.Current.SNTPLookup_SNTPServers.Add(
                    new ServerConnectionInfoProfile(instance.Name, instance.Servers.ToList()));
            }, _ => { _dialogCoordinator.HideMetroDialogAsync(this, customDialog); },
            (ServerInfoProfileNames, false, false), _profileDialogDefaultValues);

        customDialog.Content = new ServerConnectionInfoProfileDialog
        {
            DataContext = viewModel
        };

        await _dialogCoordinator.ShowMetroDialogAsync(this, customDialog);
    }

    public async Task EditServer()
    {
        var customDialog = new CustomDialog
        {
            Title = Strings.EditSNTPServer
        };

        var viewModel = new ServerConnectionInfoProfileViewModel(instance =>
            {
                _dialogCoordinator.HideMetroDialogAsync(this, customDialog);

                SettingsManager.Current.SNTPLookup_SNTPServers.Remove(SelectedSNTPServer);
                SettingsManager.Current.SNTPLookup_SNTPServers.Add(
                    new ServerConnectionInfoProfile(instance.Name, instance.Servers.ToList()));
            }, _ => { _dialogCoordinator.HideMetroDialogAsync(this, customDialog); },
            (ServerInfoProfileNames, true, false),
            _profileDialogDefaultValues, SelectedSNTPServer);

        customDialog.Content = new ServerConnectionInfoProfileDialog
        {
            DataContext = viewModel
        };

        await _dialogCoordinator.ShowMetroDialogAsync(this, customDialog);
    }

    private async Task DeleteServer()
    {
        var customDialog = new CustomDialog
        {
            Title = Strings.DeleteSNTPServer
        };

        var viewModel = new ConfirmDeleteViewModel(_ =>
            {
                _dialogCoordinator.HideMetroDialogAsync(this, customDialog);

                SettingsManager.Current.SNTPLookup_SNTPServers.Remove(SelectedSNTPServer);
            }, _ => { _dialogCoordinator.HideMetroDialogAsync(this, customDialog); },
            Strings.DeleteSNTPServerMessage);

        customDialog.Content = new ConfirmDeleteDialog
        {
            DataContext = viewModel
        };

        await _dialogCoordinator.ShowMetroDialogAsync(this, customDialog);
    }

    #endregion
}