using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NETworkManager.Models.Lookup;
using NETworkManager.Utilities;
using System.Collections.Concurrent;
using System.Linq;


namespace NETworkManager.Models.Network;

public sealed class PortScanner
{
    #region Constructor

    public PortScanner(PortScannerOptions options)
    {
        _options = options;
    }

    #endregion

    #region Variables

    private int _progressValue;

    private readonly PortScannerOptions _options;

    #endregion

    #region Events

    public event EventHandler<PortScannerPortScannedArgs> PortScanned;

    private void OnPortScanned(PortScannerPortScannedArgs e)
    {
        PortScanned?.Invoke(this, e);
    }

    public event EventHandler ScanComplete;

    private void OnScanComplete()
    {
        ScanComplete?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler<ProgressChangedArgs> ProgressChanged;

    private void OnProgressChanged()
    {
        ProgressChanged?.Invoke(this, new ProgressChangedArgs(_progressValue));
    }

    public event EventHandler UserHasCanceled;

    private void OnUserHasCanceled()
    {
        UserHasCanceled?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Methods
    public Task<IEnumerable<PortInfo>> ScanPortsAsync((IPAddress ipAddress, string hostname) host, IEnumerable<int> ports, CancellationToken cancellationToken)
    {
        var portParallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = _options.MaxPortThreads
        };
        return ScanPortsAsync(host, ports, portParallelOptions);
    }
    private async Task<IEnumerable<PortInfo>> ScanPortsAsync((IPAddress ipAddress, string hostname) host, IEnumerable<int> ports, ParallelOptions parallelOptions)
    {
        ConcurrentBag<PortInfo> results = [];
                
        await Parallel.ForEachAsync(ports, parallelOptions, async (port, _ct) =>
        {
            // Test if port is open
            var random = new Random().Next(100, 500);
            var tmp = _options.Timeout;
            _options.Timeout += random;
            var portResult = await ScanPortAsync(host.ipAddress, port, _ct).ConfigureAwait(false);
            _options.Timeout = tmp;
            if (portResult.State == PortState.Open || _options.ShowAllResults)
            {
                results.Add(portResult);
                await Task.Delay(random, _ct); //Random delay for UI smoothness
                OnPortScanned(new PortScannerPortScannedArgs(new PortScannerPortInfo(host.ipAddress, host.hostname, portResult.Port, portResult.LookupInfo, portResult.State)));
            }
            IncreaseProgress();
        }).ConfigureAwait(false);
        return results.AsEnumerable();
    }

    private async Task<PortInfo> ScanPortAsync(IPAddress ipAddress, int port, CancellationToken cancellationToken)
    {
        var portState = PortState.None;
        var result = new PortInfo(port, null, portState);
        using var tcpClient = new TcpClient(ipAddress.AddressFamily);
        tcpClient.SendTimeout = tcpClient.ReceiveTimeout = _options.Timeout;
        tcpClient.NoDelay = true;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await tcpClient.ConnectAsync(ipAddress, port, cancellationToken).ConfigureAwait(false);
            portState = tcpClient.Connected ? PortState.Open : PortState.Closed;
            var isSocketReadable = tcpClient.GetStream().CanRead;
        }
        catch
        {
            portState = PortState.Closed;
            result.State = portState;
        }
        finally
        {
            tcpClient.Close();
            //
        }        
        if (portState == PortState.Open)//options.ShowAllResults)
             result = new(port, PortLookup.LookupByPortAndProtocol(port), portState);
        return result;
    }

    public async Task ScanAsync(IEnumerable<(IPAddress ipAddress, string hostname)> hosts, IEnumerable<int> ports,
        CancellationToken cancellationToken)
    {
        _progressValue = 0;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hostParallelOptions = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = _options.MaxHostThreads
            };

            await Parallel.ForEachAsync(hosts, hostParallelOptions, async (host, _ct) =>
            {
                // Resolve Hostname (PTR)
                if (_options.ResolveHostname)
                {
                    var dnsResolverTask = await DNSClient.GetInstance().ResolvePtrAsync(host.ipAddress, _ct).ConfigureAwait(false);

                    if (!dnsResolverTask.HasError)
                        host.hostname = dnsResolverTask.Value;
                }
                // Check each port
                await ScanPortsAsync(host, ports, _ct).ConfigureAwait(false);
                IncreaseProgress();
            }).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            OnUserHasCanceled();
        }
        finally
        {
            OnScanComplete();
        }
    }

    private void IncreaseProgress()
    {
        // Increase the progress                        
        Interlocked.Increment(ref _progressValue);
        OnProgressChanged();
    }

    #endregion
}