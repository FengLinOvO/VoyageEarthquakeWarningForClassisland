namespace Voyage.EarthquakeWarning.Models;

public sealed class WarningState : ObservableObject
{
    private double _countdown;
    private string _countdownText = "--";
    private double _localIntensity;
    private string _localIntensityText = "0.0";
    private string _distanceText = "--";
    private string _tierText = "蓝色地震预警（无感）";
    private string _sensation = "无感地震，请勿惊慌";
    private string _background = "#3764FF";
    private string _foreground = "#FFFFFF";
    private string _iconPath = "";
    private bool _arrived;

    public string EventId { get; init; } = "";
    public string PlaceName { get; init; } = "";
    public string ShockTimeText { get; init; } = "";
    public string MagnitudeText { get; init; } = "";
    public double DepthKm { get; init; }
    public double EpicenterIntensity { get; init; }
    public int Updates { get; init; }

    public double CountdownSeconds
    {
        get => _countdown;
        set => SetProperty(ref _countdown, value);
    }

    public string CountdownText
    {
        get => _countdownText;
        set => SetProperty(ref _countdownText, value);
    }

    public double LocalIntensity
    {
        get => _localIntensity;
        set => SetProperty(ref _localIntensity, value);
    }

    public string LocalIntensityText
    {
        get => _localIntensityText;
        set => SetProperty(ref _localIntensityText, value);
    }

    public string DistanceText
    {
        get => _distanceText;
        set => SetProperty(ref _distanceText, value);
    }

    public string TierText
    {
        get => _tierText;
        set => SetProperty(ref _tierText, value);
    }

    public string Sensation
    {
        get => _sensation;
        set => SetProperty(ref _sensation, value);
    }

    public string BackgroundHex
    {
        get => _background;
        set => SetProperty(ref _background, value);
    }

    public string ForegroundHex
    {
        get => _foreground;
        set => SetProperty(ref _foreground, value);
    }

    public string IconPath
    {
        get => _iconPath;
        set => SetProperty(ref _iconPath, value);
    }

    public bool Arrived
    {
        get => _arrived;
        set => SetProperty(ref _arrived, value);
    }

    public WarningTier Tier { get; set; }
    public DateTime ArrivalTimeBeijing { get; set; }
    public DateTime ReceivedAtBeijing { get; set; }
    public DateTime ShockTimeBeijing { get; set; }

    public void UpdateCountdown(DateTime nowBeijing)
    {
        CountdownSeconds = (ArrivalTimeBeijing - nowBeijing).TotalSeconds;
        if (CountdownSeconds <= 0)
        {
            Arrived = true;
            CountdownText = "地震横波已到达";
            return;
        }

        Arrived = false;
        CountdownText = $"{Math.Ceiling(CountdownSeconds):0}s";
    }
}
