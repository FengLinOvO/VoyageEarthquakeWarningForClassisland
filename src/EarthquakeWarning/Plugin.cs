using System.IO;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Voyage.EarthquakeWarning.Services;
using Voyage.EarthquakeWarning.Settings;

namespace Voyage.EarthquakeWarning;

[PluginEntrance]
public sealed class Plugin : PluginBase
{
    public static Plugin? Current { get; private set; }
    public static SettingsStore? SettingsStoreInstance { get; private set; }
    public static GeoLocationService? GeoLocationServiceInstance { get; private set; }
    public static EewService? EewServiceInstance { get; private set; }

    public PluginSettings Settings { get; private set; } = null!;
    public string ConfigDirectory { get; private set; } = null!;

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        Current = this;
        ConfigDirectory = Path.Combine(PluginConfigFolder, "Voyage.EarthquakeWarning");
        Directory.CreateDirectory(ConfigDirectory);
        Settings = PluginSettings.Load(Path.Combine(ConfigDirectory, "settings.json"));

        services.AddSingleton<SettingsStore>(sp =>
        {
            var value = new SettingsStore();
            SettingsStoreInstance = value;
            return value;
        });

        services.AddSingleton<AudioService>();

        services.AddSingleton<GeoLocationService>(sp =>
        {
            var value = new GeoLocationService();
            GeoLocationServiceInstance = value;
            return value;
        });

        services.AddSingleton<WarningEngine>();

        services.AddSingleton<EewService>(sp =>
        {
            var value = ActivatorUtilities.CreateInstance<EewService>(sp);
            EewServiceInstance = value;
            return value;
        });

        services.AddSingleton<WarningWindowService>();
        services.AddHostedService(sp => sp.GetRequiredService<EewService>());

        services.AddNotificationProvider<EewNotificationProvider, GeneralSettingsPage>();
    }
}
