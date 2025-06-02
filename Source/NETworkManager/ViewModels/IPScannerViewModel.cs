using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;

using NETworkManager.Controls;
using NETworkManager.Localization;
using NETworkManager.Localization.Resources;
using NETworkManager.Models;
using NETworkManager.Models.EventSystem;
using NETworkManager.Models.Export;
using NETworkManager.Models.Network;
using NETworkManager.Profiles;
using NETworkManager.Settings;
using NETworkManager.Utilities;
using NETworkManager.Views;

using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;

namespace NETworkManager.ViewModels;

public class IPScannerViewModel : ViewModelCollectionBase<IPScannerHostInfo>, IProfileManagerMinimal
{
    #region Variables

    protected readonly IDialogCoordinator _dialogCoordinator;
    private readonly Guid _tabId;

    private int _hostsScanned;
    private int _hostsToScan;

    private bool _preparingScan;
    private bool _isSubnetDetectionRunning;

    

    public bool IsSubnetDetectionRunning
    {
        get => _isSubnetDetectionRunning;
        set => SetField(ref _isSubnetDetectionRunning, value);
    }

    public int HostsToScan
    {
        get => _hostsToScan;
        set => SetField(ref _hostsToScan, value);
    }

    public int HostsScanned
    {
        get => _hostsScanned;
        set => SetField(ref _hostsScanned, value);
    }

    public bool PreparingScan
    {
        get => _preparingScan;
        set => SetField(ref _preparingScan, value);
    }

    public static IEnumerable<CustomCommandInfo> CustomCommands => SettingsManager.Current.IPScanner_CustomCommands;
    
    #endregion

    #region Constructor, load settings, shutdown

    public IPScannerViewModel(IDialogCoordinator instance, Guid tabId, string hostOrIPRange)
    {
        _dialogCoordinator = instance;

        ConfigurationManager.Current.IPScannerTabCount++;

        _tabId = tabId;
        InputEntry = hostOrIPRange;

        // Host history
        HostHistoryView = CollectionViewSource.GetDefaultView(SettingsManager.Current.IPScanner_HostHistory);

        // Result view
        Results = [];
        ResultsView = CollectionViewSource.GetDefaultView(Results);
        StartCommand = new AsyncRelayCommand(async _ => await Task.Run(async () => await Start(_), _), Scan_CanExecute);
        StopCommand = new AsyncRelayCommand(async _ => await Task.Run(async () => await Stop(), _));

        // Custom comparer to sort by IP address
        ((ListCollectionView)ResultsView).CustomSort = Comparer<IPScannerHostInfo>.Create((x, y) =>
            IPAddressHelper.CompareIPAddresses(x.PingInfo.IPAddress, y.PingInfo.IPAddress));
    }

    public override async Task OnLoaded()
    {
        await base.OnLoaded();
    }

    #endregion

    #region IRelayCommands & Actions

    public override IAsyncRelayCommand StartCommand { get; }
    public override IAsyncRelayCommand StopCommand { get; }

    private bool Scan_CanExecute() 
    {
        return Application.Current.MainWindow != null && !((MetroWindow)Application.Current.MainWindow).IsAnyDialogOpen;
    }

    public IRelayCommand DetectSubnetCommand => new RelayCommand(DetectSubnetAction);

    private void DetectSubnetAction()
    {
        DetectIPRange().ConfigureAwait(false);
    }

    public IRelayCommand RedirectDataToApplicationCommand => new RelayCommand<object>(name => RedirectDataToApplicationAction(name));

    private void RedirectDataToApplicationAction(object name)
    {
        if (name is not ApplicationName applicationName)
            return;

        var host = !string.IsNullOrEmpty(SelectedResult.Hostname)
            ? SelectedResult.Hostname
            : SelectedResult.PingInfo.IPAddress.ToString();

        EventSystem.RedirectToApplication(applicationName, host);
    }

    public IRelayCommand PerformDNSLookupIPAddressCommand => new RelayCommand(PerformDNSLookupIPAddressAction);

    private void PerformDNSLookupIPAddressAction()
    {
        EventSystem.RedirectToApplication(ApplicationName.DNSLookup, SelectedResult.PingInfo.IPAddress.ToString());
    }

    public IRelayCommand PerformDNSLookupHostnameCommand => new RelayCommand(PerformDNSLookupHostnameAction);

    private void PerformDNSLookupHostnameAction()
    {
        EventSystem.RedirectToApplication(ApplicationName.DNSLookup, SelectedResult.Hostname);
    }

    public IRelayCommand CustomCommandCommand => new RelayCommand<object>(guid => CustomCommandAction(guid));

    private void CustomCommandAction(object guid)
    {
        CustomCommand(guid).ConfigureAwait(false);
    }

    public IRelayCommand AddProfileSelectedHostCommand => new RelayCommand(AddProfileSelectedHostAction);

    private async void AddProfileSelectedHostAction()
    {
        ProfileInfo profileInfo = new()
        {
            Name = string.IsNullOrEmpty(SelectedResult.Hostname)
                ? SelectedResult.PingInfo.IPAddress.ToString()
                : SelectedResult.Hostname.TrimEnd('.'),
            Host = SelectedResult.PingInfo.IPAddress.ToString(),

            // Additional data
            WakeOnLAN_MACAddress = SelectedResult.MACAddress
        };

        var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive);

        await ProfileDialogManager.ShowAddProfileDialog(window, this, _dialogCoordinator, profileInfo, null,
            ApplicationName.IPScanner);
    }

    public IRelayCommand CopySelectedPortsCommand => new RelayCommand(CopySelectedPortsAction);

    private void CopySelectedPortsAction()
    {
        StringBuilder stringBuilder = new();

        foreach (var port in SelectedResult.Ports)
            stringBuilder.AppendLine(
                $"{port.Port}/{port.LookupInfo.Protocol},{ResourceTranslator.Translate(ResourceIdentifier.PortState, port.State)},{port.LookupInfo.Service},{port.LookupInfo.Description}");

        ClipboardHelper.SetClipboard(stringBuilder.ToString());
    }
    #endregion

    #region Methods

    public override async Task Start(CancellationToken cancellationToken)
    {
        await base.Start(cancellationToken).ConfigureAwait(true);
        await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
        {
            IsStatusMessageDisplayed = false;
            PreparingScan = true;
            Results.Clear();
            DragablzTabItem.SetTabHeader(_tabId, InputEntry);
        });
        // Resolve hostnames
        (List<(IPAddress ipAddress, string hostname)> hosts, List<string> hostnamesNotResolved) hosts;

        try
        {
            hosts = await HostRangeHelper.ResolveAsync(HostRangeHelper.CreateListFromInput(InputEntry),
                SettingsManager.Current.Network_ResolveHostnamePreferIPv4, CancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            UserHasCanceled(this, EventArgs.Empty);
            return;
        }

        // Show error message if (some) hostnames could not be resolved
        if (hosts.hostnamesNotResolved.Count > 0)
        {
            StatusMessage =
                $"{Strings.TheFollowingHostnamesCouldNotBeResolved} {string.Join(", ", hosts.hostnamesNotResolved)}";
            IsStatusMessageDisplayed = true;
        }
        await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
        {
            HostsToScan = hosts.hosts.Count;
            HostsScanned = 0;

            PreparingScan = false;
            // Add host(s) to the history
            AddHostToHistory(InputEntry);
        });        

        var ipScanner = new IPScanner(new IPScannerOptions(
            SettingsManager.Current.IPScanner_MaxHostThreads,
            SettingsManager.Current.IPScanner_MaxPortThreads,
            SettingsManager.Current.IPScanner_ICMPAttempts,
            SettingsManager.Current.IPScanner_ICMPTimeout,
            new byte[SettingsManager.Current.IPScanner_ICMPBuffer],
            SettingsManager.Current.IPScanner_ResolveHostname,
            SettingsManager.Current.IPScanner_PortScanEnabled,
            PortRangeHelper.ConvertPortRangeToIntArray(SettingsManager.Current.IPScanner_PortScanPorts),
            SettingsManager.Current.IPScanner_PortScanTimeout,
            SettingsManager.Current.IPScanner_NetBIOSEnabled,
            SettingsManager.Current.IPScanner_NetBIOSTimeout,
            SettingsManager.Current.IPScanner_ResolveMACAddress,
            SettingsManager.Current.IPScanner_ShowAllResults
        ));

        ipScanner.HostScanned += HostScanned;
        ipScanner.ScanComplete += ScanCompleted;
        ipScanner.ProgressChanged += ProgressChanged;
        ipScanner.UserHasCanceled += UserHasCanceled;
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                UserHasCanceled(this, EventArgs.Empty);
                return;
            }
            await ipScanner.ScanAsync(hosts.hosts, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            
        }
        catch (Exception ex)
        {
            await StopCommand.ExecuteAsync(null);
            StatusMessage =
                $"{Strings.UnkownError} {ex.Message}";
            IsStatusMessageDisplayed = true;
            
        }

    }

    public override async Task Stop()
    {
        ScanCompleted(null, null);
        await base.Stop();
    }

    private async Task DetectIPRange()
    {
        IsSubnetDetectionRunning = true;

        var localIP = await NetworkInterface.DetectLocalIPAddressBasedOnRoutingAsync(IPAddress.Parse("1.1.1.1"));

        // Could not detect local ip address
        if (localIP != null)
        {
            var subnetmaskDetected = false;

            // Get subnetmask, based on ip address
            foreach (var networkInterface in (await NetworkInterface.GetNetworkInterfacesAsync(CancellationTokenSource.Token)).Where(
                         networkInterface => networkInterface.IPv4Address.Any(x => x.Item1.Equals(localIP))))
            {
                subnetmaskDetected = true;

                InputEntry = $"{localIP}/{Subnetmask.ConvertSubnetmaskToCidr(networkInterface.IPv4Address.First().Item2)}";

                // Fix: If the user clears the TextBox and then clicks again on the button, the TextBox remains empty...
                OnPropertyChanged(nameof(InputEntry));

                break;
            }

            if (!subnetmaskDetected)
                await _dialogCoordinator.ShowMessageAsync(this, Strings.Error,
                    Strings.CouldNotDetectSubnetmask, MessageDialogStyle.Affirmative,
                    AppearanceManager.MetroDialog);
        }
        else
        {
            await _dialogCoordinator.ShowMessageAsync(this, Strings.Error,
                Strings.CouldNotDetectLocalIPAddressMessage, MessageDialogStyle.Affirmative,
                AppearanceManager.MetroDialog);
        }

        IsSubnetDetectionRunning = false;
    }

    protected async Task CustomCommand(object guid)
    {
        if (guid is Guid id)
        {
            var info = (CustomCommandInfo)CustomCommands.FirstOrDefault(x => x.ID == id)?.Clone();

            if (info == null)
                return; // ToDo: Log and error message

            // Replace vars
            var hostname = !string.IsNullOrWhiteSpace(SelectedResult.Hostname) ? SelectedResult.Hostname.TrimEnd('.') : "";
            var ipAddress = SelectedResult.PingInfo.IPAddress.ToString();
            
            info.FilePath = Regex.Replace(info.FilePath, "\\$\\$hostname\\$\\$", string.IsNullOrWhiteSpace(hostname) ? ipAddress : hostname, RegexOptions.IgnoreCase);
            info.FilePath = Regex.Replace(info.FilePath, "\\$\\$ipaddress\\$\\$", ipAddress, RegexOptions.IgnoreCase);

            if (!string.IsNullOrEmpty(info.Arguments))
            {
                info.Arguments = Regex.Replace(info.Arguments, "\\$\\$hostname\\$\\$", string.IsNullOrWhiteSpace(hostname) ? ipAddress : hostname,
                    RegexOptions.IgnoreCase);
                info.Arguments = Regex.Replace(info.Arguments, "\\$\\$ipaddress\\$\\$", ipAddress,
                    RegexOptions.IgnoreCase);
            }

            try
            {
                Utilities.CustomCommand.Run(info);
            }
            catch (Exception ex)
            {
                await _dialogCoordinator.ShowMessageAsync(this,
                    Strings.ResourceManager.GetString("Error",
                        LocalizationManager.GetInstance().Culture), ex.Message, MessageDialogStyle.Affirmative,
                    AppearanceManager.MetroDialog);
            }
        }
    }

    private void AddHostToHistory(string ipRange)
    {
        // Create the new list
        var list = ListHelper.Modify(SettingsManager.Current.IPScanner_HostHistory.ToList(), ipRange,
            SettingsManager.Current.General_HistoryListEntries);

        // Clear the old items
        SettingsManager.Current.IPScanner_HostHistory.Clear();
        OnPropertyChanged(nameof(InputEntry)); // Raise property changed again, after the collection has been cleared

        // Fill with the new items
        list.ForEach(SettingsManager.Current.IPScanner_HostHistory.Add);
    }

    protected override async Task Export()
    {
        await Task.Yield();
        var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive);

        var customDialog = new CustomDialog
        {
            Title = Strings.Export
        };
        var exportViewModel = new ExportViewModel(async instance =>
        {
            await _dialogCoordinator.HideMetroDialogAsync(window, customDialog);

            try
            {
                ExportManager.Export(instance.FilePath, instance.FileType,
                    instance.ExportAll
                        ? Results
                        : new ObservableCollection<IPScannerHostInfo>(SelectedResults.Cast<IPScannerHostInfo>()
                            .ToArray()));
            }
            catch (Exception ex)
            {
                var settings = AppearanceManager.MetroDialog;
                settings.AffirmativeButtonText = Strings.OK;

                await _dialogCoordinator.ShowMessageAsync(window, Strings.Error,
                    Strings.AnErrorOccurredWhileExportingTheData + Environment.NewLine +
                    Environment.NewLine + ex.Message, MessageDialogStyle.Affirmative, settings);
            }

            SettingsManager.Current.IPScanner_ExportFileType = instance.FileType;
            SettingsManager.Current.IPScanner_ExportFilePath = instance.FilePath;
        }, _ => { _dialogCoordinator.HideMetroDialogAsync(window, customDialog); }, [
            ExportFileType.Csv, ExportFileType.Xml, ExportFileType.Json
        ], true, SettingsManager.Current.IPScanner_ExportFileType, SettingsManager.Current.IPScanner_ExportFilePath);

        customDialog.Content = new ExportDialog
        {
            DataContext = exportViewModel
        };

        await _dialogCoordinator.ShowMetroDialogAsync(window, customDialog);
    }

    public override async Task OnClose()
    {
        await base.OnClose();

        ConfigurationManager.Current.IPScannerTabCount--;
    }

    #endregion

    #region Events
    private void HostScanned(object sender, IPScannerHostScannedArgs e)
    {
        if (e is null || e.Args is null)
            return;
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal,() =>
        {
            if (Results.Any(h => h.PingInfo.IPAddressInt32 == e.Args.PingInfo.IPAddressInt32) 
                || (!SettingsManager.Current.IPScanner_ShowAllResults && !e.Args.IsReachable))
                return;

            Results.Add(e.Args);
        });
    }

    private void ProgressChanged(object sender, ProgressChangedArgs e)
    {
        if (e is null)
            return;
        HostsScanned = e.Value;
    }

    private void ScanCompleted(object sender, EventArgs e)
    {
        if (!StartCommand.IsCancellationRequested)
        {
            if (Results.Count == 0)
            {
                StatusMessage = Strings.NoReachableHostsFound;
            }
            StatusMessage = "Scan Completed.";
            IsStatusMessageDisplayed = true; 
        }
        IsCanceling = false;
        CancellationTokenSource = new();
    }

    private void UserHasCanceled(object sender, EventArgs e)
    {
        StatusMessage = Strings.CanceledByUserMessage;
        IsStatusMessageDisplayed = true;

        IsCanceling = false;
        StartCommand.Cancel();
        CancellationTokenSource.Cancel();        
    }

    #endregion
}