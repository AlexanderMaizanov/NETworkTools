using System;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace NETworkManager.Models.IPApi;

/// <summary>
///     Class contains the IP geolocation information.
/// </summary>
/// <example>
///     To parse this JSON data, add NuGet 'System.Text.Json' then do:
///
///    using NETworkManager.Models.IPApi;
///
///    var ipGeolocationInfo = IpGeolocationInfo.FromJson(jsonString);
/// </example>
#nullable enable
#pragma warning disable CS8618
#pragma warning disable CS8601
#pragma warning disable CS8603

public partial class IPGeolocationInfo
{
    [JsonPropertyName("status")]
    public string Status { get; set; }

    [JsonPropertyName("continent")]
    public string Continent { get; set; }

    [JsonPropertyName("continentCode")]
    public string ContinentCode { get; set; }

    [JsonPropertyName("country")]
    public string Country { get; set; }

    [JsonPropertyName("countryCode")]
    public string CountryCode { get; set; }

    [JsonPropertyName("region")]
    public string Region { get; set; }

    [JsonPropertyName("regionName")]
    public string RegionName { get; set; }

    [JsonPropertyName("city")]
    public string City { get; set; }

    [JsonPropertyName("district")]
    public string District { get; set; }

    [JsonPropertyName("zip")]
    public string Zip { get; set; }

    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lon")]
    public double Lon { get; set; }

    [JsonPropertyName("timezone")]
    public string Timezone { get; set; }

    [JsonPropertyName("offset")]
    public long Offset { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; }

    [JsonPropertyName("isp")]
    public string Isp { get; set; }

    [JsonPropertyName("org")]
    public string Org { get; set; }

    [JsonPropertyName("as")]
    public string As { get; set; }

    [JsonPropertyName("asname")]
    public string Asname { get; set; }

    [JsonPropertyName("reverse")]
    public string Reverse { get; set; }

    [JsonPropertyName("mobile")]
    public bool Mobile { get; set; }

    [JsonPropertyName("proxy")]
    public bool Proxy { get; set; }

    [JsonPropertyName("hosting")]
    public bool Hosting { get; set; }

    [JsonPropertyName("query")]
    public string Query { get; set; }
}

public partial class IPGeolocationInfo
{
    public static IPGeolocationInfo FromJson(string json) => JsonSerializer.Deserialize<IPGeolocationInfo>(json, Converter.Settings);
}

public static class Serialize
{
    public static string ToJson(this IPGeolocationInfo self) => JsonSerializer.Serialize(self, Converter.Settings);
}

internal static class Converter
{
    public static readonly JsonSerializerOptions Settings = new(JsonSerializerDefaults.General)
    {
        Converters =
            {
                new DateOnlyConverter(),
                new TimeOnlyConverter(),
                IsoDateTimeOffsetConverter.Singleton
            },
    };
}

public class DateOnlyConverter : JsonConverter<DateOnly>
{
    private readonly string serializationFormat;
    public DateOnlyConverter() : this(null) { }

    public DateOnlyConverter(string? serializationFormat)
    {
        this.serializationFormat = serializationFormat ?? "yyyy-MM-dd";
    }

    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return DateOnly.Parse(value!);
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString(serializationFormat));
}

public class TimeOnlyConverter : JsonConverter<TimeOnly>
{
    private readonly string serializationFormat;

    public TimeOnlyConverter() : this(null) { }

    public TimeOnlyConverter(string? serializationFormat)
    {
        this.serializationFormat = serializationFormat ?? "HH:mm:ss.fff";
    }

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return TimeOnly.Parse(value!);
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString(serializationFormat));
}

internal class IsoDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override bool CanConvert(Type t) => t == typeof(DateTimeOffset);

    private const string DefaultDateTimeFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK";

    private DateTimeStyles _dateTimeStyles = DateTimeStyles.RoundtripKind;
    private string? _dateTimeFormat;
    private CultureInfo? _culture;

    public DateTimeStyles DateTimeStyles
    {
        get => _dateTimeStyles;
        set => _dateTimeStyles = value;
    }

    public string? DateTimeFormat
    {
        get => _dateTimeFormat ?? string.Empty;
        set => _dateTimeFormat = (string.IsNullOrEmpty(value)) ? null : value;
    }

    public CultureInfo Culture
    {
        get => _culture ?? CultureInfo.CurrentCulture;
        set => _culture = value;
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        string text;


        if ((_dateTimeStyles & DateTimeStyles.AdjustToUniversal) == DateTimeStyles.AdjustToUniversal
                || (_dateTimeStyles & DateTimeStyles.AssumeUniversal) == DateTimeStyles.AssumeUniversal)
        {
            value = value.ToUniversalTime();
        }

        text = value.ToString(_dateTimeFormat ?? DefaultDateTimeFormat, Culture);

        writer.WriteStringValue(text);
    }

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? dateText = reader.GetString();

        if (string.IsNullOrEmpty(dateText) == false)
        {
            if (!string.IsNullOrEmpty(_dateTimeFormat))
            {
                return DateTimeOffset.ParseExact(dateText, _dateTimeFormat, Culture, _dateTimeStyles);
            }
            else
            {
                return DateTimeOffset.Parse(dateText, Culture, _dateTimeStyles);
            }
        }
        else
        {
            return default(DateTimeOffset);
        }
    }


    public static readonly IsoDateTimeOffsetConverter Singleton = new IsoDateTimeOffsetConverter();
}
#pragma warning restore CS8618
#pragma warning restore CS8601
#pragma warning restore CS8603

//public class IPGeolocationInfo
//{
//    /// <summary>
//    ///     Status of the IP geolocation information retrieval.
//    /// </summary>
//    [JsonPropertyName("status")]
//    public string Status { get; set; }

//    /// <summary>
//    ///     Continent where the IP address is located.
//    /// </summary>
//    public string Continent { get; set; }

//    /// <summary>
//    ///     Continent code where the IP address is located.
//    /// </summary>
//    public string ContinentCode { get; set; }

//    /// <summary>
//    ///     Country where the IP address is located.
//    /// </summary>
//    public string Country { get; set; }

//    /// <summary>
//    ///     Country code where the IP address is located.
//    /// </summary>
//    public string CountryCode { get; set; }

//    /// <summary>
//    ///     Region where the IP address is located.
//    /// </summary>
//    public string Region { get; set; }

//    /// <summary>
//    ///     Region name where the IP address is located.
//    /// </summary>
//    public string RegionName { get; set; }

//    /// <summary>
//    ///     City where the IP address is located.
//    /// </summary>
//    public string City { get; set; }

//    /// <summary>
//    ///     District where the IP address is located.
//    /// </summary>
//    public string District { get; set; }

//    /// <summary>
//    ///     Zip code of the location where the IP address is located.
//    /// </summary>
//    public string Zip { get; set; }

//    /// <summary>
//    ///     Latitude of the location where the IP address is located.
//    /// </summary>
//    public double Lat { get; set; }

//    /// <summary>
//    ///     Longitude of the location where the IP address is located.
//    /// </summary>
//    public double Lon { get; set; }

//    /// <summary>
//    ///     Timezone of the location where the IP address is located.
//    /// </summary>
//    public string Timezone { get; set; }

//    /// <summary>
//    ///     Offset from UTC in seconds for the location where the IP address is located.
//    /// </summary>
//    public int Offset { get; set; }

//    /// <summary>
//    ///     Currency used in the country where the IP address is located.
//    /// </summary>
//    public string Currency { get; set; }

//    /// <summary>
//    ///     Internet Service Provider (ISP) of the IP address.
//    /// </summary>
//    public string Isp { get; set; }

//    /// <summary>
//    ///     Organization associated with the IP address.
//    /// </summary>
//    public string Org { get; set; }

//    /// <summary>
//    ///     Autonomous System (AS) number and name associated with the IP address.
//    /// </summary>
//    public string As { get; set; }

//    /// <summary>
//    ///     Name of the Autonomous System (AS) associated with the IP address.
//    /// </summary>
//    public string Asname { get; set; }

//    /// <summary>
//    ///     Reverse DNS hostname associated with the IP address.
//    /// </summary>
//    public string Reverse { get; set; }

//    /// <summary>
//    ///     Indicates whether the IP address is associated with a mobile network.
//    /// </summary>
//    public bool Mobile { get; set; }

//    /// <summary>
//    ///     Indicates whether the IP address is a proxy server.
//    /// </summary>
//    public bool Proxy { get; set; }

//    /// <summary>
//    ///     Indicates whether the IP address is associated with hosting services.
//    /// </summary>
//    public bool Hosting { get; set; }

//    /// <summary>
//    ///     IP address used for the query.
//    /// </summary>
//    public string Query { get; set; }
//}