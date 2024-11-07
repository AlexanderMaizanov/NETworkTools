using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Threading;
using Windows.Devices.WiFi;
using Windows.Foundation.Metadata;
using Windows.Security.Credentials;
using Windows.System;
using log4net;
using MahApps.Metro.Controls.Dialogs;
using NETworkManager.Localization;
using NETworkManager.Localization.Resources;
using NETworkManager.Models.Export;
using NETworkManager.Models.Lookup;
using NETworkManager.Models.Network;
using NETworkManager.Settings;
using NETworkManager.Utilities;
using NETworkManager.Views;
using System.Threading;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using AsyncAwaitBestPractices.MVVM;
using System.IO;

namespace NETworkManager.ViewModels;

public partial class WiFiViewModel : ViewModelBase
{
    #region Variables

    private readonly IDialogCoordinator _dialogCoordinator;

    private static readonly ILog Log = LogManager.GetLogger(typeof(WiFiViewModel));

    private bool _isLoading;
    private readonly DispatcherTimer _autoRefreshTimer = new();
    private readonly DispatcherTimer _hideConnectionStatusMessageTimer = new();

    private bool _sdkContractAvailable;

    public bool SdkContractAvailable
    {
        get => _sdkContractAvailable;
        set
        {
            if (value == _sdkContractAvailable)
                return;

            _sdkContractAvailable = value;
            OnPropertyChanged();
        }
    }

    private bool _wiFiAdapterAccessEnabled;

    public bool WiFiAdapterAccessEnabled
    {
        get => _wiFiAdapterAccessEnabled;
        set
        {
            if (value == _wiFiAdapterAccessEnabled)
                return;

            _wiFiAdapterAccessEnabled = value;
            OnPropertyChanged();
        }
    }

    private bool _isAdaptersLoading;

    public bool IsAdaptersLoading
    {
        get => _isAdaptersLoading;
        set
        {
            if (value == _isAdaptersLoading)
                return;

            _isAdaptersLoading = value;
            OnPropertyChanged();
        }
    }

    private List<WiFiAdapterInfo> _adapters = [];

    public List<WiFiAdapterInfo> Adapters
    {
        get => _adapters;
        private set
        {
            if (value == _adapters)
                return;

            _adapters = value;
            OnPropertyChanged();
        }
    }

    private WiFiAdapterInfo _selectedAdapters;

    public WiFiAdapterInfo SelectedAdapter
    {
        get => _selectedAdapters;
        set
        {
            if (value == _selectedAdapters)
                return;

            if (value != null)
            {
                if (!_isLoading)
                    SettingsManager.Current.WiFi_InterfaceId = value.NetworkInterfaceInfo.Id;

                ScanAsync(value, CancellationTokenSource.Token).ConfigureAwait(false);
            }

            _selectedAdapters = value;
            OnPropertyChanged();
        }
    }

    private bool _isNetworksLoading;

    public bool IsNetworksLoading
    {
        get => _isNetworksLoading;
        set
        {
            if (value == _isNetworksLoading)
                return;

            _isNetworksLoading = value;
            OnPropertyChanged();
        }
    }

    private bool _autoRefreshEnabled;

    public bool AutoRefreshEnabled
    {
        get => _autoRefreshEnabled;
        set
        {
            if (value == _autoRefreshEnabled)
                return;

            if (!_isLoading)
                SettingsManager.Current.WiFi_AutoRefreshEnabled = value;

            _autoRefreshEnabled = value;

            // Start timer to refresh automatically
            if (value)
            {
                StartAutoRefreshTimer();
            }
            else
            {
                StopAutoRefreshTimer();
            }

            OnPropertyChanged();
        }
    }

    public ICollectionView AutoRefreshTimes { get; }

    private AutoRefreshTimeInfo _selectedAutoRefreshTime;

    public AutoRefreshTimeInfo SelectedAutoRefreshTime
    {
        get => _selectedAutoRefreshTime;
        set
        {
            if (value == _selectedAutoRefreshTime)
                return;

            if (!_isLoading)
                SettingsManager.Current.WiFi_AutoRefreshTime = value;

            _selectedAutoRefreshTime = value;

            if (AutoRefreshEnabled)
            {
                _autoRefreshTimer.Interval = AutoRefreshTime.CalculateTimeSpan(value);
                _autoRefreshTimer.Start();
            }

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

            NetworksView.Refresh();

            OnPropertyChanged();
        }
    }

    private bool _show2dot4GHzNetworks;

    public bool Show2dot4GHzNetworks
    {
        get => _show2dot4GHzNetworks;
        set
        {
            if (value == _show2dot4GHzNetworks)
                return;

            if (!_isLoading)
                SettingsManager.Current.WiFi_Show2dot4GHzNetworks = value;

            _show2dot4GHzNetworks = value;

            NetworksView.Refresh();

            OnPropertyChanged();
        }
    }

    private bool _show5GHzNetworks;

    public bool Show5GHzNetworks
    {
        get => _show5GHzNetworks;
        set
        {
            if (value == _show5GHzNetworks)
                return;

            if (!_isLoading)
                SettingsManager.Current.WiFi_Show5GHzNetworks = value;

            _show5GHzNetworks = value;

            NetworksView.Refresh();

            OnPropertyChanged();
        }
    }

    private ObservableCollection<WiFiNetworkInfo> _networks = [];

    public ObservableCollection<WiFiNetworkInfo> Networks
    {
        get => _networks;
        set
        {
            if (value != null && value == _networks)
                return;

            _networks = value;
            OnPropertyChanged();
        }
    }

    public ICollectionView NetworksView { get; }

    private WiFiNetworkInfo _selectedNetwork;

    public WiFiNetworkInfo SelectedNetwork
    {
        get => _selectedNetwork;
        set
        {
            if (value == _selectedNetwork)
                return;

            _selectedNetwork = value;
            OnPropertyChanged();
        }
    }

    private IList _selectedNetworks = new ArrayList();

    public IList SelectedNetworks
    {
        get => _selectedNetworks;
        set
        {
            if (Equals(value, _selectedNetworks))
                return;

            _selectedNetworks = value;
            OnPropertyChanged();
        }
    }

    public Axis[] Radion1XAxes { get; set; } = [];

    public Axis[] Radion1YAxes { get; set; } = [];

    public ObservableCollection<ISeries> Radio1Series { get; set; } =
    [
        new LineSeries<double>
        {
            Values = [],
            Fill = null
        }
    ];

    

    public string[] Radio1Labels { get; set; } =
        { " ", " ", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", " ", " " };

    public ObservableCollection<ISeries> Radio2Series { get; set; } =
    [
        new LineSeries<double>
        {
            Values = [],
            Fill = null
        }
    ];

    public Axis[] Radion2XAxes { get; set; } = [];

    public Axis[] Radion2YAxes { get; set; } = [];

    public string[] Radio2Labels { get; set; } =
    {
        " ", " ", "36", "40", "44", "48", "52", "56", "60", "64", "", "", "", "", "100", "104", "108", "112", "116",
        "120", "124", "128", "132", "136", "140", "144", "149", "153", "157", "161", "165", " ", " "
    };

    public Func<double, string> FormattedDbm { get; set; } =
        value => $"- {100 - value} dBm"; // Reverse y-axis 0 to -100

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

    private bool _isBackgroundSearchRunning;

    public bool IsBackgroundSearchRunning
    {
        get => _isBackgroundSearchRunning;
        set
        {
            if (value == _isBackgroundSearchRunning)
                return;

            _isBackgroundSearchRunning = value;
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

    private bool _isConnecting;

    public bool IsConnecting
    {
        get => _isConnecting;
        set
        {
            if (value == _isConnecting)
                return;

            _isConnecting = value;
            OnPropertyChanged();
        }
    }

    private bool _isConnectionStatusMessageDisplayed;

    public bool IsConnectionStatusMessageDisplayed
    {
        get => _isConnectionStatusMessageDisplayed;
        set
        {
            if (value == _isConnectionStatusMessageDisplayed)
                return;

            _isConnectionStatusMessageDisplayed = value;
            OnPropertyChanged();
        }
    }

    private string _connectionStatusMessage;
    private bool _initAdapter;

    public string ConnectionStatusMessage
    {
        get => _connectionStatusMessage;
        private set
        {
            if (value == _connectionStatusMessage)
                return;

            _connectionStatusMessage = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region Constructor, load settings

    public WiFiViewModel(IDialogCoordinator instance)
    {
        _isLoading = true;

        _dialogCoordinator = instance;

        Radion1XAxes = [
            new Axis
                {
                    Name = Strings.Channel,
                    MaxLimit = 16,
                    MinLimit = 0,
                    NamePaint = new SolidColorPaint(SKColors.Black),

                    LabelsPaint = new SolidColorPaint(SKColors.Blue),
                    TextSize = 10,

                    Labels = Radio1Labels,

                    SeparatorsPaint = new SolidColorPaint() { StrokeThickness = 0 }
                }
            ];
        Radion1YAxes =
            [
                new Axis
                {
                    Name = Strings.SignalStrength,
                    MaxLimit = 100,
                    MinLimit = 0,
                    NamePaint = new SolidColorPaint(SKColors.Red),

                    LabelsPaint = new SolidColorPaint(SKColors.Green),
                    TextSize = 20,
                    Labeler = FormattedDbm,

                    SeparatorsPaint = new SolidColorPaint(SKColors.DimGray)
                    {
                        StrokeThickness = 10,
                        PathEffect = new DashEffect([10.0f, 0.0f])
                    }
                }
            ];

        // Check if Microsoft.Windows.SDK.Contracts is available 
        SdkContractAvailable = ApiInformation.IsTypePresent("Windows.Devices.WiFi.WiFiAdapter");

        if (!SdkContractAvailable)
        {
            _isLoading = false;

            return;
        }

        _initAdapter = true;
        _autoRefreshTimer.Interval = new TimeSpan(0, 0, 3);
        NetworksView = CollectionViewSource.GetDefaultView(Networks);
        // Auto refresh
        _autoRefreshTimer.Tick += AutoRefreshTimer_Tick;

        AutoRefreshTimes = CollectionViewSource.GetDefaultView(AutoRefreshTime.GetDefaults);
        SelectedAutoRefreshTime = AutoRefreshTimes.Cast<AutoRefreshTimeInfo>().FirstOrDefault(x =>
            x.Value == SettingsManager.Current.WiFi_AutoRefreshTime.Value &&
            x.TimeUnit == SettingsManager.Current.WiFi_AutoRefreshTime.TimeUnit);
        AutoRefreshEnabled = SettingsManager.Current.WiFi_AutoRefreshEnabled;

        // Hide ConnectionStatusMessage automatically
        _hideConnectionStatusMessageTimer.Interval = new TimeSpan(0, 0, 15);
        _hideConnectionStatusMessageTimer.Tick += HideConnectionStatusMessageTimer_Tick;

        // Load settings
        LoadSettings();
        _autoRefreshTimer.Start();

        _isLoading = false;
    }

    private async Task InitializeAdapterAsync(CancellationToken cancellationToken)
    {
        
        // Check if the access is denied and show a message
        WiFiAdapterAccessEnabled = await RequestAccessAsync(cancellationToken) == WiFiAccessStatus.Allowed;

        if (!WiFiAdapterAccessEnabled)
        {
            _isLoading = false;

            return;
        }

        // Result view + search
        
        NetworksView.SortDescriptions.Add(new SortDescription(
            $"{nameof(WiFiNetworkInfo.AvailableNetwork)}.{nameof(WiFiNetworkInfo.AvailableNetwork.Ssid)}",
            ListSortDirection.Ascending));
        NetworksView.Filter = o =>
        {
            if (o is not WiFiNetworkInfo info)
                return false;

            if (WiFi.Is2dot4GHzNetwork(info.AvailableNetwork.ChannelCenterFrequencyInKilohertz) &&
                !Show2dot4GHzNetworks)
                return false;

            if (WiFi.Is5GHzNetwork(info.AvailableNetwork.ChannelCenterFrequencyInKilohertz) && !Show5GHzNetworks)
                return false;

            if (string.IsNullOrEmpty(Search))
                return true;

            // Search by: SSID, Security, Channel, BSSID (MAC address), Vendor, Phy kind
            return info.AvailableNetwork.Ssid.IndexOf(Search, StringComparison.OrdinalIgnoreCase) > -1 ||
                   WiFi.GetHumanReadableNetworkAuthenticationType(info.AvailableNetwork.SecuritySettings
                       .NetworkAuthenticationType).IndexOf(Search, StringComparison.OrdinalIgnoreCase) > -1 ||
                   $"{WiFi.GetChannelFromChannelFrequency(info.AvailableNetwork.ChannelCenterFrequencyInKilohertz)}"
                       .IndexOf(Search, StringComparison.OrdinalIgnoreCase) > -1 ||
                   info.AvailableNetwork.Bssid.IndexOf(Search, StringComparison.OrdinalIgnoreCase) > -1 ||
                   OUILookup.LookupByMacAddress(info.AvailableNetwork.Bssid).FirstOrDefault()?.Vendor
                       .IndexOf(Search, StringComparison.OrdinalIgnoreCase) > -1 ||
                   WiFi.GetHumanReadablePhyKind(info.AvailableNetwork.PhyKind)
                       .IndexOf(Search, StringComparison.OrdinalIgnoreCase) > -1;
        };

        // Load network adapters
        await LoadAdapters(SettingsManager.Current.WiFi_InterfaceId).ConfigureAwait(false);
        _initAdapter = false;
    }

    private void LoadSettings()
    {
        Show2dot4GHzNetworks = SettingsManager.Current.WiFi_Show2dot4GHzNetworks;
        Show5GHzNetworks = SettingsManager.Current.WiFi_Show5GHzNetworks;
    }

    private async Task ReloadAdapter(CancellationToken cancelationToken)
    {
        IsAdaptersLoading = true;

        await Task.Delay(2000, cancelationToken); // Make the user happy, let him see a reload animation (and he cannot spam the reload command)

        string id = string.Empty;

        if (SelectedAdapter != null)
            id = SelectedAdapter.NetworkInterfaceInfo.Id;

        try
        {
            Adapters = await WiFi.GetAdapterAsync(cancelationToken);

            if (Adapters.Count > 0)
                SelectedAdapter = string.IsNullOrEmpty(id) ? Adapters.FirstOrDefault() : Adapters.FirstOrDefault(x => x.NetworkInterfaceInfo.Id == id);
        }
        catch (FileNotFoundException) // This exception is thrown, when the Microsoft.Windows.SDK.Contracts is not available...
        {
            SdkContractAvailable = false;
        }

        IsAdaptersLoading = false;
    }

    private void ChangeAutoRefreshTimerInterval(TimeSpan timeSpan)
    {
        _autoRefreshTimer.Interval = timeSpan;
    }

    private void StartAutoRefreshTimer()
    {
        ChangeAutoRefreshTimerInterval(AutoRefreshTime.CalculateTimeSpan(SelectedAutoRefreshTime));

        _autoRefreshTimer.Start();
    }

    private void StopAutoRefreshTimer()
    {
        _autoRefreshTimer.Stop();
    }

    private void PauseAutoRefreshTimer()
    {
        if (!_autoRefreshTimer.IsEnabled)
            return;

        StopAutoRefreshTimer();
    }

    private void ResumeAutoRefreshTimer()
    {
        if (!_autoRefreshTimer.IsEnabled)
            return;

        StartAutoRefreshTimer();
    }

    public void OnViewVisible()
    {
        ResumeAutoRefreshTimer();
    }

    public void OnViewHide()
    {
        PauseAutoRefreshTimer();
    }

    #endregion

    #region ICommands & Actions

    public IAsyncCommand ReloadAdaptersCommand => new AsyncCommand(() => LoadAdapters(SelectedAdapter?.NetworkInterfaceInfo.Id));

    private void ReloadAdapterAction()
    {
        LoadAdapters(SelectedAdapter?.NetworkInterfaceInfo.Id).ConfigureAwait(false);
    }

    public IAsyncCommand ScanNetworksCommand =>
        new AsyncCommand(() => ScanAsync(SelectedAdapter, CancellationTokenSource.Token, true), ScanNetworks_CanExecute, continueOnCapturedContext: false);

    private bool ScanNetworks_CanExecute(object obj)
    {
        return !IsAdaptersLoading && !IsNetworksLoading && !IsBackgroundSearchRunning && !IsConnecting;
    }

    private async Task ScanNetworksAction()
    {
        await ScanAsync(SelectedAdapter, CancellationTokenSource.Token, true);
    }

    public IAsyncCommand ConnectCommand => new AsyncCommand(() => ConnectAsync(CancellationTokenSource.Token), continueOnCapturedContext: false);

    private void ConnectAction()
    {
        //Connect();
    }

    public IAsyncCommand DisconnectCommand => new AsyncCommand(() => DisconnectAsync().WaitAsync(CancellationTokenSource.Token), continueOnCapturedContext: false);

    private async Task DisconnectAction()
    {
        await DisconnectAsync();
    }

    public IAsyncCommand ExportCommand => new AsyncCommand(() => Export().WaitAsync(CancellationTokenSource.Token), continueOnCapturedContext: false);

    private void ExportAction()
    {
        Export().ConfigureAwait(false);
    }

    public IAsyncCommand OpenSettingsCommand => new AsyncCommand(() => OpenSettingsActionAsync(CancellationTokenSource.Token), continueOnCapturedContext: false);

    private Task OpenSettingsActionAsync(CancellationToken cancellationToken)
    {
        return Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-location")).AsTask(cancellationToken);
    }

    #endregion

    #region Methods

    /// <summary>
    ///     Request access to the WiFi adapter.
    /// </summary>
    /// <returns>Fails if the access is denied.</returns>
    private bool RequestAccess()
    {
        return WiFiAdapter.RequestAccessAsync().GetAwaiter().GetResult() == WiFiAccessStatus.Allowed;
    }

    private Task<WiFiAccessStatus> RequestAccessAsync(CancellationToken cancellationToken)
    {
        return WiFiAdapter.RequestAccessAsync().AsTask(cancellationToken);
    }

    private async Task LoadAdapters(string adapterId = null)
    {
        IsAdaptersLoading = true;

        // Show a loading animation for the user
        await Task.Delay(2500);

        try
        {
            Adapters = await WiFi.GetAdapterAsync(CancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            Log.Error("Error trying to get WiFi adapters.", ex);

            Adapters.Clear();
        }

        // Check if we found any adapters
        if (Adapters.Count > 0)
        {
            // Check for existing adapter id or select the first one
            if (string.IsNullOrEmpty(adapterId))
                SelectedAdapter = Adapters.FirstOrDefault();
            else
                SelectedAdapter = Adapters.FirstOrDefault(s => s.NetworkInterfaceInfo.Id == adapterId) ??
                                  Adapters.FirstOrDefault();
        }

        IsAdaptersLoading = false;
    }

    private async Task ScanAsync(WiFiAdapterInfo adapterInfo, CancellationToken cancellationToken, bool refreshing = false, uint delayInMs = 0)
    {
        if (refreshing)
        {
            StatusMessage = Strings.SearchingForNetworksDots;
            IsBackgroundSearchRunning = true;
        }
        else
        {
            IsStatusMessageDisplayed = false;
            IsNetworksLoading = true;
        }

        if (delayInMs != 0)
            await Task.Delay((int)delayInMs);

        var statusMessage = string.Empty;

        try
        {
            var wiFiNetworkScanInfo = await WiFi.GetNetworksAsync(adapterInfo.WiFiAdapter, cancellationToken);

            // Clear the values after the scan to make the UI smoother
            ClearCollections();

            foreach (var network in wiFiNetworkScanInfo.WiFiNetworkInfos)
            {
                Networks.Add(network);

                if (WiFi.ConvertChannelFrequencyToGigahertz(network.AvailableNetwork
                        .ChannelCenterFrequencyInKilohertz) < 5) // 2.4 GHz
                    Radio1Series = [GetSeriesCollection(network, WiFiRadio.One)];
                else
                    Radio2Series = [GetSeriesCollection(network, WiFiRadio.Two)];
            }

            statusMessage = string.Format(Strings.LastScanAtX,
                wiFiNetworkScanInfo.Timestamp.ToLongTimeString());
        }
        catch (Exception ex)
        {
            // Clear the existing old values if an error occurs
            ClearCollections();

            statusMessage = string.Format(Strings.ErrorWhileScanningWiFiAdapterXXXWithErrorXXX,
                adapterInfo.NetworkInterfaceInfo.Name, ex.Message);
        }
        finally
        {
            IsStatusMessageDisplayed = true;
            StatusMessage = statusMessage;

            IsBackgroundSearchRunning = false;
            IsNetworksLoading = false;
        }
    }

    private void ClearCollections()
    {
        Networks.Clear();
        Radio1Series.Clear();
        Radio2Series.Clear();
    }

    private ObservableCollection<double> GetDefaultChartValues(WiFiRadio radio)
    {
        ObservableCollection<double> values = [];

        for (var i = 0; i < (radio == WiFiRadio.One ? Radio1Labels.Length : Radio2Labels.Length); i++)
            values.Add(-1);

        return values;
    }

    private ObservableCollection<double> GetChartValues(WiFiNetworkInfo network, WiFiRadio radio, int index)
    {
        var values = GetDefaultChartValues(radio);

        var reverseMilliwatts = 100 - network.AvailableNetwork.NetworkRssiInDecibelMilliwatts * -1;

        values[index - 2] = -1;
        values[index - 1] = reverseMilliwatts;
        values[index] = reverseMilliwatts;
        values[index + 1] = reverseMilliwatts;
        values[index + 2] = -1;

        return values;
    }

    private LineSeries<double> GetSeriesCollection(WiFiNetworkInfo network, WiFiRadio radio)
    {
        var index = Array.IndexOf(radio == WiFiRadio.One ? Radio1Labels : Radio2Labels,
            $"{WiFi.GetChannelFromChannelFrequency(network.AvailableNetwork.ChannelCenterFrequencyInKilohertz)}");

        return new LineSeries<double>
        {
            Values = GetChartValues(network, radio, index),
            LineSmoothness = 0 
            
        };
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        var selectedAdapter = SelectedAdapter;
        var selectedNetwork = SelectedNetwork;

        var connectMode = WiFi.GetConnectMode(selectedNetwork.AvailableNetwork);

        var customDialog = new CustomDialog
        {
            Title = selectedNetwork.IsHidden
                ? Strings.HiddenNetwork
                : string.Format(Strings.ConnectToXXX, selectedNetwork.AvailableNetwork.Ssid)
        };

        var exportViewModel = new WiFiConnectViewModel(async instance =>
            {
                // Connect Open/PSK/EAP
                await _dialogCoordinator.HideMetroDialogAsync(this, customDialog).WaitAsync(cancellationToken);

                var ssid = selectedNetwork.IsHidden ? instance.Ssid : selectedNetwork.AvailableNetwork.Ssid;

                // Show status message
                IsConnecting = true;
                ConnectionStatusMessage = string.Format(Strings.ConnectingToXXX, ssid);
                IsConnectionStatusMessageDisplayed = true;

                // Connect to the network
                var reconnectionKind = instance.ConnectAutomatically
                    ? WiFiReconnectionKind.Automatic
                    : WiFiReconnectionKind.Manual;

                PasswordCredential credential = new();

                switch (instance.ConnectMode)
                {
                    case WiFiConnectMode.Psk:
                        credential.Password = SecureStringHelper.ConvertToString(instance.PreSharedKey);
                        break;
                    case WiFiConnectMode.Eap:
                        credential.UserName = instance.Username;

                        if (!string.IsNullOrEmpty(instance.Domain))
                            credential.Resource = instance.Domain;

                        credential.Password = SecureStringHelper.ConvertToString(instance.Password);
                        break;
                }

                WiFiConnectionStatus connectionResult;

                if (selectedNetwork.IsHidden)
                    connectionResult = await WiFi.ConnectAsync(instance.Options.AdapterInfo.WiFiAdapter,
                        instance.Options.NetworkInfo.AvailableNetwork, reconnectionKind, credential, cancellationToken, instance.Ssid);
                else
                    connectionResult = await WiFi.ConnectAsync(instance.Options.AdapterInfo.WiFiAdapter,
                        instance.Options.NetworkInfo.AvailableNetwork, reconnectionKind, credential, cancellationToken);

                // Done connecting
                IsConnecting = false;

                // Get result
                ConnectionStatusMessage = connectionResult == WiFiConnectionStatus.Success
                    ? string.Format(Strings.SuccessfullyConnectedToXXX, ssid)
                    : string.Format(Strings.CouldNotConnectToXXXReasonXXX, ssid,
                        ResourceTranslator.Translate(ResourceIdentifier.WiFiConnectionStatus, connectionResult));

                // Hide message automatically
                _hideConnectionStatusMessageTimer.Start();

                // Update the wifi networks.
                // Wait because an error may occur if a refresh is done directly after connecting.            
                await ScanAsync(SelectedAdapter, CancellationToken.None, true, 5000);
            }, async instance =>
            {
                // Connect WPS
                await _dialogCoordinator.HideMetroDialogAsync(this, customDialog);

                var ssid = selectedNetwork.IsHidden ? instance.Ssid : selectedNetwork.AvailableNetwork.Ssid;

                // Show status message
                IsConnecting = true;
                ConnectionStatusMessage = string.Format(Strings.ConnectingToXXX, ssid);
                IsConnectionStatusMessageDisplayed = true;

                // Connect to the network
                var reconnectionKind = instance.ConnectAutomatically
                    ? WiFiReconnectionKind.Automatic
                    : WiFiReconnectionKind.Manual;

                var connectionResult = await WiFi.ConnectWpsAsync(instance.Options.AdapterInfo.WiFiAdapter,
                    instance.Options.NetworkInfo.AvailableNetwork,
                    reconnectionKind, CancellationToken.None);

                // Done connecting
                IsConnecting = false;

                // Get result
                ConnectionStatusMessage = connectionResult == WiFiConnectionStatus.Success
                    ? string.Format(Strings.SuccessfullyConnectedToXXX, ssid)
                    : string.Format(Strings.CouldNotConnectToXXXReasonXXX, ssid,
                        ResourceTranslator.Translate(ResourceIdentifier.WiFiConnectionStatus, connectionResult));

                // Hide message automatically
                _hideConnectionStatusMessageTimer.Start();

                // Update the wifi networks.
                // Wait because an error may occur if a refresh is done directly after connecting.            
                await ScanAsync(SelectedAdapter, CancellationToken.None, true, 5000);
            },
            async _ => { await _dialogCoordinator.HideMetroDialogAsync(this, customDialog); },
            (selectedAdapter, selectedNetwork),
            connectMode);

        customDialog.Content = new WiFiConnectDialog
        {
            DataContext = exportViewModel
        };

        await _dialogCoordinator.ShowMetroDialogAsync(this, customDialog);
    }

    private Task DisconnectAsync()
    {
        var connectedNetwork = Networks.FirstOrDefault(x => x.IsConnected);

        WiFi.Disconnect(SelectedAdapter.WiFiAdapter);

        if (connectedNetwork != null)
        {
            ConnectionStatusMessage = string.Format(Strings.XXXDisconnected,
                connectedNetwork.AvailableNetwork.Ssid);
            IsConnectionStatusMessageDisplayed = true;

            // Hide message automatically
            _hideConnectionStatusMessageTimer.Start();
        }

        // Refresh
        return ScanAsync(SelectedAdapter, CancellationTokenSource.Token, true, 2500);
    }

    private async Task Export()
    {
        var customDialog = new CustomDialog
        {
            Title = Strings.Export
        };

        var exportViewModel = new ExportViewModel(async instance =>
            {
                await _dialogCoordinator.HideMetroDialogAsync(this, customDialog);

                try
                {
                    ExportManager.Export(instance.FilePath, instance.FileType,
                        instance.ExportAll
                            ? Networks
                            : new ObservableCollection<WiFiNetworkInfo>(SelectedNetworks.Cast<WiFiNetworkInfo>()
                                .ToArray()));
                }
                catch (Exception ex)
                {
                    var settings = AppearanceManager.MetroDialog;
                    settings.AffirmativeButtonText = Strings.OK;

                    await _dialogCoordinator.ShowMessageAsync(this, Strings.Error,
                        Strings.AnErrorOccurredWhileExportingTheData + Environment.NewLine +
                        Environment.NewLine + ex.Message, MessageDialogStyle.Affirmative, settings);
                }

                SettingsManager.Current.WiFi_ExportFileType = instance.FileType;
                SettingsManager.Current.WiFi_ExportFilePath = instance.FilePath;
            }, _ => { _dialogCoordinator.HideMetroDialogAsync(this, customDialog); },
            new[] { ExportFileType.Csv, ExportFileType.Xml, ExportFileType.Json }, true,
            SettingsManager.Current.WiFi_ExportFileType, SettingsManager.Current.WiFi_ExportFilePath);

        customDialog.Content = new ExportDialog
        {
            DataContext = exportViewModel
        };

        await _dialogCoordinator.ShowMetroDialogAsync(this, customDialog);
    }

    
    #endregion

    #region Events

    private async void AutoRefreshTimer_Tick(object sender, EventArgs e)
    {
        if (_initAdapter)
        {
            IsNetworksLoading = _isLoading = true;
            await InitializeAdapterAsync(CancellationTokenSource.Token);
            IsNetworksLoading = _isLoading = false;
        }
        
        // Don't refresh if it's already loading or connecting
        if (IsNetworksLoading || IsBackgroundSearchRunning || IsConnecting)
            return;

        // Stop timer...
        _autoRefreshTimer.Stop();

        // Scan networks
        if (SelectedAdapter is not null)
        {
            await ScanAsync(SelectedAdapter, CancellationTokenSource.Token, true);
        }
        

        // Restart timer...
        _autoRefreshTimer.Start();
    }

    private void HideConnectionStatusMessageTimer_Tick(object sender, EventArgs e)
    {
        _hideConnectionStatusMessageTimer.Stop();
        IsConnectionStatusMessageDisplayed = false;
    }

    #endregion
}