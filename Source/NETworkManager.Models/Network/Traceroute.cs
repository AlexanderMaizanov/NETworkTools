using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using NETworkManager.Models.IPApi;
using NETworkManager.Utilities;

namespace NETworkManager.Models.Network;

public sealed class Traceroute
{
    #region Variables

    private readonly TracerouteOptions _options;

    #endregion

    #region Constructor

    public Traceroute(TracerouteOptions options)
    {
        _options = options;
    }

    #endregion

    #region Methods

    public async Task TraceRouteAsync(IPAddress ipAddress, CancellationToken cancellationToken)
    {
        try
        {
            var pingOptions = new PingOptions { Ttl = 1, DontFragment = _options.DontFragment };
            var ping = new Ping(0, _options.Timeout, 1, _options.DontFragment);
            PingInfo[] results = [];

            for (var i = 1; i < _options.MaximumHops + 1; i++)
            {
                var i1 = i;
                ping.TTL = i1;

                try
                {
                    // Send 3 pings
                    results = await ping.SendAsync(ipAddress, 3, _options.ResolveHostname, cancellationToken).ConfigureAwait(false);
                }
                catch (AggregateException ex)
                {
                    // Remove duplicate messages
                    OnTraceError(new TracerouteErrorArgs(string.Join(", ",
                        ex.Flatten().InnerExceptions.Select(s => s.Message).Distinct())));
                    return;
                }
                // Check for cancel
                if (cancellationToken.IsCancellationRequested)
                {
                    OnUserHasCanceled();
                    return;
                }
                // Check results -> Get IP on success or TTL expired
                var ipAddressHop = (from task in results
                                    where task.Status != IPStatus.TimedOut
                                    where task.Status is IPStatus.TtlExpired or IPStatus.Success
                                    select task.IPAddress).FirstOrDefault();

                

                IPGeolocationResult ipGeolocationResult = null;

                // Get IP geolocation info
                if (_options.CheckIPApiIPGeolocation && ipAddressHop != null &&
                    !IPAddressHelper.IsPrivateIPAddress(ipAddressHop))
                    ipGeolocationResult =
                        await IPGeolocationService.GetInstance().GetIPGeolocationAsync($"{ipAddressHop}", cancellationToken).ConfigureAwait(false);
                
                OnHopReceived(new TracerouteHopReceivedArgs(new TracerouteHopInfo(i,
                    results[0].Status, results[0].Time,
                    results[1].Status, results[1].Time,
                    results[2].Status, results[2].Time,
                    ipAddressHop, results[0].Hostname,
                    ipGeolocationResult ?? new IPGeolocationResult())));

                // Check if finished
                if (ipAddressHop != null && ipAddress.ToString() == ipAddressHop.ToString())
                {
                    OnTraceComplete();
                    return;
                }

                // Check for cancel
                if (cancellationToken.IsCancellationRequested)
                {
                    OnUserHasCanceled();
                    return;
                }                
            }

            // Max hops reached...
            OnMaximumHopsReached(new MaximumHopsReachedArgs(_options.MaximumHops));
        }
        catch (Exception ex)
        {
            OnTraceError(new TracerouteErrorArgs(ex.Message + Environment.NewLine + ex.StackTrace));
        }

    }

    #endregion

    #region Events

    public event EventHandler<TracerouteHopReceivedArgs> HopReceived;

    private void OnHopReceived(TracerouteHopReceivedArgs e)
    {
        HopReceived?.Invoke(this, e);
    }

    public event EventHandler TraceComplete;

    private void OnTraceComplete()
    {
        TraceComplete?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler<MaximumHopsReachedArgs> MaximumHopsReached;

    private void OnMaximumHopsReached(MaximumHopsReachedArgs e)
    {
        MaximumHopsReached?.Invoke(this, e);
    }

    public event EventHandler<TracerouteErrorArgs> TraceError;

    private void OnTraceError(TracerouteErrorArgs e)
    {
        TraceError?.Invoke(this, e);
    }

    public event EventHandler UserHasCanceled;

    private void OnUserHasCanceled()
    {
        UserHasCanceled?.Invoke(this, EventArgs.Empty);
    }

    #endregion
}