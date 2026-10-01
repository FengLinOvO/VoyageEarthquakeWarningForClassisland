using System.Text.Json;
using Voyage.EarthquakeWarning.Models;

namespace Voyage.EarthquakeWarning.Settings;

public sealed class PluginSettings : ObservableObject
{
    private string _latitude = "36.06";
    private string _longitude = "103.83";
    private string _apiToken = "";
    private ApiSource _apiSource = ApiSource.Miui;
    private int _triggerThreshold = 2;
    private WarningMode _warningMode = WarningMode.IndependentUi;
    private int _releaseSeconds = 15;
    private bool _advancedOverride;
    private bool _enableAlertSound = true;
    private bool _topMost = true;
    private bool _forceVolume;
    private bool _autoLocationStatus;
    private string _apiConnectionTimeText = "未连接";
    private string _apiRecentDataText = "暂无数据";

    public string Latitude { get => _latitude; set => SetProperty(ref _latitude, value); }
    public string Longitude { get => _longitude; set => SetProperty(ref _longitude, value); }
    public string ApiToken { get => _apiToken; set => SetProperty(ref _apiToken, value); }
    public ApiSource ApiSource { get => _apiSource; set => SetProperty(ref _apiSource, value); }
    public int TriggerThreshold { get => _triggerThreshold; set => SetProperty(ref _triggerThreshold, value); }
    public WarningMode WarningMode { get => _warningMode; set => SetProperty(ref _warningMode, value); }
    public int ReleaseSeconds { get => _releaseSeconds; set => SetProperty(ref _releaseSeconds, Math.Clamp(value, 0, 300)); }
    public bool AdvancedOverride { get => _advancedOverride; set => SetProperty(ref _advancedOverride, value); }
    public bool EnableAlertSound { get => _enableAlertSound; set => SetProperty(ref _enableAlertSound, value); }
    public bool TopMost { get => _topMost; set => SetProperty(ref _topMost, value); }
    public bool ForceVolume { get => _forceVolume; set => SetProperty(ref _forceVolume, value); }
    public bool AutoLocationStatus { get => _autoLocationStatus; set => SetProperty(ref _autoLocationStatus, value); }
    public string ApiConnectionTimeText { get => _apiConnectionTimeText; set => SetProperty(ref _apiConnectionTimeText, value); }
    public string ApiRecentDataText { get => _apiRecentDataText; set => SetProperty(ref _apiRecentDataText, value); }

    public List<SimulationReport> Simulations { get; set; } =
        CreateDefaultSimulations();

    public static List<SimulationReport> CreateDefaultSimulations() =>
    [
        new SimulationReport
        {
            AlertDelaySeconds = 4.9,
            Latitude = 35.75,
            Longitude = 102.80,
            Magnitude = 5.9,
            EpiIntensity = 7.9,
            Depth = 10,
            Updates = 1
        },
        new SimulationReport
        {
            AlertDelaySeconds = 13.5,
            Latitude = 35.74,
            Longitude = 102.81,
            Magnitude = 6.3,
            EpiIntensity = 8.3,
            Depth = 10,
            Updates = 2
        },
        new SimulationReport
        {
            AlertDelaySeconds = 14.0,
            Latitude = 35.74,
            Longitude = 102.81,
            Magnitude = 6.8,
            EpiIntensity = 8.8,
            Depth = 10,
            Updates = 3
        },
        new SimulationReport
        {
            AlertDelaySeconds = 37.1,
            Latitude = 35.74,
            Longitude = 102.82,
            Magnitude = 6.0,
            EpiIntensity = 7.9,
            Depth = 10,
            Updates = 4
        }
    ];

    public static PluginSettings Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<PluginSettings>(json, JsonOptions()) ?? new PluginSettings();
            }
        }
        catch { }
        return new PluginSettings();
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions()));
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
}
