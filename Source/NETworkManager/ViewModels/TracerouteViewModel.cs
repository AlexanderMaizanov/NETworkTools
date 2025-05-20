using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using log4net;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using NETworkManager.Controls;
using NETworkManager.Localization.Resources;
using NETworkManager.Models;
using NETworkManager.Models.EventSystem;
using NETworkManager.Models.Export;
using NETworkManager.Models.Network;
using NETworkManager.Settings;
using NETworkManager.Utilities;
using NETworkManager.Views;
using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;

namespace NETworkManager.ViewModels;

public class TracerouteViewModel : ViewModelCollectionBase<TracerouteHopInfo>
{
    #region Variables

    private readonly ILog Log = LogManager.GetLogger(typeof(TracerouteHopInfo));
    private readonly IDialogCoordinator _dialogCoordinator;
    private readonly Guid _tabId;
    private bool _ipGeolocationRateLimitIsReached;

    private TracerouteHopInfo _selectedResult;

    public TracerouteHopInfo SelectedResult
    {
        get => _selectedResult;
        set
        {
            if (value == _selectedResult)
                return;

            _selectedResult = value;
            OnPropertyChanged();
        }
    }
    #endregion

    #region Constructor, load settings

    public TracerouteViewModel(IDialogCoordinator instance, Guid tabId, string host)
    {
        _dialogCoordinator = instance;

        ConfigurationManager.Current.TracerouteTabCount++;

        _tabId = tabId;
        Host = host;

        // Set collection view
        HostHistoryView = CollectionViewSource.GetDefaultView(SettingsManager.Current.Traceroute_HostHistory);

        // Result view
        ResultsView = CollectionViewSource.GetDefaultView(Results);
        ResultsView.SortDescriptions.Add(new SortDescription(nameof(TracerouteHopInfo.Hop),
            ListSortDirection.Ascending));

        LoadSettings();
    }

    public override async Task OnLoaded()
    {
        await base.OnLoaded();
    }

    private void LoadSettings()
    {
    }

    #endregion

    #region ICommands & Actions

    public override IAsyncRelayCommand ScanCommand => new AsyncRelayCommand(async _ =>
    {
        if (ScanCommand.IsRunning)
            await Stop();
        else
            await StartTrace().ConfigureAwait(false);
    }, Trace_CanExecute);

    private bool Trace_CanExecute()
    {
        return Application.Current.MainWindow != null &&
               !((MetroWindow)Application.Current.MainWindow).IsAnyDialogOpen;
    }    

    public ICommand RedirectDataToApplicationCommand => new RelayCommand<object>(RedirectDataToApplicationAction);

    private void RedirectDataToApplicationAction(object name)
    {
        if (name is not ApplicationName applicationName)
            return;

        var host = !string.IsNullOrEmpty(SelectedResult.Hostname)
            ? SelectedResult.Hostname
            : SelectedResult.IPAddress.ToString();

        EventSystem.RedirectToApplication(applicationName, host);
    }

    public ICommand PerformDNSLookupCommand => new RelayCommand<object>(PerformDNSLookupAction);

    private void PerformDNSLookupAction(object data)
    {
        EventSystem.RedirectToApplication(ApplicationName.DNSLookup, data.ToString());
    }

    public ICommand CopyTimeToClipboardCommand => new RelayCommand<object>(CopyTimeToClipboardAction);

    private void CopyTimeToClipboardAction(object timeIdentifier)
    {
        var time = timeIdentifier switch
        {
            "1" => Ping.TimeToString(SelectedResult.Status1, SelectedResult.Time1),
            "2" => Ping.TimeToString(SelectedResult.Status2, SelectedResult.Time2),
            "3" => Ping.TimeToString(SelectedResult.Status3, SelectedResult.Time3),
            _ => "-/-"
        };

        ClipboardHelper.SetClipboard(time);
    }

    //public override IAsyncRelayCommand ExportCommand => new AsyncRelayCommand(async _ => await ExportAction(CancellationTokenSource.Token));

    //private async Task ExportAction(CancellationToken cancellationToken)
    //{
    //    await Export(cancellationToken).ConfigureAwait(false);
    //}

    #endregion

    #region Methods

    public override async Task Stop()
    {
        await base.Stop();
    }

    public override async Task Start(CancellationToken cancellationToken)
    {
        await base.Start(cancellationToken);

    }

    private async Task StartTrace()
    {
        if(CancellationTokenSource.IsCancellationRequested)
        {
            CancellationTokenSource.Dispose();
            CancellationTokenSource = new CancellationTokenSource();
        }
            

        _ipGeolocationRateLimitIsReached = false;
        StatusMessage = string.Empty;
        IsStatusMessageDisplayed = false;
        IsCanceling = CancellationTokenSource.IsCancellationRequested;
        //IsRunning = !CancellationTokenSource.IsCancellationRequested;
        Results.Clear();

        DragablzTabItem.SetTabHeader(_tabId, Host);
        
        // Try to parse the string into an IP-Address
        if (!IPAddress.TryParse(Host, out var ipAddress))
        {
            var dnsResult =
                await DNSClientHelper.ResolveAorAaaaAsync(Host,
                    SettingsManager.Current.Network_ResolveHostnamePreferIPv4, CancellationTokenSource.Token);

            if (dnsResult.HasError)
            {
                DisplayStatusMessage(DNSClientHelper.FormatDNSClientResultError(Host, dnsResult));

                //IsRunning = false;

                return;
            }

            ipAddress = dnsResult.Value;
        }

        try
        {
            var traceroute = new Traceroute(new TracerouteOptions(
                SettingsManager.Current.Traceroute_Timeout,
                new byte[SettingsManager.Current.Traceroute_Buffer],
                SettingsManager.Current.Traceroute_MaximumHops,
                true,
                SettingsManager.Current.Traceroute_ResolveHostname,
                SettingsManager.Current.Traceroute_CheckIPApiIPGeolocation
            ));

            traceroute.HopReceived += Traceroute_HopReceived;
            traceroute.TraceComplete += Traceroute_TraceComplete;
            traceroute.MaximumHopsReached += Traceroute_MaximumHopsReached;
            traceroute.TraceError += Traceroute_TraceError;
            traceroute.UserHasCanceled += Traceroute_UserHasCanceled;
            AddHostToHistory(Host);

            await traceroute.TraceRouteAsync(ipAddress, CancellationTokenSource.Token).ConfigureAwait(false);

            // Add the host to history
            
        }
        catch (Exception ex) // This will catch any exception
        {
            CancellationTokenSource.Cancel();

            DisplayStatusMessage(ex.Message);
        }
    }

    protected override async Task Export()
    {
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
                            : SelectedResults.Cast<TracerouteHopInfo>().ToArray());
                }
                catch (Exception ex)
                {
                    var settings = AppearanceManager.MetroDialog;
                    settings.AffirmativeButtonText = Strings.OK;

                    await _dialogCoordinator.ShowMessageAsync(window, Strings.Error,
                        Strings.AnErrorOccurredWhileExportingTheData + Environment.NewLine +
                        Environment.NewLine + ex.Message, MessageDialogStyle.Affirmative, settings);
                }

                SettingsManager.Current.Traceroute_ExportFileType = instance.FileType;
                SettingsManager.Current.Traceroute_ExportFilePath = instance.FilePath;
            }, _ => { _dialogCoordinator.HideMetroDialogAsync(window, customDialog); },
            [ExportFileType.Csv, ExportFileType.Xml, ExportFileType.Json],
            true,
            SettingsManager.Current.Traceroute_ExportFileType, SettingsManager.Current.Traceroute_ExportFilePath
        );

        customDialog.Content = new ExportDialog
        {
            DataContext = exportViewModel
        };

        await _dialogCoordinator.ShowMetroDialogAsync(window, customDialog);
    }

    private void AddHostToHistory(string host)
    {
        // Create the new list
        var list = ListHelper.Modify(SettingsManager.Current.Traceroute_HostHistory.ToList(), host,
            SettingsManager.Current.General_HistoryListEntries);

        // Clear the old items
        SettingsManager.Current.Traceroute_HostHistory.Clear();
        OnPropertyChanged(nameof(Host)); // Raise property changed again, after the collection has been cleared

        // Fill with the new items
        list.ForEach(x => SettingsManager.Current.Traceroute_HostHistory.Add(x));
    }

    public override async Task OnClose()
    {
        await base.OnClose();

        ConfigurationManager.Current.TracerouteTabCount--;
    }

    #endregion

    #region Events

    private void Traceroute_HopReceived(object sender, TracerouteHopReceivedArgs e)
    {
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
        {
            // Check error
            if (e.Args.IPGeolocationResult.HasError)
            {
                Log.Error(
                    $"ip-api.com error: {e.Args.IPGeolocationResult.ErrorMessage}, error code: {e.Args.IPGeolocationResult.ErrorCode}");

                DisplayStatusMessage($"ip-api.com: {e.Args.IPGeolocationResult.ErrorMessage}");
            }

            // Check rate limit 
            if (!_ipGeolocationRateLimitIsReached && e.Args.IPGeolocationResult.RateLimitIsReached)
            {
                _ipGeolocationRateLimitIsReached = true;

                Log.Warn(
                    $"ip-api.com rate limit reached. Try again in {e.Args.IPGeolocationResult.RateLimitRemainingTime} seconds.");

                DisplayStatusMessage(
                    $"ip-api.com {string.Format(Strings.RateLimitReachedTryAgainInXSeconds, e.Args.IPGeolocationResult.RateLimitRemainingTime)}");
            }

            Results.Add(e.Args);
        });
    }

    private void Traceroute_MaximumHopsReached(object sender, MaximumHopsReachedArgs e)
    {
        DisplayStatusMessage(string.Format(Strings.MaximumNumberOfHopsReached, e.Hops));
        //IsRunning = false;
    }

    private void Traceroute_UserHasCanceled(object sender, EventArgs e)
    {
        DisplayStatusMessage(Strings.CanceledByUserMessage);
        //IsRunning = false;
        IsCanceling = false;
    }

    private void Traceroute_TraceError(object sender, TracerouteErrorArgs e)
    {
        DisplayStatusMessage(e.ErrorMessage);
        //IsRunning = false;
    }

    private void Traceroute_TraceComplete(object sender, EventArgs e)
    {
        //IsRunning = false;
    }

    

    #endregion
}