using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NETworkManager.Utilities;

namespace NETworkManager.Models.Network;

public sealed class Ping
{
    #region Varaibles

    /// <summary>
    ///  The time between ping packets are sent, in milliseconds
    /// </summary>
    public int WaitTime { get; private set; }
    /// <summary>
    ///  Timeout in milliseconds
    /// </summary>
    public int Timeout { get; private set; }
    public byte[] Buffer { get; set; } = new byte[32];
    public int TTL { get; private set; }
    public bool DontFragment { get; private set; }
    public PingInfo ResultInfo { get; set; }

    private const int _exceptionCancelCount = 3;

    #endregion

    #region Events

    public event EventHandler<PingReceivedArgs> PingReceived;

    private void OnPingReceived(PingReceivedArgs e)
    {
        PingReceived?.Invoke(this, e);
    }

    public event EventHandler PingCompleted;

    private void OnPingCompleted()
    {
        PingCompleted?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler<PingExceptionArgs> PingException;

    private void OnPingException(PingExceptionArgs e)
    {
        PingException?.Invoke(this, e);
    }

    public event EventHandler<HostnameArgs> HostnameResolved;

    private void OnHostnameResolved(HostnameArgs e)
    {
        HostnameResolved?.Invoke(this, e);
    }

    public event EventHandler UserHasCanceled;

    private void OnUserHasCanceled()
    {
        UserHasCanceled?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Constructor, Methods

    public Ping() : this(1000, 4000, 64, true)
    {
        
    }


    public Ping(int waitTime, int timeout, int ttl, bool dontFragment)
    {
        WaitTime = waitTime;
        Timeout = timeout;
        TTL = ttl;
        DontFragment = dontFragment;
    }

    public Task SendAsync(IPAddress ipAddress, int timeout, byte[] buffer, CancellationToken cancellationToken)
    {
        Buffer = buffer;
        Timeout = timeout;
        return SendAsync(ipAddress, cancellationToken);
    }
    public async Task SendAsync(IPAddress ipAddress, CancellationToken cancellationToken)
    {
        var hostname = string.Empty;
        try
        {
            // Try to resolve PTR
            var dnsResult = await DNSClient.GetInstance().ResolvePtrAsync(ipAddress, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.None);

            if (!dnsResult.HasError)
            {
                hostname = dnsResult.Value;

                OnHostnameResolved(new HostnameArgs(hostname));
            }

            var errorCount = 0;

            var options = new PingOptions
            {
                Ttl = TTL,
                DontFragment = DontFragment
            };

            using var ping = new System.Net.NetworkInformation.Ping();

            do
            {
                // Get timestamp 
                var timestamp = DateTime.Now;

                // Send ping
                var pingReply = await ping.SendPingAsync(ipAddress, TimeSpan.FromMilliseconds(Timeout), Buffer, options, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.None);
                                
                if (pingReply == null)
                {
                    errorCount++;

                    if (errorCount == _exceptionCancelCount)
                    {
                        OnPingException(new PingExceptionArgs("No data received", null));
                        break;
                    }
                }
                else
                {
                    // Reset the error count (if no exception was thrown)
                    errorCount = 0;
                }

                if (pingReply.Status == IPStatus.Success)
                {
                    if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
                        OnPingReceived(new PingReceivedArgs(
                            new PingInfo(timestamp, pingReply.Address, hostname,
                                pingReply.Buffer.Length, pingReply.RoundtripTime, pingReply.Options!.Ttl,
                                pingReply.Status)));
                    else
                        OnPingReceived(new PingReceivedArgs(
                            new PingInfo(timestamp, pingReply.Address, hostname,
                                pingReply.Buffer.Length, pingReply.RoundtripTime, pingReply.Status)));
                }
                else
                {
                    ResultInfo = new PingInfo(timestamp, pingReply.Address, hostname, pingReply.Status);
                    OnPingReceived(new PingReceivedArgs(ResultInfo));

                }
                // If ping is canceled... dont wait for example 5 seconds
                for (var i = 0; i < WaitTime; i += 100)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    await Task.Delay(100, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.None);
                }
            } while (!cancellationToken.IsCancellationRequested);
        }
        catch (OperationCanceledException)
        {
            OnUserHasCanceled();
        }
        // Currently not used (ping will run until the user cancels it)
        OnPingCompleted();
        await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.None);        
    }

    // Param: disableSpecialChar --> ExportManager --> "<" this char cannot be displayed in xml
    public static string TimeToString(IPStatus status, long time, bool disableSpecialChar = false)
    {
        if (status != IPStatus.Success && status != IPStatus.TtlExpired)
            return "-/-";

        _ = long.TryParse(time.ToString(), out var t);

        return disableSpecialChar ? $"{t} ms" : t == 0 ? "<1 ms" : $"{t} ms";
    }

    #endregion
}