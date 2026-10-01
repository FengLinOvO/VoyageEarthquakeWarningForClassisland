namespace Voyage.EarthquakeWarning.Services;

public static class IntensityCalculator
{
    public static double Calculate(double magnitude, double depthOrZero, double distanceKm)
    {
        if (double.IsNaN(magnitude) || double.IsNaN(depthOrZero) || double.IsNaN(distanceKm) || distanceKm > 10000)
            return 0;
        var dep = depthOrZero;
        dep = dep >= 10 ? dep : (Math.Max(dep, 0) + 10) / 2;
        const double r = 6371;
        var theta = distanceKm / r;
        var a = r - dep;
        var lineDis = Math.Sqrt(a * a + r * r - 2 * a * r * Math.Cos(theta));
        var k = 1 - 0.7 / Math.Sqrt(dep / 10);
        var hypoDis = lineDis - k * dep;
        if (hypoDis <= 0) return 0;
        var cea = 1.297 * magnitude - 4.368 * Math.Log10(hypoDis + 8) + 5.363;
        var icl = 1.363 * magnitude - 1.494 * Math.Log(hypoDis) + 2.941;
        var avg = (cea + icl) / 2;
        return Math.Round(Math.Max(avg, 0), 1, MidpointRounding.ToEven);
    }
}

public static class GeoDistance
{
    public static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371.0;
        var p1 = lat1 * Math.PI / 180;
        var p2 = lat2 * Math.PI / 180;
        var dp = (lat2 - lat1) * Math.PI / 180;
        var dl = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dp / 2) * Math.Sin(dp / 2) + Math.Cos(p1) * Math.Cos(p2) * Math.Sin(dl / 2) * Math.Sin(dl / 2);
        return r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}

public static class WaveArrivalCalculator
{
    public const double DefaultWaveVelocityKmPerSecond = 3.5;

    public static double GetCountdownSeconds(double distanceKm, DateTime shockTimeBeijing, DateTime nowBeijing)
        => distanceKm / DefaultWaveVelocityKmPerSecond - (nowBeijing - shockTimeBeijing).TotalSeconds;
}
