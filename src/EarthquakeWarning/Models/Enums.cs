namespace Voyage.EarthquakeWarning.Models;

public enum WarningMode
{
    NativeBanner = 0,
    IndependentUi = 1
}

public enum ApiSource
{
    Voyage = 0,
    Miui = 1
}

public enum WarningTier
{
    BlueNoFeel = 0,
    BlueFeel = 1,
    Yellow = 2,
    Orange = 3,
    Red = 4
}
