using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using NETworkManager.Utilities;

namespace NETworkManager.Models.Network;

public sealed class Ping(int waitTime, int timeout, int ttl, bool dontFragment)
{
    #region Varaibles

    /// <summary>
    ///  The time between ping packets are sent, in milliseconds
    /// </summary>
    public int WaitTime { get; set; } = waitTime;
    /// <summary>
    ///  Timeout in milliseconds
    /// </summary>
    public int Timeout { get; set; } = timeout;
    public byte[] Buffer { get; set; } = new byte[32];
    public int TTL { get; set; } = ttl;
    public bool DontFragment { get; set; } = dontFragment;
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

    public Task<PingInfo> SendAsync(IPAddress ipAddress, int timeout, byte[] buffer, bool resolve = true, CancellationToken cancellationToken = default)
    {
        Buffer = buffer;
        Timeout = timeout;
        return SendAsync(ipAddress, 0, resolve, cancellationToken);
    }
    public async Task<PingInfo> SendAsync(IPAddress ipAddress, int attempts = 0, bool resolve = true, CancellationToken cancellationToken = default)
    {
        var hostname = string.Empty;
        try
        {
            // Try to resolve PTR
            if (resolve)
            {
                var dnsResult = await DNSClient.GetInstance().ResolvePtrAsync(ipAddress, cancellationToken);

                if (!dnsResult.HasError)
                {
                    hostname = dnsResult.Value;

                    OnHostnameResolved(new HostnameArgs(hostname));
                } 
            }

            var errorCount = 0;

            var options = new PingOptions
            {
                Ttl = TTL,
                DontFragment = DontFragment
            };

            using var ping = new System.Net.NetworkInformation.Ping();
            var iterations = attempts > 0 ? attempts : 1;

            while (!cancellationToken.IsCancellationRequested && iterations > 0)
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
                ResultInfo = new PingInfo(timestamp, pingReply.Address, hostname,
                                pingReply.Buffer.Length, pingReply.RoundtripTime, 
                                pingReply.Status);

                if (pingReply.Status != IPStatus.Success)
                {
                    ResultInfo.IPAddress = ipAddress;
                    ResultInfo.TTL = options.Ttl;
                }
                else
                {
                    ResultInfo.TTL = pingReply.Options.Ttl;
                }
                OnPingReceived(new PingReceivedArgs(ResultInfo));
                // If ping is canceled... dont wait for example 5 seconds
                for (var i = 0; i < WaitTime && !cancellationToken.IsCancellationRequested; i += 100)
                {
                    await Task.Delay(100, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.None);
                }
                if(attempts != 0)
                    iterations--;
            }
        }
        catch (OperationCanceledException)
        {
            OnUserHasCanceled();
        }
        // Currently not used (ping will run until the user cancels it)
        OnPingCompleted();
        return ResultInfo;
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