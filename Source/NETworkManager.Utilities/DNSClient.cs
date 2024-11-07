using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DnsClient;
using DnsClient.Protocol;

namespace NETworkManager.Utilities;

public class DNSClient : SingletonBase<DNSClient>
{
    /// <summary>
    ///     Error message which is returned when the DNS client is not configured.
    /// </summary>
    private const string _notConfiguredMessage = "DNS client is not configured. Call Configure() first.";

    /// <summary>
    ///     Hold the current instance of the LookupClient.
    /// </summary>
    private LookupClient _client;

    /// <summary>
    ///     Indicates if the DNS client is configured.
    /// </summary>
    private bool _isConfigured;

    /// <summary>
    ///     Store the current DNS settings.
    /// </summary>
    private DNSClientSettings _settings;

    /// <summary>
    ///     Method to configure the DNS client.
    /// </summary>
    /// <param name="settings"></param>
    public void Configure(DNSClientSettings settings)
    {
        _settings = settings;

        if (_settings.UseCustomDNSServers)
        {
            // Setup custom DNS servers
            List<NameServer> servers = [];

            foreach (var (server, port) in _settings.DNSServers)
                servers.Add(new IPEndPoint(IPAddress.Parse(server), port));

            _client = new LookupClient(new LookupClientOptions(servers.ToArray()));
        }
        else
        {
            UpdateFromWindows();
        }

        _isConfigured = true;
    }

    /// <summary>
    ///     Method to update the (Windows) name servers of the DNS client
    ///     when they may have changed due to a network update.
    /// </summary>
    public void UpdateFromWindows()
    {
        // Default (Windows) settings
        if (_settings.UseCustomDNSServers)
            return;

        _client = new LookupClient();
    }

    /// <summary>
    ///     Resolve an IPv4 address from a hostname or FQDN.
    /// </summary>
    /// <param name="query">Hostname or FQDN as string like "example.com".</param>
    /// <returns><see cref="IPAddress" /> of the host.</returns>
    public async Task<DNSClientResult> ResolveAsync(string query, QueryType queryType = QueryType.A, CancellationToken token = default)
    {
        if (!_isConfigured)
            throw new DNSClientNotConfiguredException(_notConfiguredMessage);

        try
        {
            var queryResult = await _client.QueryAsync(query, queryType, cancellationToken: token).ConfigureAwait(ConfigureAwaitOptions.None);

            // Pass the error we got from the lookup client (dns server).
            if (queryResult.HasError)
                return new DNSClientResultIPAddress(queryResult.HasError, queryResult.ErrorMessage, $"{queryResult.NameServer}");

            // Validate result because of https://github.com/BornToBeRoot/NETworkManager/issues/1934

            DnsResourceRecord record = default;
            DNSClientResult result = default;

            switch (queryType)

            {
                //case QueryType.None:
                //    break;
                case QueryType.A:
                    record = queryResult.Answers.ARecords().FirstOrDefault();
                    result = new DNSClientResultIPAddress(((AddressRecord)record).Address, queryResult.NameServer.Address);
                    break;
                case QueryType.AAAA:
                    record = queryResult.Answers.AaaaRecords().FirstOrDefault();
                    result = new DNSClientResultIPAddress(((AddressRecord)record).Address, queryResult.NameServer.Address);
                    break;                
                case QueryType.CNAME:
                    record = queryResult.Answers.CnameRecords().FirstOrDefault();
                    result = new DNSClientResultString(((CNameRecord)record).CanonicalName, queryResult.NameServer.Address);
                    break;
                case QueryType.PTR:
                    record = queryResult.Answers.PtrRecords().FirstOrDefault();
                    result = new DNSClientResultString(((PtrRecord)record).PtrDomainName, queryResult.NameServer.Address);
                    break;
                case QueryType.NS:
                    break;
                case QueryType.SOA:
                    break;
                case QueryType.MB:
                    break;
                case QueryType.MG:
                    break;
                case QueryType.MR:
                    break;
                case QueryType.NULL:
                    break;
                case QueryType.WKS:
                    break;
                case QueryType.HINFO:
                    break;
                case QueryType.MINFO:
                    break;
                case QueryType.MX:
                    break;
                case QueryType.TXT:
                    break;
                case QueryType.RP:
                    break;
                case QueryType.AFSDB:
                    break;                
                case QueryType.SRV:
                    break;
                case QueryType.NAPTR:
                    break;
                case QueryType.CERT:
                    break;
                case QueryType.DS:
                    break;
                case QueryType.RRSIG:
                    break;
                case QueryType.NSEC:
                    break;
                case QueryType.DNSKEY:
                    break;
                case QueryType.NSEC3:
                    break;
                case QueryType.NSEC3PARAM:
                    break;
                case QueryType.TLSA:
                    break;
                case QueryType.SPF:
                    break;
                case QueryType.AXFR:
                    break;
                case QueryType.ANY:
                    break;
                case QueryType.URI:
                    break;
                case QueryType.CAA:
                    break;
                case QueryType.SSHFP:
                    break;
                default:
                    break;
            }
            return record != null
                ? result
                : new DNSClientResultIPAddress(true,
                    $"Result \"{query}\" could not be resolved and the DNS server did not return an error. Try to check your DNS server with: dig @{queryResult.NameServer.Address} {query}",
                    queryResult.NameServer.Address);
        }
        catch (DnsResponseException ex)
        {
            return new DNSClientResultIPAddress(true, ex.Message);
        }
    }

    /// <summary>
    ///     Resolve an IPv4 address from a hostname or FQDN.
    /// </summary>
    /// <param name="query">Hostname or FQDN as string like "example.com".</param>
    /// <returns><see cref="IPAddress" /> of the host.</returns>
    public async Task<DNSClientResultIPAddress> ResolveAAsync(string query, CancellationToken token)
    {
        return await ResolveAsync(query, QueryType.A, token).ConfigureAwait(ConfigureAwaitOptions.None) as DNSClientResultIPAddress;
        //if (!_isConfigured)
        //    throw new DNSClientNotConfiguredException(_notConfiguredMessage);

        //try
        //{
        //    var result = await _client.QueryAsync(query, QueryType.A, cancellationToken: token).ConfigureAwait(ConfigureAwaitOptions.None);

        //    // Pass the error we got from the lookup client (dns server).
        //    if (result.HasError)
        //        return new DNSClientResultIPAddress(result.HasError, result.ErrorMessage, $"{result.NameServer}");

        //    // Validate result because of https://github.com/BornToBeRoot/NETworkManager/issues/1934
        //    var record = result.Answers.ARecords().FirstOrDefault();

        //    return record != null
        //        ? new DNSClientResultIPAddress(record.Address, $"{result.NameServer}")
        //        : new DNSClientResultIPAddress(true,
        //            $"IP address for \"{query}\" could not be resolved and the DNS server did not return an error. Try to check your DNS server with: dig @{result.NameServer.Address} {query}",
        //            $"{result.NameServer}");
        //}
        //catch (DnsResponseException ex)
        //{
        //    return new DNSClientResultIPAddress(true, ex.Message);
        //}
    }

    /// <summary>
    ///     Resolve an IPv6 address from a hostname or FQDN.
    /// </summary>
    /// <param name="query">Hostname or FQDN as string like "example.com".</param>
    /// <returns><see cref="IPAddress" /> of the host.</returns>
    public async Task<DNSClientResultIPAddress> ResolveAaaaAsync(string query, CancellationToken token)
    {
        return await ResolveAsync(query, QueryType.AAAA, token).ConfigureAwait(ConfigureAwaitOptions.None) as DNSClientResultIPAddress;
        //if (!_isConfigured)
        //    throw new DNSClientNotConfiguredException(_notConfiguredMessage);

        //try
        //{
        //    var result = await _client.QueryAsync(query, QueryType.AAAA, cancellationToken: token).ConfigureAwait(ConfigureAwaitOptions.None);

        //    // Pass the error we got from the lookup client (dns server).
        //    if (result.HasError)
        //        return new DNSClientResultIPAddress(result.HasError, result.ErrorMessage, $"{result.NameServer}");

        //    // Validate result because of https://github.com/BornToBeRoot/NETworkManager/issues/1934
        //    var record = result.Answers.AaaaRecords().FirstOrDefault();

        //    return record != null
        //        ? new DNSClientResultIPAddress(record.Address, $"{result.NameServer}")
        //        : new DNSClientResultIPAddress(true,
        //            $"IP address for \"{query}\" could not be resolved and the DNS server did not return an error. Try to check your DNS server with: dig @{result.NameServer.Address} {query}",
        //            $"{result.NameServer}");
        //}
        //catch (DnsResponseException ex)
        //{
        //    return new DNSClientResultIPAddress(true, ex.Message);
        //}
    }

    /// <summary>
    ///     Resolve a CNAME from a hostname or FQDN.
    /// </summary>
    /// <param name="query">Hostname or FQDN as string like "example.com".</param>
    /// <returns>CNAME of the host.</returns>
    public async Task<DNSClientResultString> ResolveCnameAsync(string query, CancellationToken token)
    {
        return await ResolveAsync(query, QueryType.CNAME, token).ConfigureAwait(ConfigureAwaitOptions.None) as DNSClientResultString;
        //if (!_isConfigured)
        //    throw new DNSClientNotConfiguredException(_notConfiguredMessage);

        //try
        //{
        //    var result = await _client.QueryAsync(query, QueryType.CNAME, cancellationToken: token).ConfigureAwait(ConfigureAwaitOptions.None);

        //    // Pass the error we got from the lookup client (dns server).
        //    if (result.HasError)
        //        return new DNSClientResultString(result.HasError, result.ErrorMessage, $"{result.NameServer}");

        //    // Validate result because of https://github.com/BornToBeRoot/NETworkManager/issues/1934
        //    var record = result.Answers.CnameRecords().FirstOrDefault();

        //    return record != null
        //        ? new DNSClientResultString(record.CanonicalName, $"{result.NameServer}")
        //        : new DNSClientResultString(true,
        //            $"CNAME for \"{query}\" could not be resolved and the DNS server did not return an error. Try to check your DNS server with: dig @{result.NameServer.Address} {query}",
        //            $"{result.NameServer}");
        //}
        //catch (DnsResponseException ex)
        //{
        //    return new DNSClientResultString(true, ex.Message);
        //}
    }

    /// <summary>
    ///     Resolve a PTR for an IP address.
    /// </summary>
    /// <param name="ipAddress">IP address of the host.</param>
    /// <returns>PTR domain name.</returns>
    public async Task<DNSClientResultString> ResolvePtrAsync(IPAddress ipAddress, CancellationToken token)
    {
        //return await ResolveAsync(query, QueryType.CNAME, token).ConfigureAwait(ConfigureAwaitOptions.None) as DNSClientResultString;

        if (!_isConfigured)
            throw new DNSClientNotConfiguredException(_notConfiguredMessage);

        try
        {
            var result = await _client.QueryReverseAsync(ipAddress, token).ConfigureAwait(ConfigureAwaitOptions.None);

            // Pass the error we got from the lookup client (dns server).
            if (result.HasError)
                return new DNSClientResultString(result.HasError, result.ErrorMessage, $"{result.NameServer}");

            // Validate result because of https://github.com/BornToBeRoot/NETworkManager/issues/1934
            var record = result.Answers.PtrRecords().FirstOrDefault();

            return record != null
                ? new DNSClientResultString(record.PtrDomainName, $"{result.NameServer}")
                : new DNSClientResultString(true,
                    $"PTR for \"{ipAddress}\" could not be resolved and the DNS server did not return an error. Try to check your DNS server with: dig @{result.NameServer.Address} -x {ipAddress}",
                    $"{result.NameServer}");
        }
        catch (DnsResponseException ex)
        {
            return new DNSClientResultString(true, ex.Message);
        }
    }
}