using DnsClient;
using DnsClient.Protocol;
using Microsoft.Win32;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Protection.PlayReady;

namespace NETworkManager.Utilities;

public class DNSClient : SingletonBase<DNSClient>
{
    /// <summary>
    ///     Error message which is returned when the DNS client is not configured.
    /// </summary>
    private const string _notConfiguredMessage = "DNS client is not configured. Call Configure() first.";

    private string[] _systemSearchDomains = [];

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
        _systemSearchDomains = GetSystemSearchDomains();
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
    public async Task<DNSClientResult> ResolveAsync(string requestedHostName, QueryType queryType = QueryType.A, CancellationToken token = default)
    {
        if (!_isConfigured)
            throw new DNSClientNotConfiguredException(_notConfiguredMessage);
        var queryNames = requestedHostName.Contains('.') ? [requestedHostName] : ResolveWithSearchDomainsAsync(requestedHostName);
        
        try
        {
            IDnsQueryResponse queryResult = default;
            foreach (var query in queryNames)
            {
                queryResult = await _client.QueryAsync(query, queryType, cancellationToken: token).ConfigureAwait(ConfigureAwaitOptions.None);
                if (!queryResult.HasError)
                    break;
            } 
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
                case QueryType.SRV:
                    break;
                case QueryType.MX:
                    break;
                case QueryType.TXT:
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
                case QueryType.RP:
                    break;
                case QueryType.AFSDB:
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
                    $"Result \"{requestedHostName}\" could not be resolved and the DNS server did not return an error. Try to check your DNS server with: dig @{queryResult.NameServer.Address} {requestedHostName}",
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
    }

    /// <summary>
    ///     Resolve an IPv6 address from a hostname or FQDN.
    /// </summary>
    /// <param name="query">Hostname or FQDN as string like "example.com".</param>
    /// <returns><see cref="IPAddress" /> of the host.</returns>
    public async Task<DNSClientResultIPAddress> ResolveAaaaAsync(string query, CancellationToken token)
    {
        return await ResolveAsync(query, QueryType.AAAA, token).ConfigureAwait(ConfigureAwaitOptions.None) as DNSClientResultIPAddress;
    }

    /// <summary>
    ///     Resolve a CNAME from a hostname or FQDN.
    /// </summary>
    /// <param name="query">Hostname or FQDN as string like "example.com".</param>
    /// <returns>CNAME of the host.</returns>
    public async Task<DNSClientResultString> ResolveCnameAsync(string query, CancellationToken token)
    {
        return await ResolveAsync(query, QueryType.CNAME, token).ConfigureAwait(ConfigureAwaitOptions.None) as DNSClientResultString;
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

    public IEnumerable<string> ResolveWithSearchDomainsAsync(string hostName)
    {
        // Перебираем домены поиска
        foreach (var domain in _systemSearchDomains)
        {
            string fullName = $"{hostName}.{domain}";
            yield return fullName;            
        }
    }

    public string[] GetLinuxSearchDomains()
    {
        const string resolvConf = "/etc/resolv.conf";
        var domains = new HashSet<string>();

        if (!File.Exists(resolvConf))
            return Array.Empty<string>();

        char[]? buffer = ArrayPool<char>.Shared.Rent(4096);
        try
        {
            using var reader = new StreamReader(resolvConf);
            int bytesRead;
            while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                var span = buffer.AsSpan(0, bytesRead);
                ProcessSpan(span, domains);
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }

        return SetToArray(domains);
    }

    private static void ProcessSpan(ReadOnlySpan<char> data, HashSet<string> domains)
    {
        int lineStart = 0;
        for (int i = 0; i <= data.Length; i++)
        {
            bool isNewline = i == data.Length || data[i] == '\n';

            if (isNewline)
            {
                if (i > lineStart)
                {
                    var line = data.Slice(lineStart, i - lineStart).Trim();
                    ProcessLine(line, domains);
                }
                lineStart = i + 1;
            }
        }
    }

    private static void ProcessLine(ReadOnlySpan<char> line, HashSet<string> domains)
    {
        if (line.IsEmpty) return;

        // Проверяем директивы search/domain
        if (line.StartsWith("search".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("domain".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            // Пропускаем ключевое слово
            var content = line.Slice(line.IndexOf(' ') + 1);
            ParseDomains(content, domains);
        }
    }

    private static void ParseDomains(ReadOnlySpan<char> content, HashSet<string> domains)
    {
        int start = 0;
        var span = content.Trim();

        for (int i = 0; i <= span.Length; i++)
        {
            bool isDelimiter = i == span.Length || char.IsWhiteSpace(span[i]);

            if (isDelimiter && i > start)
            {
                var domain = span.Slice(start, i - start).Trim();
                if (!domain.IsEmpty)
                {
                    domains.Add(domain.ToString());
                }
                start = i + 1;
            }
        }
    }

    private static string[] SetToArray(HashSet<string> set)
    {
        var array = new string[set.Count];
        set.CopyTo(array);
        return array;
    }
    public string[] GetWindowsSearchDomains()
    {
        var domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Глобальные настройки
        using (var globalKey = Registry.LocalMachine.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters"))
        {
            if (globalKey != null)
            {
                AddDomains(globalKey.GetValue("SearchList"), domains);
                AddDomain(globalKey.GetValue("Domain"), domains);
            }
        }

        // Настройки адаптеров
        using (var interfacesKey = Registry.LocalMachine.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces"))
        {
            if (interfacesKey != null)
            {
                foreach (var interfaceId in interfacesKey.GetSubKeyNames())
                {
                    using (var interfaceKey = interfacesKey.OpenSubKey(interfaceId))
                    {
                        if (interfaceKey != null)
                        {
                            AddDomains(interfaceKey.GetValue("SearchList"), domains);
                            AddDomain(interfaceKey.GetValue("Domain"), domains);
                        }
                    }
                }
            }
        }

        return SetToArray(domains);
    }

    private static void AddDomain(object? value, HashSet<string> domains)
    {
        if (value is string domain && !domain.AsSpan().IsEmpty)
        {
            domains.Add(domain);
        }
    }

    private static void AddDomains(object? value, HashSet<string> domains)
    {
        if (value is not string domainsStr) return;

        var span = domainsStr.AsSpan().Trim();
        if (span.IsEmpty) return;

        int start = 0;
        for (int i = 0; i <= span.Length; i++)
        {
            bool isEnd = i == span.Length;
            bool isDelimiter = !isEnd && span[i] == ',';

            if (isEnd || isDelimiter)
            {
                if (i > start)
                {
                    var domainSpan = span.Slice(start, i - start).Trim();
                    if (!domainSpan.IsEmpty)
                    {
                        domains.Add(domainSpan.ToString());
                    }
                }
                start = i + 1;
            }
        }
    }


    public string[] GetSystemSearchDomains()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return GetWindowsSearchDomains();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
           RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return GetLinuxSearchDomains();
        }

        return Array.Empty<string>();
    }
}