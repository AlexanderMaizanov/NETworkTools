using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using NETworkManager.Localization.Resources;
using NETworkManager.Models;
using NETworkManager.Models.EventSystem;
using NETworkManager.Models.Export;
using NETworkManager.Models.Network;
using NETworkManager.Profiles;
using NETworkManager.Settings;
using NETworkManager.Utilities;
using NETworkManager.Views;
using NetworkInterface = NETworkManager.Models.Network.NetworkInterface;

namespace NETworkManager.ViewModels;

public class NetworkInterfaceViewModel : ViewModelBase1, IProfileManager
{
    #region Variables

    public Axis[] Radion1XAxes { get; set; } = [];

    public Axis[] Radion1YAxes { get; set; } = [];

    public ObservableCollection<ISeries> Series { get; set; } =
    [
        new LineSeries<double>
        {
            Values = [],
            Fill = null
        }
    ];


    private readonly IDialogCoordinator _dialogCoordinator;
    private readonly DispatcherTimer _searchDispatcherTimer = new();
    private BandwidthMeter _bandwidthMeter;

    private readonly bool _isLoading;
    private bool _isViewActive = true;

    private bool _isNetworkInterfaceLoading;

    public bool IsNetworkInterfaceLoading
    {
        get => _isNetworkInterfaceLoading;
        set
        {
            if (value == _isNetworkInterfaceLoading)
                return;

            _isNetworkInterfaceLoading = value;
            OnPropertyChanged();
        }
    }

    private bool _canConfigure;

    public bool CanConfigure
    {
        get => _canConfigure;
        set
        {
            if (value == _canConfigure)
                return;

            _canConfigure = value;
            OnPropertyChanged();
        }
    }

    private bool _isConfigurationRunning;

    public bool IsConfigurationRunning
    {
        get => _isConfigurationRunning;
        set
        {
            if (value == _isConfigurationRunning)
                return;

            _isConfigurationRunning = value;
            OnPropertyChanged();
        }
    }

    private bool _isStatusMessageDisplayed;

    public bool IsStatusMessageDisplayed
    {
        get => _isStatusMessageDisplayed;
        set
        {
            if (value == _isStatusMessageDisplayed)
                return;

            _isStatusMessageDisplayed = value;
            OnPropertyChanged();
        }
    }

    private string _statusMessage;

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (value == _statusMessage)
                return;

            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    #region NetworkInterfaces, SelectedNetworkInterface

    private List<NetworkInterfaceInfo> _networkInterfaces;

    public List<NetworkInterfaceInfo> NetworkInterfaces
    {
        get => _networkInterfaces;
        private set
        {
            if (value == _networkInterfaces)
                return;

            _networkInterfaces = value;
            OnPropertyChanged();
        }
    }

    private NetworkInterfaceInfo _selectedNetworkInterface;

    public NetworkInterfaceInfo SelectedNetworkInterface
    {
        get => _selectedNetworkInterface;
        set
        {
            if (value == _selectedNetworkInterface)
                return;

            if (value != null)
            {
                if (!_isLoading)
                    SettingsManager.Current.NetworkInterface_InterfaceId = value.Id;

                // Bandwidth
                StopBandwidthMeter();
                StartBandwidthMeter(value.Id);

                // Configuration
                SetConfigurationDefaults(value);

                CanConfigure = value.IsOperational;
            }

            _selectedNetworkInterface = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region Bandwidth

    private long _bandwidthTotalBytesSentTemp;

    private long _bandwidthTotalBytesSent;

    public long BandwidthTotalBytesSent
    {
        get => _bandwidthTotalBytesSent;
        set
        {
            if (value == _bandwidthTotalBytesSent)
                return;

            _bandwidthTotalBytesSent = value;
            OnPropertyChanged();
        }
    }

    private long _bandwidthTotalBytesReceivedTemp;
    private long _bandwidthTotalBytesReceived;

    public long BandwidthTotalBytesReceived
    {
        get => _bandwidthTotalBytesReceived;
        set
        {
            if (value == _bandwidthTotalBytesReceived)
                return;

            _bandwidthTotalBytesReceived = value;
            OnPropertyChanged();
        }
    }

    private long _bandwidthDiffBytesSent;

    public long BandwidthDiffBytesSent
    {
        get => _bandwidthDiffBytesSent;
        set
        {
            if (value == _bandwidthDiffBytesSent)
                return;

            _bandwidthDiffBytesSent = value;
            OnPropertyChanged();
        }
    }

    private long _bandwidthDiffBytesReceived;

    public long BandwidthDiffBytesReceived
    {
        get => _bandwidthDiffBytesReceived;
        set
        {
            if (value == _bandwidthDiffBytesReceived)
                return;

            _bandwidthDiffBytesReceived = value;
            OnPropertyChanged();
        }
    }

    private long _bandwidthBytesReceivedSpeed;

    public long BandwidthBytesReceivedSpeed
    {
        get => _bandwidthBytesReceivedSpeed;
        set
        {
            if (value == _bandwidthBytesReceivedSpeed)
                return;

            _bandwidthBytesReceivedSpeed = value;
            OnPropertyChanged();
        }
    }

    private long _bandwidthBytesSentSpeed;

    public long BandwidthBytesSentSpeed
    {
        get => _bandwidthBytesSentSpeed;
        set
        {
            if (value == _bandwidthBytesSentSpeed)
                return;

            _bandwidthBytesSentSpeed = value;
            OnPropertyChanged();
        }
    }

    private DateTime _bandwidthStartTime;

    public DateTime BandwidthStartTime
    {
        get => _bandwidthStartTime;
        set
        {
            if (value == _bandwidthStartTime)
                return;

            _bandwidthStartTime = value;
            OnPropertyChanged();
        }
    }

    private TimeSpan _bandwidthMeasuredTime;

    public TimeSpan BandwidthMeasuredTime
    {
        get => _bandwidthMeasuredTime;
        set
        {
            if (value == _bandwidthMeasuredTime)
                return;

            _bandwidthMeasuredTime = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region Config

    private bool _configEnableDynamicIPAddress = true;

    public bool ConfigEnableDynamicIPAddress
    {
        get => _configEnableDynamicIPAddress;
        set
        {
            if (value == _configEnableDynamicIPAddress)
                return;

            _configEnableDynamicIPAddress = value;
            OnPropertyChanged();
        }
    }

    private bool _configEnableStaticIPAddress;

    public bool ConfigEnableStaticIPAddress
    {
        get => _configEnableStaticIPAddress;
        set
        {
            if (value == _configEnableStaticIPAddress)
                return;

            ConfigEnableStaticDNS = true;

            _configEnableStaticIPAddress = value;
            OnPropertyChanged();
        }
    }

    private string _configIPAddress;

    public string ConfigIPAddress
    {
        get => _configIPAddress;
        set
        {
            if (value == _configIPAddress)
                return;

            _configIPAddress = value;
            OnPropertyChanged();
        }
    }

    private string _configSubnetmask;

    public string ConfigSubnetmask
    {
        get => _configSubnetmask;
        set
        {
            if (value == _configSubnetmask)
                return;

            _configSubnetmask = value;
            OnPropertyChanged();
        }
    }

    private string _configGateway;

    public string ConfigGateway
    {
        get => _configGateway;
        set
        {
            if (value == _configGateway)
                return;

            _configGateway = value;
            OnPropertyChanged();
        }
    }

    private bool _configEnableDynamicDNS = true;

    public bool ConfigEnableDynamicDNS
    {
        get => _configEnableDynamicDNS;
        set
        {
            if (value == _configEnableDynamicDNS)
                return;

            _configEnableDynamicDNS = value;
            OnPropertyChanged();
        }
    }

    private bool _configEnableStaticDNS;

    public bool ConfigEnableStaticDNS
    {
        get => _configEnableStaticDNS;
        set
        {
            if (value == _configEnableStaticDNS)
                return;

            _configEnableStaticDNS = value;
            OnPropertyChanged();
        }
    }

    private string _configPrimaryDNSServer;

    public string ConfigPrimaryDNSServer
    {
        get => _configPrimaryDNSServer;
        set
        {
            if (value == _configPrimaryDNSServer)
                return;

            _configPrimaryDNSServer = value;
            OnPropertyChanged();
        }
    }

    private string _configSecondaryDNSServer;

    public string ConfigSecondaryDNSServer
    {
        get => _configSecondaryDNSServer;
        set
        {
            if (value == _configSecondaryDNSServer)
                return;

            _configSecondaryDNSServer = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region Profiles

    private ICollectionView _profiles;

    public ICollectionView Profiles
    {
        get => _profiles;
        private set
        {
            if (value == _profiles)
                return;

            _profiles = value;
            OnPropertyChanged();
        }
    }

    private ProfileInfo _selectedProfile = new();

    public ProfileInfo SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (value == _selectedProfile)
                return;

            if (value != null)
            {
                ConfigEnableDynamicIPAddress = !value.NetworkInterface_EnableStaticIPAddress;
                ConfigEnableStaticIPAddress = value.NetworkInterface_EnableStaticIPAddress;
                ConfigIPAddress = value.NetworkInterface_IPAddress;
                ConfigGateway = value.NetworkInterface_Gateway;
                ConfigSubnetmask = value.NetworkInterface_Subnetmask;
                ConfigEnableDynamicDNS = !value.NetworkInterface_EnableStaticDNS;
                ConfigEnableStaticDNS = value.NetworkInterface_EnableStaticDNS;
                ConfigPrimaryDNSServer = value.NetworkInterface_PrimaryDNSServer;
                ConfigSecondaryDNSServer = value.NetworkInterface_SecondaryDNSServer;
            }

            _selectedProfile = value;
            OnPropertyChanged();
        }
    }

    private string _search;

    public string Search
    {
        get => _search;
        set
        {
            if (value == _search)
                return;

            _search = value;

            // Start searching...
            IsSearching = true;
            _searchDispatcherTimer.Start();

            OnPropertyChanged();
        }
    }

    private bool _isSearching;

    public bool IsSearching
    {
        get => _isSearching;
        set
        {
            if (value == _isSearching)
                return;

            _isSearching = value;
            OnPropertyChanged();
        }
    }

    private bool _canProfileWidthChange = true;
    private double _tempProfileWidth;

    private bool _expandProfileView;

    public bool ExpandProfileView
    {
        get => _expandProfileView;
        set
        {
            if (value == _expandProfileView)
                return;

            if (!_isLoading)
                SettingsManager.Current.NetworkInterface_ExpandProfileView = value;

            _expandProfileView = value;

            if (_canProfileWidthChange)
                ResizeProfile(false);

            OnPropertyChanged();
        }
    }

    private GridLength _profileWidth;

    public GridLength ProfileWidth
    {
        get => _profileWidth;
        set
        {
            if (value == _profileWidth)
                return;

            if (!_isLoading && Math.Abs(value.Value - GlobalStaticConfiguration.Profile_WidthCollapsed) >
                GlobalStaticConfiguration.Profile_FloatPointFix) // Do not save the size when collapsed
                SettingsManager.Current.NetworkInterface_ProfileWidth = value.Value;

            _profileWidth = value;

            if (_canProfileWidthChange)
                ResizeProfile(true);

            OnPropertyChanged();
        }
    }

    #endregion

    #endregion

    #region Constructor, LoadSettings, OnShutdown

    public NetworkInterfaceViewModel(IDialogCoordinator instance)
    {
        _isLoading = true;

        _dialogCoordinator = instance;

        LoadNetworkInterfaces().SafeFireAndForget(ConfigureAwaitOptions.None);

        InitialBandwidthChart();

        // Profiles
        SetProfilesView();

        ProfileManager.OnProfilesUpdated += ProfileManager_OnProfilesUpdated;

        _searchDispatcherTimer.Interval = GlobalStaticConfiguration.SearchDispatcherTimerTimeSpan;
        _searchDispatcherTimer.Tick += SearchDispatcherTimer_Tick;

        // Detect if network address or status changed...
        NetworkChange.NetworkAvailabilityChanged += (_, _) => ReloadNetworkInterfacesAction();
        NetworkChange.NetworkAddressChanged += (_, _) => ReloadNetworkInterfacesAction();

        LoadSettings();

        _isLoading = false;
    }

    private void InitialBandwidthChart()
    {
        Series =
        [
            new LineSeries<LvlChartsDefaultInfo>
            {
                Name = "Download",
                Values = [],
                Mapping = (day, index) => new ((double)day.DateTime.Ticks / TimeSpan.FromHours(1).Ticks, day.Value)

            },
            new LineSeries<LvlChartsDefaultInfo>
            {
                Name = "Upload",
                Values = [],
                Mapping = (day, index) => new ((double)day.DateTime.Ticks / TimeSpan.FromHours(1).Ticks, day.Value)
            }
        ];

        FormatterDate = value =>
            DateTimeHelper.DateTimeToTimeString(new DateTime((long)(value * TimeSpan.FromHours(1).Ticks)));
        FormatterSpeed = value => $"{FileSizeConverter.GetBytesReadable((long)value * 8)}it/s";
    }

    public Func<double, string> FormatterDate { get; set; }
    public Func<double, string> FormatterSpeed { get; set; }
    //public SeriesCollection Series { get; set; }

    private async Task LoadNetworkInterfaces()
    {
        IsNetworkInterfaceLoading = true;

        NetworkInterfaces = await NetworkInterface.GetNetworkInterfacesAsync(CancellationTokenSource.Token);

        // Get the last selected interface, if it is still available on this machine...
        if (NetworkInterfaces.Count > 0)
        {
            var info = NetworkInterfaces.FirstOrDefault(s =>
                s.Id == SettingsManager.Current.NetworkInterface_InterfaceId);

            SelectedNetworkInterface = info ?? NetworkInterfaces[0];
        }

        IsNetworkInterfaceLoading = false;
    }

    private void LoadSettings()
    {
        ExpandProfileView = SettingsManager.Current.NetworkInterface_ExpandProfileView;

        ProfileWidth = ExpandProfileView
            ? new GridLength(SettingsManager.Current.NetworkInterface_ProfileWidth)
            : new GridLength(GlobalStaticConfiguration.Profile_WidthCollapsed);

        _tempProfileWidth = SettingsManager.Current.NetworkInterface_ProfileWidth;
    }

    #endregion

    #region ICommands & Actions

    public ICommand ReloadNetworkInterfacesCommand =>
        new RelayCommand(_ => ReloadNetworkInterfacesAction(), ReloadNetworkInterfaces_CanExecute);

    private bool ReloadNetworkInterfaces_CanExecute(object obj)
    {
        return !IsNetworkInterfaceLoading &&
               System.Windows.Application.Current.MainWindow != null &&
               !((MetroWindow)System.Windows.Application.Current.MainWindow)
                   .IsAnyDialogOpen;
    }

    private async void ReloadNetworkInterfacesAction()
    {
        await ReloadNetworkInterfacesAsync(CancellationTokenSource.Token);
    }

    public IAsyncCommand ExportCommand => new AsyncCommand(async () => await ExportAction(CancellationTokenSource.Token).WaitAsync(CancellationTokenSource.Token)
        , continueOnCapturedContext: false);

    private async Task ExportAction(CancellationToken cancellationToken)
    {
        var customDialog = new CustomDialog
        {
            Title = Strings.Export
        };

        var exportViewModel = new ExportViewModel(async instance =>
            {
                await _dialogCoordinator.HideMetroDialogAsync(this, customDialog).WaitAsync(cancellationToken);

                try
                {
                    ExportManager.Export(instance.FilePath, instance.FileType,
                        instance.ExportAll ? NetworkInterfaces : [SelectedNetworkInterface]);
                }
                catch (Exception ex)
                {
                    var settings = AppearanceManager.MetroDialog;
                    settings.AffirmativeButtonText = Strings.OK;

                    await _dialogCoordinator.ShowMessageAsync(this, Strings.Error,
                        Strings.AnErrorOccurredWhileExportingTheData + Environment.NewLine +
                        Environment.NewLine + ex.Message, MessageDialogStyle.Affirmative, settings).WaitAsync(cancellationToken);
                }

                SettingsManager.Current.NetworkInterface_ExportFileType = instance.FileType;
                SettingsManager.Current.NetworkInterface_ExportFilePath = instance.FilePath;
            }, async _ => { await _dialogCoordinator.HideMetroDialogAsync(this, customDialog).WaitAsync(cancellationToken); },
            [
                ExportFileType.Csv, ExportFileType.Xml, ExportFileType.Json
            ], true,
            SettingsManager.Current.NetworkInterface_ExportFileType,
            SettingsManager.Current.NetworkInterface_ExportFilePath);

        customDialog.Content = new ExportDialog
        {
            DataContext = exportViewModel
        };

        await _dialogCoordinator.ShowMetroDialogAsync(this, customDialog).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public IAsyncCommand ApplyConfigurationCommand =>
        new AsyncCommand(async () => await ApplyConfigurationAsync(CancellationTokenSource.Token).WaitAsync(CancellationTokenSource.Token)
                                    , ApplyConfiguration_CanExecute
                                    , continueOnCapturedContext: false);

    private bool ApplyConfiguration_CanExecute(object parameter)
    {
        return System.Windows.Application.Current.MainWindow != null &&
               !((MetroWindow)System.Windows.Application.Current.MainWindow)
                   .IsAnyDialogOpen;
    }

    public IAsyncCommand ApplyProfileConfigCommand => new AsyncCommand(() => ApplyConfigurationFromProfileAsync(CancellationTokenSource.Token)
    .WaitAsync(CancellationTokenSource.Token), continueOnCapturedContext: false);

    public ICommand AddProfileCommand => new AsyncCommand(() => ProfileDialogManager
            .ShowAddProfileDialog(this, this, _dialogCoordinator, null, null, ApplicationName.NetworkInterface)
            .WaitAsync(CancellationTokenSource.Token), continueOnCapturedContext: false);

    private bool ModifyProfile_CanExecute(object obj)
    {
        //AddProfileCommand.RaiseCanExecuteChanged();
        return SelectedProfile is { IsDynamic: false };
    }

    public ICommand EditProfileCommand => new AsyncCommand(() => ProfileDialogManager.ShowEditProfileDialog(this, _dialogCoordinator, SelectedProfile)
            .WaitAsync(CancellationTokenSource.Token), ModifyProfile_CanExecute, continueOnCapturedContext: false);

    public ICommand CopyAsProfileCommand => new AsyncCommand(() => ProfileDialogManager.ShowCopyAsProfileDialog(this, _dialogCoordinator, SelectedProfile)
                                                    , ModifyProfile_CanExecute, continueOnCapturedContext: false);

    public ICommand DeleteProfileCommand => new AsyncCommand(() => ProfileDialogManager
            .ShowDeleteProfileDialog(this, _dialogCoordinator, [SelectedProfile]), ModifyProfile_CanExecute, continueOnCapturedContext: false);

    public ICommand EditGroupCommand => new RelayCommand(EditGroupCommandAsync.Execute);
    public IAsyncCommand<string> EditGroupCommandAsync => new AsyncCommand<string>(EditGroupActionAsync);

    private Task EditGroupActionAsync(string group)
    {
        return ProfileDialogManager.ShowEditGroupDialog(this, _dialogCoordinator, ProfileManager.GetGroup(group.ToString()));
    }

    public ICommand ClearSearchCommand => new RelayCommand(_ => ClearSearchAction());

    private void ClearSearchAction()
    {
        Search = string.Empty;
    }

    #region Additional commands

    private bool AdditionalCommands_CanExecute(object parameter)
    {
        OpenNetworkConnectionsCommand.RaiseCanExecuteChanged();
        return System.Windows.Application.Current.MainWindow != null &&
               !((MetroWindow)System.Windows.Application.Current.MainWindow)
                   .IsAnyDialogOpen;
    }

    public IAsyncCommand OpenNetworkConnectionsCommand =>
        new AsyncCommand(() => OpenNetworkConnectionsAsync(CancellationTokenSource.Token), AdditionalCommands_CanExecute, continueOnCapturedContext: false);

    public ICommand IPScannerCommand => new RelayCommand(_ => IPScannerAction(), AdditionalCommands_CanExecute);

    private void IPScannerAction()
    {
        var ipTuple = SelectedNetworkInterface?.IPv4Address.FirstOrDefault();

        // ToDo: Log error in the future
        if (ipTuple == null)
            return;

        EventSystem.RedirectToApplication(ApplicationName.IPScanner,
            $"{ipTuple.Item1}/{Subnetmask.ConvertSubnetmaskToCidr(ipTuple.Item2)}");
    }

    public IAsyncCommand FlushDNSCommand => new AsyncCommand(() => FlushDNSAsync(CancellationTokenSource.Token), AdditionalCommands_CanExecute, continueOnCapturedContext: false);

    public IAsyncCommand ReleaseRenewCommand => new AsyncCommand(() => ReleaseRenewAsync(IPConfigReleaseRenewMode.ReleaseRenew, CancellationTokenSource.Token), AdditionalCommands_CanExecute, continueOnCapturedContext: false);

    public IAsyncCommand ReleaseCommand => new AsyncCommand(() => ReleaseRenewAsync(IPConfigReleaseRenewMode.Release, CancellationTokenSource.Token), AdditionalCommands_CanExecute, continueOnCapturedContext: false);

    public IAsyncCommand RenewCommand => new AsyncCommand(() => ReleaseRenewAsync(IPConfigReleaseRenewMode.Renew, CancellationTokenSource.Token), AdditionalCommands_CanExecute, continueOnCapturedContext: false);

    public IAsyncCommand ReleaseRenew6Command => new AsyncCommand(() => ReleaseRenewAsync(IPConfigReleaseRenewMode.ReleaseRenew6, CancellationTokenSource.Token), AdditionalCommands_CanExecute, continueOnCapturedContext: false);

    public IAsyncCommand Release6Command => new AsyncCommand(() => ReleaseRenewAsync(IPConfigReleaseRenewMode.Release6, CancellationTokenSource.Token), AdditionalCommands_CanExecute, continueOnCapturedContext: false);
    
    public IAsyncCommand Renew6Command => new AsyncCommand(() => ReleaseRenewAsync(IPConfigReleaseRenewMode.Renew, CancellationTokenSource.Token), AdditionalCommands_CanExecute, continueOnCapturedContext: false); 

    public IAsyncCommand AddIPv4AddressCommand => new AsyncCommand(() => AddIPv4AddressAction(CancellationTokenSource.Token),
        AdditionalCommands_CanExecute, continueOnCapturedContext: false);

    private async Task AddIPv4AddressAction(CancellationToken cancellationToken)
    {
        var customDialog = new CustomDialog
        {
            Title = Strings.AddIPv4Address
        };

        var ipAddressAndSubnetmaskViewModel = new IPAddressAndSubnetmaskViewModel(async instance =>
        {
            await _dialogCoordinator.HideMetroDialogAsync(this, customDialog).WaitAsync(cancellationToken).ConfigureAwait(false);

            await AddIPv4Address(instance.IPAddress, instance.Subnetmask, cancellationToken).ConfigureAwait(false);
        }, async _ => { await _dialogCoordinator.HideMetroDialogAsync(this, customDialog).WaitAsync(cancellationToken); });

        customDialog.Content = new IPAddressAndSubnetmaskDialog
        {
            DataContext = ipAddressAndSubnetmaskViewModel
        };

        await _dialogCoordinator.ShowMetroDialogAsync(this, customDialog).WaitAsync(cancellationToken);
    }

    public IAsyncCommand RemoveIPv4AddressCommand => new AsyncCommand(() => RemoveIPv4AddressAction(CancellationTokenSource.Token),
        AdditionalCommands_CanExecute, continueOnCapturedContext: false);

    private async Task RemoveIPv4AddressAction(CancellationToken cancellationToken)
    {
        var customDialog = new CustomDialog
        {
            Title = Strings.RemoveIPv4Address
        };

        var dropdownViewModel = new DropdownViewModel(async instance =>
            {
                await _dialogCoordinator.HideMetroDialogAsync(this, customDialog).WaitAsync(cancellationToken).ConfigureAwait(false);

                await RemoveIPv4Address(instance.SelectedValue.Split("/")[0], cancellationToken).ConfigureAwait(false);
            }, async _ => { await _dialogCoordinator.HideMetroDialogAsync(this, customDialog).WaitAsync(cancellationToken); },
            SelectedNetworkInterface.IPv4Address.Select(x => $"{x.Item1}/{Subnetmask.ConvertSubnetmaskToCidr(x.Item2)}")
                .ToList(), Strings.IPv4Address);

        customDialog.Content = new DropdownDialog
        {
            DataContext = dropdownViewModel
        };

        await _dialogCoordinator.ShowMetroDialogAsync(this, customDialog);
    }

    #endregion

    #endregion

    #region Methods

    private async Task ReloadNetworkInterfacesAsync(CancellationToken cancellationToken)
    {
        IsNetworkInterfaceLoading = true;

        // Make the user happy, let him see a reload animation (and he cannot spam the reload command)
        await Task.Delay(2000, cancellationToken);

        var id = string.Empty;

        if (SelectedNetworkInterface != null)
            id = SelectedNetworkInterface.Id;

        NetworkInterfaces = await NetworkInterface.GetNetworkInterfacesAsync(cancellationToken);

        // Change interface...
        SelectedNetworkInterface = string.IsNullOrEmpty(id)
            ? NetworkInterfaces.FirstOrDefault()
            : NetworkInterfaces.FirstOrDefault(x => x.Id == id);

        IsNetworkInterfaceLoading = false;
    }

    private void SetConfigurationDefaults(NetworkInterfaceInfo info)
    {
        if (info.DhcpEnabled)
        {
            ConfigEnableDynamicIPAddress = true;
        }
        else
        {
            ConfigEnableStaticIPAddress = true;
            ConfigIPAddress = info.IPv4Address.FirstOrDefault()?.Item1.ToString();
            ConfigSubnetmask = info.IPv4Address.FirstOrDefault()?.Item2.ToString();
            ConfigGateway = info.IPv4Gateway?.Any() == true
                ? info.IPv4Gateway.FirstOrDefault()?.ToString()
                : string.Empty;
        }

        if (info.DNSAutoconfigurationEnabled)
        {
            ConfigEnableDynamicDNS = true;
        }
        else
        {
            ConfigEnableStaticDNS = true;

            var dnsServers = info.DNSServer.Where(x => x.AddressFamily == AddressFamily.InterNetwork)
                .ToList();
            ConfigPrimaryDNSServer = dnsServers.Count > 0 ? dnsServers[0].ToString() : string.Empty;
            ConfigSecondaryDNSServer = dnsServers.Count > 1 ? dnsServers[1].ToString() : string.Empty;
        }
    }

    private async Task ApplyConfigurationAsync(CancellationToken cancellationToken)
    {
        IsConfigurationRunning = true;
        IsStatusMessageDisplayed = false;

        var subnetmask = ConfigSubnetmask;

        // CIDR to subnetmask
        if (ConfigEnableStaticIPAddress && subnetmask.StartsWith("/"))
            subnetmask = Subnetmask.GetFromCidr(int.Parse(subnetmask.TrimStart('/'))).Subnetmask;

        // If primary and secondary DNS are empty --> autoconfiguration
        if (ConfigEnableStaticDNS && string.IsNullOrEmpty(ConfigPrimaryDNSServer) &&
            string.IsNullOrEmpty(ConfigSecondaryDNSServer))
            ConfigEnableDynamicDNS = true;

        // When primary DNS is empty, swap it with secondary (if not empty)
        if (ConfigEnableStaticDNS && string.IsNullOrEmpty(ConfigPrimaryDNSServer) &&
            !string.IsNullOrEmpty(ConfigSecondaryDNSServer))
        {
            ConfigPrimaryDNSServer = ConfigSecondaryDNSServer;
            ConfigSecondaryDNSServer = string.Empty;
        }

        var config = new NetworkInterfaceConfig
        {
            Name = SelectedNetworkInterface.Name,
            EnableStaticIPAddress = ConfigEnableStaticIPAddress,
            IPAddress = ConfigIPAddress,
            Subnetmask = subnetmask,
            Gateway = ConfigGateway,
            EnableStaticDNS = ConfigEnableStaticDNS,
            PrimaryDNSServer = ConfigPrimaryDNSServer,
            SecondaryDNSServer = ConfigSecondaryDNSServer
        };

        try
        {
            var networkInterface = new NetworkInterface();

            networkInterface.UserHasCanceled += NetworkInterface_UserHasCanceled;

            await networkInterface.ConfigureNetworkInterfaceAsync(config, cancellationToken).ConfigureAwait(false);

            await ReloadNetworkInterfacesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            IsStatusMessageDisplayed = true;
        }
        finally
        {
            IsConfigurationRunning = false;
        }
    }

    private async Task ApplyConfigurationFromProfileAsync(CancellationToken cancellationToken)
    {
        IsConfigurationRunning = true;
        IsStatusMessageDisplayed = false;

        var subnetmask = SelectedProfile.NetworkInterface_Subnetmask;

        // CIDR to subnetmask
        if (SelectedProfile.NetworkInterface_EnableStaticIPAddress && subnetmask.StartsWith("/"))
            subnetmask = Subnetmask.GetFromCidr(int.Parse(subnetmask.TrimStart('/'))).Subnetmask;

        var enableStaticDNS = SelectedProfile.NetworkInterface_EnableStaticDNS;

        var primaryDNSServer = SelectedProfile.NetworkInterface_PrimaryDNSServer;
        var secondaryDNSServer = SelectedProfile.NetworkInterface_SecondaryDNSServer;

        // If primary and secondary DNS are empty --> autoconfiguration
        if (enableStaticDNS && string.IsNullOrEmpty(primaryDNSServer) && string.IsNullOrEmpty(secondaryDNSServer))
            enableStaticDNS = false;

        // When primary DNS is empty, swap it with secondary (if not empty)
        if (SelectedProfile.NetworkInterface_EnableStaticDNS && string.IsNullOrEmpty(primaryDNSServer) &&
            !string.IsNullOrEmpty(secondaryDNSServer))
        {
            primaryDNSServer = secondaryDNSServer;
            secondaryDNSServer = string.Empty;
        }

        var config = new NetworkInterfaceConfig
        {
            Name = SelectedNetworkInterface.Name,
            EnableStaticIPAddress = SelectedProfile.NetworkInterface_EnableStaticIPAddress,
            IPAddress = SelectedProfile.NetworkInterface_IPAddress,
            Subnetmask = subnetmask,
            Gateway = SelectedProfile.NetworkInterface_Gateway,
            EnableStaticDNS = enableStaticDNS,
            PrimaryDNSServer = primaryDNSServer,
            SecondaryDNSServer = secondaryDNSServer
        };

        try
        {
            var networkInterface = new NetworkInterface();

            networkInterface.UserHasCanceled += NetworkInterface_UserHasCanceled;

            await networkInterface.ConfigureNetworkInterfaceAsync(config, cancellationToken).ConfigureAwait(false);

            await ReloadNetworkInterfacesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            IsStatusMessageDisplayed = true;
        }
        finally
        {
            IsConfigurationRunning = false;
        }
    }

    private async Task OpenNetworkConnectionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            ProcessStartInfo info = new()
            {
                FileName = "NCPA.cpl",
                UseShellExecute = true
            };

            Process.Start(info);
        }
        catch (Exception ex)
        {
            await _dialogCoordinator.ShowMessageAsync(this, Strings.Error, ex.Message,
                MessageDialogStyle.Affirmative, AppearanceManager.MetroDialog).WaitAsync(cancellationToken);
        }
    }

    private async Task FlushDNSAsync(CancellationToken cancellationToken)
    {
        IsConfigurationRunning = true;
        IsStatusMessageDisplayed = false;

        await NetworkInterface.FlushDnsAsync(cancellationToken);

        IsConfigurationRunning = false;
    }

    private async Task ReleaseRenewAsync(IPConfigReleaseRenewMode releaseRenewMode, CancellationToken cancellationToken)
    {
        IsConfigurationRunning = true;

        await NetworkInterface.ReleaseRenewAsync(releaseRenewMode, SelectedNetworkInterface.Name);

        await ReloadNetworkInterfacesAsync(cancellationToken);

        IsConfigurationRunning = false;
    }

    private async Task AddIPv4Address(string ipAddress, string subnetmaskOrCidr, CancellationToken cancellationToken)
    {
        IsConfigurationRunning = true;
        IsStatusMessageDisplayed = false;

        var subnetmask = subnetmaskOrCidr;

        // CIDR to subnetmask
        if (subnetmask.StartsWith("/"))
            subnetmask = Subnetmask.GetFromCidr(int.Parse(subnetmask.TrimStart('/'))).Subnetmask;

        var config = new NetworkInterfaceConfig
        {
            Name = SelectedNetworkInterface.Name,
            EnableDhcpStaticIpCoexistence = SelectedNetworkInterface.DhcpEnabled,
            IPAddress = ipAddress,
            Subnetmask = subnetmask
        };

        try
        {
            await NetworkInterface.AddIPAddressToNetworkInterfaceAsync(config, cancellationToken).ConfigureAwait(false);
            await ReloadNetworkInterfacesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            IsStatusMessageDisplayed = true;
        }
        finally
        {
            IsConfigurationRunning = false;
        }
    }

    private async Task RemoveIPv4Address(string ipAddress, CancellationToken cancellationToken)
    {
        IsConfigurationRunning = true;
        IsStatusMessageDisplayed = false;

        var config = new NetworkInterfaceConfig
        {
            Name = SelectedNetworkInterface.Name,
            IPAddress = ipAddress
        };

        try
        {
            await NetworkInterface.RemoveIPAddressFromNetworkInterfaceAsync(config, cancellationToken).ConfigureAwait(false);
            await ReloadNetworkInterfacesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            IsStatusMessageDisplayed = true;
        }
        finally
        {
            IsConfigurationRunning = false;
        }
    }

    private void ResizeProfile(bool dueToChangedSize)
    {
        _canProfileWidthChange = false;

        if (dueToChangedSize)
        {
            ExpandProfileView = Math.Abs(ProfileWidth.Value - GlobalStaticConfiguration.Profile_WidthCollapsed) >
                                GlobalStaticConfiguration.Profile_FloatPointFix;
        }
        else
        {
            if (ExpandProfileView)
            {
                ProfileWidth =
                    Math.Abs(_tempProfileWidth - GlobalStaticConfiguration.Profile_WidthCollapsed) <
                    GlobalStaticConfiguration.Profile_FloatPointFix
                        ? new GridLength(GlobalStaticConfiguration.Profile_DefaultWidthExpanded)
                        : new GridLength(_tempProfileWidth);
            }
            else
            {
                _tempProfileWidth = ProfileWidth.Value;
                ProfileWidth = new GridLength(GlobalStaticConfiguration.Profile_WidthCollapsed);
            }
        }

        _canProfileWidthChange = true;
    }

    private void ResetBandwidthChart()
    {
        if (Series == null)
            return;

        Series[0].Values = new ObservableCollection<LvlChartsDefaultInfo>();
        Series[1].Values = new ObservableCollection<LvlChartsDefaultInfo>();

        var currentDateTime = DateTime.Now;

        for (var i = 60; i > 0; i--)
        {
            var bandwidthInfo = new LvlChartsDefaultInfo(currentDateTime.AddSeconds(-i), double.NaN);

            ((ObservableCollection<LvlChartsDefaultInfo>)Series[0].Values).Add(bandwidthInfo);
            ((ObservableCollection<LvlChartsDefaultInfo>)Series[1].Values).Add(bandwidthInfo);
        }
    }

    private bool _resetBandwidthStatisticOnNextUpdate;

    private void StartBandwidthMeter(string networkInterfaceId)
    {
        // Reset chart
        ResetBandwidthChart();

        // Reset statistic
        _resetBandwidthStatisticOnNextUpdate = true;

        _bandwidthMeter = new BandwidthMeter(networkInterfaceId);
        _bandwidthMeter.UpdateSpeed += BandwidthMeter_UpdateSpeed;
        _bandwidthMeter.Start();
    }

    private void ResumeBandwidthMeter()
    {
        if (_bandwidthMeter is not { IsRunning: false })
            return;

        ResetBandwidthChart();

        _resetBandwidthStatisticOnNextUpdate = true;

        _bandwidthMeter.Start();
    }

    private void StopBandwidthMeter()
    {
        if (_bandwidthMeter is not { IsRunning: true })
            return;

        _bandwidthMeter.Stop();
    }

    public void OnViewVisible()
    {
        _isViewActive = true;

        RefreshProfiles();

        ResumeBandwidthMeter();
    }

    public void OnViewHide()
    {
        StopBandwidthMeter();

        _isViewActive = false;
    }

    private void SetProfilesView(ProfileInfo profile = null)
    {
        Profiles = new CollectionViewSource
        {
            Source = ProfileManager.Groups.SelectMany(x => x.Profiles).Where(x => x.NetworkInterface_Enabled)
                .OrderBy(x => x.Group).ThenBy(x => x.Name)
        }.View;

        Profiles.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ProfileInfo.Group)));

        Profiles.Filter = o =>
        {
            if (o is not ProfileInfo info)
                return false;

            if (string.IsNullOrEmpty(Search))
                return true;

            var search = Search.Trim();

            // Search by: Tag=xxx (exact match, ignore case)
            /*
            if (search.StartsWith(ProfileManager.TagIdentifier, StringComparison.OrdinalIgnoreCase))
                return !string.IsNullOrEmpty(info.Tags) && info.PingMonitor_Enabled && info.Tags.Replace(" ", "").Split(';').Any(str => search.Substring(ProfileManager.TagIdentifier.Length, search.Length - ProfileManager.TagIdentifier.Length).Equals(str, StringComparison.OrdinalIgnoreCase));
            */

            // Search by: Name
            return info.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) > -1;
        };

        // Set specific profile or first if null
        SelectedProfile = null;

        if (profile != null)
            SelectedProfile = Profiles.Cast<ProfileInfo>().FirstOrDefault(x => x.Equals(profile)) ??
                              Profiles.Cast<ProfileInfo>().FirstOrDefault();
        else
            SelectedProfile = Profiles.Cast<ProfileInfo>().FirstOrDefault();
    }

    private void RefreshProfiles()
    {
        if (!_isViewActive)
            return;

        SetProfilesView(SelectedProfile);
    }

    #endregion

    #region Events

    private void ProfileManager_OnProfilesUpdated(object sender, EventArgs e)
    {
        RefreshProfiles();
    }

    private void SearchDispatcherTimer_Tick(object sender, EventArgs e)
    {
        _searchDispatcherTimer.Stop();

        RefreshProfiles();

        IsSearching = false;
    }

    private void BandwidthMeter_UpdateSpeed(object sender, BandwidthMeterSpeedArgs e)
    {
        // Reset statistics
        if (_resetBandwidthStatisticOnNextUpdate)
        {
            BandwidthStartTime = DateTime.Now;
            _bandwidthTotalBytesReceivedTemp = e.TotalBytesReceived;
            _bandwidthTotalBytesSentTemp = e.TotalBytesSent;

            _resetBandwidthStatisticOnNextUpdate = false;
        }

        // Measured time
        BandwidthMeasuredTime = DateTime.Now - BandwidthStartTime;

        // Current download/upload
        BandwidthTotalBytesReceived = e.TotalBytesReceived;
        BandwidthTotalBytesSent = e.TotalBytesSent;
        BandwidthBytesReceivedSpeed = e.ByteReceivedSpeed;
        BandwidthBytesSentSpeed = e.ByteSentSpeed;

        // Total download/upload
        BandwidthDiffBytesReceived = BandwidthTotalBytesReceived - _bandwidthTotalBytesReceivedTemp;
        BandwidthDiffBytesSent = BandwidthTotalBytesSent - _bandwidthTotalBytesSentTemp;

        var upValues = Series[0].Values as ObservableCollection<LvlChartsDefaultInfo>;
        var downValues = Series[1].Values as ObservableCollection<LvlChartsDefaultInfo>;

        // Add chart entry
        upValues.Add(new LvlChartsDefaultInfo(e.DateTime, e.ByteReceivedSpeed));
        downValues.Add(new LvlChartsDefaultInfo(e.DateTime, e.ByteSentSpeed));

        // Remove data older than 60 seconds
        if (upValues.Count > 59)
            upValues.RemoveAt(0);

        if (downValues.Count > 59)
            downValues.RemoveAt(0);
    }

    private void NetworkInterface_UserHasCanceled(object sender, EventArgs e)
    {
        StatusMessage = Strings.CanceledByUserMessage;
        IsStatusMessageDisplayed = true;
        CancellationTokenSource.Cancel();
    }

    #endregion
}