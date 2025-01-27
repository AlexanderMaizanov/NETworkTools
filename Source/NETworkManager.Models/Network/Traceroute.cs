﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            var stopwatch = new Stopwatch();
            var pingOptions = new PingOptions { Ttl = 1, DontFragment = _options.DontFragment };
            var tasks = new List<Tuple<PingReply, long>>();
            var ping = new System.Net.NetworkInformation.Ping();

            for (var i = 1; i < _options.MaximumHops + 1; i++)
            {                
                var i1 = i;
                pingOptions.Ttl = i1;

                try
                {
                    // Send 3 pings
                    if (tasks.Count > 0)
                    {
                        tasks.Clear();
                    }
                    for (var y = 0; y < 3; y++)
                    {
                        stopwatch.Restart();
                        var pingReply = await ping.SendPingAsync(ipAddress, TimeSpan.FromMilliseconds(_options.Timeout), _options.Buffer, pingOptions, cancellationToken);
                        stopwatch.Stop();

                        tasks.Add(Tuple.Create(pingReply, stopwatch.ElapsedMilliseconds));
                    }
                }
                catch (AggregateException ex)
                {
                    // Remove duplicate messages
                    OnTraceError(new TracerouteErrorArgs(string.Join(", ",
                        ex.Flatten().InnerExceptions.Select(s => s.Message).Distinct())));
                    return;
                }

                // Check results -> Get IP on success or TTL expired
                var ipAddressHop = (from task in tasks
                                    where task.Item1.Status != IPStatus.TimedOut
                                    where task.Item1.Status is IPStatus.TtlExpired or IPStatus.Success
                                    select task.Item1.Address).FirstOrDefault();

                // Resolve Hostname
                var hostname = string.Empty;

                if (_options.ResolveHostname && ipAddressHop != null)
                {
                    var dnsResult = await DNSClient.GetInstance().ResolvePtrAsync(ipAddressHop, cancellationToken);

                    if (!dnsResult.HasError)
                        hostname = dnsResult.Value;
                }

                IPGeolocationResult ipGeolocationResult = null;

                // Get IP geolocation info
                if (_options.CheckIPApiIPGeolocation && ipAddressHop != null &&
                    !IPAddressHelper.IsPrivateIPAddress(ipAddressHop))
                    ipGeolocationResult =
                        await IPGeolocationService.GetInstance().GetIPGeolocationAsync($"{ipAddressHop}");

                OnHopReceived(new TracerouteHopReceivedArgs(new TracerouteHopInfo(i,
                    tasks[0].Item1.Status, tasks[0].Item2,
                    tasks[1].Item1.Status, tasks[1].Item2,
                    tasks[2].Item1.Status, tasks[2].Item2,
                    ipAddressHop, hostname, ipGeolocationResult ?? new IPGeolocationResult())));

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
            OnTraceError(new TracerouteErrorArgs(ex.Message));
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