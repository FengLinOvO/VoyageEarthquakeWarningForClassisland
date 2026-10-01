namespace Voyage.EarthquakeWarning.Models;

public sealed class SimulationReport : ObservableObject
{
    private string _placeName = "";
    private double? _longitude;
    private double? _latitude;
    private double? _magnitude;
    private double? _epiIntensity;
    private double? _depth;
    private int? _updates;
    private double? _alertDelaySeconds;

    public string PlaceName { get => _placeName; set => SetProperty(ref _placeName, value); }
    public double? Longitude { get => _longitude; set => SetProperty(ref _longitude, value); }
    public double? Latitude { get => _latitude; set => SetProperty(ref _latitude, value); }
    public double? Magnitude { get => _magnitude; set => SetProperty(ref _magnitude, value); }
    public double? EpiIntensity { get => _epiIntensity; set => SetProperty(ref _epiIntensity, value); }
    public double? Depth { get => _depth; set => SetProperty(ref _depth, value); }
    public int? Updates { get => _updates; set => SetProperty(ref _updates, value); }
    public double? AlertDelaySeconds { get => _alertDelaySeconds; set => SetProperty(ref _alertDelaySeconds, value); }
}
