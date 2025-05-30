﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NETworkManager.Utilities;

namespace NETworkManager.Models.Network;

/// <summary>
///     Helper class to interact with host ranges.
///     E.g. Parse inputs, resolve hostnames and ip ranges.
/// </summary>
public static class HostRangeHelper
{
    /// <summary>
    ///     Create a list of hosts from a string input like "10.0.0.1; example.com; 10.0.0.0/24"
    /// </summary>
    /// <param name="hosts">Hosts like "10.0.0.1; example.com; 10.0.0.0/24"</param>
    /// <returns>List of hosts.</returns>
    public static IEnumerable<string> CreateListFromInput(string hosts)
    {
        return hosts.Replace(" ", "").Split(';')
            .Where(x => !string.IsNullOrEmpty(x))
            .Select(x => x.Trim())
            .ToArray();
    }

    public static async Task<(List<(IPAddress ipAddress, string hostname)> hosts, List<string> hostnamesNotResolved)> ResolveAsync(
        IEnumerable<string> hosts, bool dnsResolveHostnamePreferIPv4, CancellationToken cancellationToken)
    {
        var hostsBag = new ConcurrentBag<(IPAddress ipAddress, string hostname)>();
        var hostnamesNotResovledBag = new ConcurrentBag<string>();

        await Parallel.ForEachAsync(hosts, cancellationToken, async (host, cancellationToken) =>
        {
            if(cancellationToken.IsCancellationRequested) return;
            switch (host)
            {
                // 192.168.0.1
                case var _ when Regex.IsMatch(host, RegexHelper.IPv4AddressRegex):
                // 2001:db8:85a3::8a2e:370:7334
                case var _ when Regex.IsMatch(host, RegexHelper.IPv6AddressRegex):
                    hostsBag.Add((IPAddress.Parse(host), string.Empty));

                    break;

                // 192.168.0.0/24
                case var _ when Regex.IsMatch(host, RegexHelper.IPv4AddressCidrRegex):
                // 192.168.0.0/255.255.255.0
                case var _ when Regex.IsMatch(host, RegexHelper.IPv4AddressSubnetmaskRegex):
                    var network = IPNetwork2.Parse(host);

                    Parallel.For(IPv4Address.ToInt32(network.Network), IPv4Address.ToInt32(network.Broadcast) + 1,
                        (i, state) =>
                        {
                            if (cancellationToken.IsCancellationRequested)
                                state.Break();

                            hostsBag.Add((IPv4Address.FromInt32(i), string.Empty));
                        });

                    break;

                // 192.168.0.0 - 192.168.0.100
                case var _ when Regex.IsMatch(host, RegexHelper.IPv4AddressRangeRegex):
                    var range = host.Split('-');

                    for(int i = IPv4Address.ToInt32(IPAddress.Parse(range[0])); 
                        i <= IPv4Address.ToInt32(IPAddress.Parse(range[1])) + 1; i++) 
                        {
                            hostsBag.Add((IPv4Address.FromInt32(i), string.Empty));                            
                        }

                    break;

                // 192.168.[50-100].1
                case var _ when Regex.IsMatch(host, RegexHelper.IPv4AddressSpecialRangeRegex):
                    var octets = host.Split('.');

                    var list = new List<ConcurrentBag<int>>();

                    // Go through each octet...
                    foreach (var octet in octets)
                    {
                        var innerList = new ConcurrentBag<int>();

                        // Create a range for each octet
                        if (Regex.IsMatch(octet, RegexHelper.SpecialRangeRegex))
                            foreach (var numberOrRange in octet[1..^1].Split(','))
                                // 50-100
                                if (numberOrRange.Contains('-'))
                                {
                                    var rangeNumbers = numberOrRange.Split('-');

                                    Parallel.For(int.Parse(rangeNumbers[0]), int.Parse(rangeNumbers[1]) + 1,
                                        (i, state) =>
                                        {
                                            if (cancellationToken.IsCancellationRequested)
                                                state.Break();

                                            innerList.Add(i);
                                        });
                                } // 200
                                else
                                {
                                    innerList.Add(int.Parse(numberOrRange));
                                }
                        else
                            innerList.Add(int.Parse(octet));

                        list.Add(innerList);
                    }

                    // Build the new ipv4
                    Parallel.ForEach(list[0], new ParallelOptions { CancellationToken = cancellationToken },
                        i =>
                        {
                            Parallel.ForEach(list[1], new ParallelOptions { CancellationToken = cancellationToken },
                                j =>
                                {
                                    Parallel.ForEach(list[2],
                                        new ParallelOptions { CancellationToken = cancellationToken },
                                        k =>
                                        {
                                            Parallel.ForEach(list[3],
                                                new ParallelOptions { CancellationToken = cancellationToken },
                                                h =>
                                                {
                                                    hostsBag.Add((IPAddress.Parse($"{i}.{j}.{k}.{h}"), string.Empty));
                                                });
                                        });
                                });
                        });

                    break;

                // example.com
                case var _ when Regex.IsMatch(host, RegexHelper.HostnameOrDomainRegex):
                    var dnsResolver = await
                           DNSClientHelper.ResolveAorAaaaAsync(host, dnsResolveHostnamePreferIPv4, cancellationToken).ConfigureAwait(false);
                    
                        if (dnsResolver.HasError)
                        {
                            hostnamesNotResovledBag.Add(host);
                            break;
                        }
                        hostsBag.Add((IPAddress.Parse(dnsResolver.Value.ToString()), host));                        
                    break;

                // example.com/24 or example.com/255.255.255.128
                case var _ when Regex.IsMatch(host, RegexHelper.HostnameOrDomainWithCidrRegex):
                case var _ when Regex.IsMatch(host, RegexHelper.HostnameOrDomainWithSubnetmaskRegex):
                    var hostAndSubnet = host.Split('/');

                    // Only support IPv4
                    dnsResolver = await DNSClientHelper.ResolveAorAaaaAsync(hostAndSubnet[0], true, cancellationToken);
                        if (!dnsResolver.HasError)
                        {
                            // Only support IPv4 for ranges for now
                            if (dnsResolver.Value.AddressFamily == AddressFamily.InterNetwork)
                            {
                                network = IPNetwork2.Parse(
                                    $"{dnsResolver.Value}/{hostAndSubnet[1]}");

                                Parallel.For(IPv4Address.ToInt32(network.Network),
                                    IPv4Address.ToInt32(network.Broadcast) + 1, (i, state) =>
                                    {
                                        if (cancellationToken.IsCancellationRequested)
                                            state.Break();
                                        hostsBag.Add((IPv4Address.FromInt32(i), string.Empty));
                                    });
                            }
                            else
                            {
                                hostnamesNotResovledBag.Add(hostAndSubnet[0]);
                            }
                        }
                        else
                        {
                            hostnamesNotResovledBag.Add(hostAndSubnet[0]);
                        }
                break;
            }
        });

        // Sort list and return
        IPAddressComparer comparer = new();

        return ([.. hostsBag.OrderBy(x => x.ipAddress, comparer)], [.. hostnamesNotResovledBag]);
    }
}