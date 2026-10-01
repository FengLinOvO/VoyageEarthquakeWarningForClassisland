using System.Text.Json.Serialization;

namespace Voyage.EarthquakeWarning.Models;

public sealed class EewEnvelope
{
    [JsonPropertyName("Data")]
    public EewData? Data { get; set; }

    [JsonPropertyName("md5")]
    public string? Md5 { get; set; }
}

public sealed class EewData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("eventId")]
    public string EventId { get; set; } = "";

    [JsonPropertyName("shockTime")]
    public string ShockTime { get; set; } = "";

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("placeName")]
    public string PlaceName { get; set; } = "";

    [JsonPropertyName("magnitude")]
    public string Magnitude { get; set; } = "0";

    [JsonPropertyName("epiIntensity")]
    public double EpiIntensity { get; set; }

    [JsonPropertyName("depth")]
    public double? Depth { get; set; }

    [JsonPropertyName("updates")]
    public int Updates { get; set; }
}

public sealed class MiuiEnvelope
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("desc")]
    public string? Desc { get; set; }

    [JsonPropertyName("data")]
    public List<MiuiRecord>? Data { get; set; }
}

public sealed class MiuiRecord
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("eventId")]
    public long EventId { get; set; }

    [JsonPropertyName("update")]
    public int Update { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("startAt")]
    public long StartAt { get; set; }

    [JsonPropertyName("updateAt")]
    public long UpdateAt { get; set; }

    [JsonPropertyName("magnitude")]
    public double Magnitude { get; set; }

    [JsonPropertyName("depth")]
    public double Depth { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("epicenter")]
    public string? Epicenter { get; set; }

    [JsonPropertyName("signature")]
    public string? Signature { get; set; }
}
