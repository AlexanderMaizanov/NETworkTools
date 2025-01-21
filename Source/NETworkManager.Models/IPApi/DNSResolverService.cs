using System;
using System.Text.Json;
using System.Net.Http;
using System.Threading.Tasks;
using NETworkManager.Utilities;
using System.Threading;

namespace NETworkManager.Models.IPApi;

/// <summary>
///     Class to interact with the DNS resolver API from ip-api.com.
///     Documentation is available at https://ip-api.com/docs
/// </summary>
public class DNSResolverService : SingletonBase<DNSResolverService>
{
    /// <summary>
    ///     Base URL fo the edns.ip-api.
    /// </summary>
    private const string BaseUrl = "https://edns.ip-api.com/json";

    private readonly HttpClient _client = new();

    /// <summary>
    ///     Gets the IP DNS details from the API asynchronously.
    /// </summary>
    /// <returns>IP DNS information's as <see cref="DNSResolverResult" />.</returns>
    public async Task<DNSResolverResult> GetDNSResolverAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.GetAsync(BaseUrl, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.None);

            if (response.IsSuccessStatusCode)
            {
                var jsonStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(ConfigureAwaitOptions.None);
                var info = await JsonSerializer.DeserializeAsync<DNSResolverDeserializationInfo>(jsonStream, cancellationToken: cancellationToken).ConfigureAwait(false);
                

                return new DNSResolverResult(DNSResolverInfo.Parse(info));
            }

            return new DNSResolverResult(true, response.ReasonPhrase, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            return new DNSResolverResult(true, ex.Message);
        }
    }
}