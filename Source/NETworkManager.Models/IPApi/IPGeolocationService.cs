﻿using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using NETworkManager.Utilities;

namespace NETworkManager.Models.IPApi;

/// <summary>
///     Class to interact with the IP geolocation API from ip-api.com.
///     Documentation is available at https://ip-api.com/docs
/// </summary>
public class IPGeolocationService : SingletonBase<IPGeolocationService>
{
    /// <summary>
    ///     Base URL fo the ip-api free endpoint.
    /// </summary>
    private const string BaseUrl = "http://ip-api.com/json/";
    //private const string Token = "3o2icb2ney6dopz4";
    //private const string BaseUrl = "https://api.2ip.io/";

    /// <summary>
    ///     Fields to be returned by the API. See documentation for more details.
    /// </summary>
    private const string Fields =
        "status,message,continent,continentCode,country,countryCode,region,regionName,city,district,zip,lat,lon,timezone,offset,currency,isp,org,as,asname,reverse,mobile,proxy,hosting,query";

    private readonly HttpClient _client = new() { BaseAddress = new Uri(BaseUrl)};

    /// <summary>
    ///     Indicates whether we have reached the rate limit.
    /// </summary>
    private bool _rateLimitIsReached;

    /// <summary>
    ///     Last time the rate limit was reached.
    /// </summary>
    private DateTime _rateLimitLastReached = DateTime.MinValue;

    /// <summary>
    ///     Remaining requests that can be processed until the rate limit window is reset.
    ///     This value is updated by Header "X-Rl". Default is 45 requests.
    /// </summary>
    private int _rateLimitRemainingRequests = 45;

    /// <summary>
    ///     Remaining time in seconds until the rate limit window resets.
    ///     This value is updated by Header "X-Ttl". Default is 60 seconds.
    /// </summary>
    private int _rateLimitRemainingTime = 60;

    /// <summary>
    ///     Gets the IP geolocation details from the API asynchronously.
    /// </summary>
    /// <param name="ipAddressOrFqdn">IP address or FQDN to get the geolocation information's from.</param>
    /// <returns>IP geolocation information's as <see cref="IPGeolocationResult" />.</returns>
    public async Task<IPGeolocationResult> GetIPGeolocationAsync(string ipAddressOrFqdn = "", CancellationToken cancellationToken = default)
    {
        if (IsInRateLimit())
            return new IPGeolocationResult(true, _rateLimitRemainingTime);

        // If the url is empty, the current IP address from which the request is made is used.
        var url = $"{BaseUrl}/{ipAddressOrFqdn}?fields={Fields}";
        //var url = $"{ipAddressOrFqdn}?token={Token}";
        try
        {
            var response = await _client.GetAsync(url, cancellationToken);

            // Check if the request was successful.
            if (response.IsSuccessStatusCode)
            {
                // Update rate limit values.
                if (!UpdateRateLimit(response.Headers))
                    return new IPGeolocationResult(true,
                        "The rate limit values couldn't be extracted from the http header. The request was probably corrupted. Try again in a few seconds.",
                        -1);

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var info = IPGeolocationInfo.FromJson(json);//JsonSerializer.Deserialize<IPGeolocationInfo>(json);

                return new IPGeolocationResult(info);
            }

            // Consider the request as failed if the status code is not successful or 429.
            if ((int)response.StatusCode != 429)
                return new IPGeolocationResult(true, response.ReasonPhrase, (int)response.StatusCode);

            // Code 429
            // We have already reached the rate limit (on the network)
            // Since we don't get any information about the remaining time, we set it to the default value.
            _rateLimitIsReached = true;
            _rateLimitRemainingTime = 60;
            _rateLimitRemainingRequests = 0;
            _rateLimitLastReached = DateTime.Now;

            return new IPGeolocationResult(true, _rateLimitRemainingTime);
        }
        catch (Exception ex)
        {
            return new IPGeolocationResult(true, ex.Message, -1);
        }
    }

    /// <summary>
    ///     Checks whether the rate limit is reached.
    /// </summary>
    /// <returns>True if the rate limit is reached, false otherwise.</returns>
    private bool IsInRateLimit()
    {
        // If the rate limit is not reached, return false.
        if (!_rateLimitIsReached)
            return false;

        // The rate limit time window is reset when the remaining time is over.
        var lastReached = _rateLimitLastReached;

        // We are still in the rate limit
        if (lastReached.AddSeconds(_rateLimitRemainingTime + 1) >= DateTime.Now)
            return true;

        // We are not in the rate limit anymore
        _rateLimitIsReached = false;

        return false;
    }

    /// <summary>
    ///     Updates the rate limit values.
    /// </summary>
    /// <param name="headers">Headers from the response.</param>
    /// <returns>True if the update was successful, false otherwise.</returns>
    private bool UpdateRateLimit(HttpHeaders headers)
    {
        // Parse header data
        if (!headers.TryGetValues("X-Rl", out var xRl))
            return false;

        if (!int.TryParse(xRl.ToArray()[0], out var remainingRequests))
            return false;

        if (!headers.TryGetValues("X-Ttl", out var xTtl))
            return false;

        if (!int.TryParse(xTtl.ToArray()[0], out var remainingTime))
            return false;

        _rateLimitRemainingTime = remainingTime;
        _rateLimitRemainingRequests = remainingRequests;

        // Only allow 40 requests... to prevent a 429 error if other
        // devices or tools on the network (e.g. another NETworkManager
        // instance) doing requests against ip-api.com.
        if (_rateLimitRemainingRequests >= 5)
            return true;

        // We have reached the rate limit (on the network)
        // Disable the service and store the time when the rate limit was reached.
        _rateLimitIsReached = true;
        _rateLimitLastReached = DateTime.Now;

        return true;
    }
}