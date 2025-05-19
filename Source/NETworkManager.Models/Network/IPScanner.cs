using ControlzEx.Standard;
using NETworkManager.Models.Lookup;
using NETworkManager.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Windows.Services.Maps;

namespace NETworkManager.Models.Network;

public sealed class IPScanner(IPScannerOptions options)
{
    #region Variables

    private int _progressValue;
    private readonly ConcurrentQueue<Ping> _pingPool = new();

    #endregion

    #region Events

    public event EventHandler<IPScannerHostScannedArgs> HostScanned;

    private void OnHostScanned(IPScannerHostScannedArgs e)
    {
        HostScanned?.Invoke(this, e);
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

    public async Task<IPScannerHostInfo> ScanHostAsync((IPAddress ipAddress, string hostname) host, ParallelOptions portScanParallelOptions, CancellationToken cancellationToken)
    {
        var networkInterfaces = options.ResolveMACAddress ? NetworkInterface.GetNetworkInterfaces() : [];
        //var dnsHostname = host.hostname;
        IPScannerHostInfo hostInfo = null;

        var pingTask = PingAsync(host.ipAddress, cancellationToken);

        // Start port scan async (if enabled)
        var portScanTask = options.PortScanEnabled
            ? PortScanAsync(host, portScanParallelOptions, cancellationToken)
            : Task.FromResult(Enumerable.Empty<PortInfo>());

        // Start netbios lookup async (if enabled)
        var cts = new CancellationTokenSource();
        cts.CancelAfter(options.NetBIOSTimeout);
        var netbiosTask = options.NetBIOSEnabled
            ? NetBIOSResolver.ResolveAsync(host.ipAddress, options.NetBIOSTimeout, cts.Token)
            : Task.FromResult(new NetBIOSInfo(host.ipAddress));

        await Task.WhenAll([pingTask, portScanTask, netbiosTask]);
        // Get ping result
        var pingInfo = pingTask.Result;

        // Get port scan result
        var portScanResults = portScanTask.Result;

        // Get netbios result
        var netBIOSInfo = netbiosTask.Result;

        // Cancel if the user has canceled
        if (cancellationToken.IsCancellationRequested)
        {
            OnUserHasCanceled();
            return null;
        }

        // Check if host is up
        var isAnyPortOpen = portScanResults.Any(x => x.State == PortState.Open);
        var isReachable = pingInfo.Status == IPStatus.Success || // ICMP response
                          isAnyPortOpen || // Any port is open   
                          netBIOSInfo.IsReachable; // NetBIOS response
        hostInfo = new(host.hostname, pingInfo, netBIOSInfo);
        // DNS & ARP
        if (isReachable || options.ShowAllResults)
        {
            // DNS
            var dnsHostname = string.Empty;

            if (options.ResolveHostname)
            {
                dnsHostname = (await DNSClient.GetInstance().ResolvePtrAsync(host.ipAddress, cancellationToken)).Value;
            }

            // ARP
            var arpMACAddress = string.Empty;
            var arpVendor = string.Empty;

            if (options.ResolveMACAddress)
            {
                // Get info from arp table
                arpMACAddress = ARP.GetMACAddress(host.ipAddress);

                // Check if it is the local mac
                if (string.IsNullOrEmpty(arpMACAddress))
                {
                    var networkInterfaceInfo = networkInterfaces.FirstOrDefault(p =>
                        p.IPv4Address.Any(x => x.Item1.Equals(host.ipAddress)));

                    if (networkInterfaceInfo != null)
                        arpMACAddress = networkInterfaceInfo.PhysicalAddress.ToString();
                }

                // Vendor lookup & default format
                if (!string.IsNullOrEmpty(arpMACAddress))
                {
                    var info = OUILookup.LookupByMacAddress(arpMACAddress).FirstOrDefault();

                    if (info != null)
                        arpVendor = info.Vendor;

                    // Apply default format
                    arpMACAddress = MACAddressHelper.GetDefaultFormat(arpMACAddress);
                }
            }
            hostInfo = new IPScannerHostInfo(
                        isReachable,
                        pingInfo,
                        // DNS is default, fallback to netbios
                        !string.IsNullOrEmpty(dnsHostname)
                            ? dnsHostname
                            : netBIOSInfo?.ComputerName ?? string.Empty,
                        dnsHostname,
                        isAnyPortOpen,
                        portScanResults.OrderBy(x => x.Port).ToList(),
                        netBIOSInfo,
                        // ARP is default, fallback to netbios
                        !string.IsNullOrEmpty(arpMACAddress)
                            ? arpMACAddress
                            : netBIOSInfo?.MACAddress ?? string.Empty,
                        !string.IsNullOrEmpty(arpMACAddress)
                            ? arpVendor
                            : netBIOSInfo?.Vendor ?? string.Empty,
                        arpMACAddress,
                        arpVendor
                    );

        }

        return hostInfo;
    }
    public async Task ScanAsync(IEnumerable<(IPAddress ipAddress, string hostname)> hosts, CancellationToken cancellationToken)
    {
        _progressValue = 0;

        // Get all network interfaces (for local mac address lookup)
        var networkInterfaces = options.ResolveMACAddress ? NetworkInterface.GetNetworkInterfaces() : [];

        try
        {
            var hostParallelOptions = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = options.MaxHostThreads
            };
            var portScanParallelOptions = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = options.MaxPortThreads
            };

            // Start scan
            await Parallel.ForEachAsync(hosts, hostParallelOptions, async (host, cancellationToken) =>
            {
                // Start ping async
                var hostInfo = await ScanHostAsync(host, portScanParallelOptions, portScanParallelOptions.CancellationToken);
                OnHostScanned(new IPScannerHostScannedArgs(hostInfo));
                IncreaseProgress();
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            OnUserHasCanceled();
        }
        finally
        {
            OnScanComplete();
        }
    }

    private async Task<PingInfo> PingAsync(IPAddress ipAddress, CancellationToken cancellationToken)
    {
        var timestamp = DateTime.Now;
        var result = new PingInfo()
        {
            Timestamp = timestamp,
            IPAddress = ipAddress,
            Status = IPStatus.Unknown
        };
        try
        {
            if (!_pingPool.TryDequeue(out var ping))
            {
                ping = new Ping();
                _pingPool.Enqueue(ping);
            }

            // Get timestamp
            ping.Buffer = options.ICMPBuffer;
            ping.Timeout = options.ICMPTimeout;
            result = await ping.SendAsync(ipAddress, options.ICMPTimeout, options.ICMPBuffer, options.ResolveHostname, cancellationToken)
                                .ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext);
        }
        catch
        {

        }
        return result;


    }
    //TODO: Refactor to AsyncEnumerable
    private Task<IEnumerable<PortInfo>> PortScanAsync((IPAddress ipAddress, string hostname) host, ParallelOptions parallelOptions,
        CancellationToken cancellationToken)
    {
        var result = Task.FromResult(new List<PortInfo>().AsEnumerable());

        var portScanner = new PortScanner(new PortScannerOptions(
            options.MaxHostThreads,
            options.MaxPortThreads,
            options.PortScanTimeout,
            options.ResolveHostname,
            options.ShowAllResults
        ));

        //portScanner.PortScanned += PortScanned;
        //portScanner.ScanComplete += ScanComplete;
        //portScanner.ProgressChanged += ProgressChanged;
        //portScanner.UserHasCanceled += UserHasCanceled;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            result = portScanner.ScanPortsAsync(host, options.PortScanPorts, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            Debug.WriteLine("PortScan canceled");
        }
        return result;
    }

    private async IAsyncEnumerable<TResult> ScanItemsAsyncEx<TInput, TResult>(IEnumerable<TInput> src, IPAddress ipAddress, 
        Func<(IPAddress address, TInput port), CancellationToken, Task<TResult>> processor,
        ParallelOptions? parallelOptions = null,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Создаем неограниченный канал для потокобезопасной передачи результатов
        var channel = Channel.CreateUnbounded<TResult>(new UnboundedChannelOptions
        {
            SingleWriter = false // Несколько задач могут писать одновременно
        });
        parallelOptions.CancellationToken = cancellationToken;
        var processingTask = Task.CompletedTask;
        try
        {
            processingTask = Parallel.ForEachAsync(src, parallelOptions, async (p, ct) =>
            {
                // Test if port is open
                var result = await processor((ipAddress, p), ct);
                await channel.Writer.WriteAsync(result, ct);
            });
        }
        finally
        {
            channel.Writer.Complete();
        }
        await foreach (var result in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return result;
        }
        await processingTask;
    }

    private readonly Func<(IPAddress address, int port), CancellationToken, Task<PortInfo>> PortFunc = async (host, ct) =>
    {
        using var tcpClient = new TcpClient(host.address.AddressFamily);
        tcpClient.SendTimeout = tcpClient.ReceiveTimeout = options.PortScanTimeout;

        var portState = PortState.None;

        try
        {
            await tcpClient.ConnectAsync(host.address, host.port, ct);
            portState = tcpClient.Connected ? PortState.Open : PortState.Closed;
        }
        catch
        {
            portState = PortState.Closed;
        }
        finally
        {
            tcpClient.Close();            
        }
        return new(host.port, PortLookup.LookupByPortAndProtocol(host.port), portState);
    };

    private void IncreaseProgress()
    {
        // Increase the progress                        
        Interlocked.Increment(ref _progressValue);
        OnProgressChanged();
    }

    #endregion
}