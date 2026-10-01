using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Controls;
using System.Globalization;
using Voyage.EarthquakeWarning.Models;
using Voyage.EarthquakeWarning.Services;

namespace Voyage.EarthquakeWarning.Settings;

public partial class GeneralSettingsPage : NotificationProviderControlBase
{
    private SettingsStore Store => Plugin.SettingsStoreInstance ?? new SettingsStore();
    private GeoLocationService Geo => Plugin.GeoLocationServiceInstance ?? new GeoLocationService();
    private EewService? Eew => Plugin.EewServiceInstance;
    private PluginSettings Settings => Plugin.Current!.Settings;

    private readonly List<Action> _simulationSync = [];

    public GeneralSettingsPage()
    {
        InitializeComponent();

        ThresholdBox.ItemsSource = new[]
        {
            "0度（将会造成过度预警，不推荐任何情况使用）",
            "1度（高楼层推荐）",
            "2度（常见场景推荐）",
            "3度",
            "4度",
            "5度"
        };

        ModeBox.ItemsSource = new[]
        {
            "ClassIsland原生横幅预警",
            "由插件绘制独立UI"
        };

        SourceBox.ItemsSource = new[]
        {
            "Voyage Project API",
            "MIUI API"
        };

        LoadUi();

        AutoLocateButton.Click += AutoLocateButton_Click;
        SaveButton.Click += (_, _) => SaveUi();
        SaveAdvancedButton.Click += (_, _) => SaveAdvancedUi();

        SourceBox.SelectionChanged += (_, _) =>
        {
            Settings.ApiSource = (ApiSource)Math.Clamp(SourceBox.SelectedIndex, 0, 1);
            TokenPanel.IsVisible = Settings.ApiSource == ApiSource.Voyage;
            Store.Save();
            Eew?.Restart();
        };

        TokenBox.LostFocus += (_, _) =>
        {
            Settings.ApiToken = TokenBox.Text?.Trim() ?? "";
            Store.Save();
        };

        AddSimulationButton.Click += (_, _) =>
        {
            Settings.Simulations.Add(new SimulationReport());
            RenderSimulations();
            Store.Save();
        };

        DefaultSimulationButton.Click += (_, _) =>
        {
            Settings.Simulations = PluginSettings.CreateDefaultSimulations();
            RenderSimulations();
            Store.Save();
        };

        RunSimulationButton.Click += async (_, _) =>
        {
            SyncSimulationInputs();
            SaveUi();
            SaveAdvancedUi();
            if (Eew is not null)
                await Eew.StartSimulationFromSettingsAsync();
        };

        OverrideBox.IsCheckedChanged += (_, _) => SaveAdvancedUi();
        SoundBox.IsCheckedChanged += (_, _) => SaveAdvancedUi();
        TopmostBox.IsCheckedChanged += (_, _) => SaveAdvancedUi();
        ForceVolumeBox.IsCheckedChanged += (_, _) => SaveAdvancedUi();
    }

    private void LoadUi()
    {
        LatitudeBox.Text = Settings.Latitude;
        LongitudeBox.Text = Settings.Longitude;
        TokenBox.Text = Settings.ApiToken;
        ThresholdBox.SelectedIndex = Math.Clamp(Settings.TriggerThreshold, 0, 5);
        ModeBox.SelectedIndex = (int)Settings.WarningMode;
        SourceBox.SelectedIndex = (int)Settings.ApiSource;
        TokenPanel.IsVisible = Settings.ApiSource == ApiSource.Voyage;
        ReleaseBox.Value = Settings.ReleaseSeconds;
        OverrideBox.IsChecked = Settings.AdvancedOverride;
        SoundBox.IsChecked = Settings.EnableAlertSound;
        TopmostBox.IsChecked = Settings.TopMost;
        ForceVolumeBox.IsChecked = Settings.ForceVolume;
        ApiConnectionText.Text = Settings.ApiConnectionTimeText;
        ApiRecentText.Text = "API最近数据：" + Settings.ApiRecentDataText;

        RenderSimulations();

        DispatcherTimer.Run(RefreshStatus, TimeSpan.FromSeconds(1));
    }

    private bool RefreshStatus()
    {
        if (!IsEffectivelyVisible) return true;

        ApiConnectionText.Text = Settings.ApiConnectionTimeText;
        ApiRecentText.Text = "API最近数据：" + Settings.ApiRecentDataText;
        return true;
    }

    private async void AutoLocateButton_Click(object? sender, RoutedEventArgs e)
    {
        AutoLocateButton.IsEnabled = false;

        try
        {
            var result = await Geo.LocateAsync();
            if (result.Success)
            {
                LatitudeBox.Text = result.Lat.ToString("0.######", CultureInfo.InvariantCulture);
                LongitudeBox.Text = result.Lng.ToString("0.######", CultureInfo.InvariantCulture);
                Settings.AutoLocationStatus = true;
                SaveUi();
            }
        }
        finally
        {
            AutoLocateButton.IsEnabled = true;
        }
    }

    private void SaveUi()
    {
        SyncSimulationInputs();

        Settings.Latitude = LatitudeBox.Text?.Trim() ?? "";
        Settings.Longitude = LongitudeBox.Text?.Trim() ?? "";
        Settings.ApiToken = TokenBox.Text?.Trim() ?? "";
        Settings.TriggerThreshold = Math.Clamp(ThresholdBox.SelectedIndex, 0, 5);
        Settings.WarningMode = (WarningMode)Math.Clamp(ModeBox.SelectedIndex, 0, 1);
        Settings.ApiSource = (ApiSource)Math.Clamp(SourceBox.SelectedIndex, 0, 1);
        Settings.ReleaseSeconds = (int)(ReleaseBox.Value ?? 15);
        Store.Save();
    }

    private void SaveAdvancedUi()
    {
        Settings.AdvancedOverride = OverrideBox.IsChecked == true;
        Settings.EnableAlertSound = SoundBox.IsChecked == true;
        Settings.TopMost = TopmostBox.IsChecked == true;
        Settings.ForceVolume = ForceVolumeBox.IsChecked == true;
        Store.Save();
    }

    private void RenderSimulations()
    {
        _simulationSync.Clear();
        SimulationPanel.Children.Clear();

        for (var i = 0; i < Settings.Simulations.Count; i++)
        {
            var index = i;
            var sim = Settings.Simulations[i];

            var panel = new StackPanel { Spacing = 6 };

            panel.Children.Add(new TextBlock { Text = $"第 {i + 1} 报", FontWeight = Avalonia.Media.FontWeight.Bold });

            TextBox Field(string label, string value, Action<string> setter)
            {
                var box = new TextBox
                {
                    Watermark = label,
                    Text = value
                };

                _simulationSync.Add(() => setter(box.Text ?? ""));

                box.LostFocus += (_, _) =>
                {
                    setter(box.Text ?? "");
                    Store.Save();
                };

                return box;
            }

            panel.Children.Add(Field(
                "发报延迟（秒，距发震）",
                sim.AlertDelaySeconds?.ToString("0.0", CultureInfo.InvariantCulture) ?? "",
                x =>
                {
                    sim.AlertDelaySeconds = double.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? Math.Max(0, v) : null;
                }));

            panel.Children.Add(Field("震中名", sim.PlaceName, x => sim.PlaceName = x));

            panel.Children.Add(Field(
                "经度",
                sim.Longitude?.ToString("0.######", CultureInfo.InvariantCulture) ?? "",
                x =>
                {
                    sim.Longitude = double.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
                }));

            panel.Children.Add(Field(
                "纬度",
                sim.Latitude?.ToString("0.######", CultureInfo.InvariantCulture) ?? "",
                x =>
                {
                    sim.Latitude = double.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
                }));

            panel.Children.Add(Field(
                "震级",
                sim.Magnitude?.ToString("0.0", CultureInfo.InvariantCulture) ?? "",
                x =>
                {
                    sim.Magnitude = double.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
                }));

            panel.Children.Add(Field(
                "预估最大烈度",
                sim.EpiIntensity?.ToString("0.0", CultureInfo.InvariantCulture) ?? "",
                x =>
                {
                    sim.EpiIntensity = double.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
                }));

            panel.Children.Add(Field(
                "震源深度（千米）",
                sim.Depth?.ToString("0.0", CultureInfo.InvariantCulture) ?? "",
                x =>
                {
                    sim.Depth = double.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
                }));

            panel.Children.Add(Field(
                "报数",
                sim.Updates?.ToString(CultureInfo.InvariantCulture) ?? "",
                x =>
                {
                    sim.Updates = int.TryParse(x, out var v) ? Math.Max(1, v) : null;
                }));

            var remove = new Button { Content = "删除本报" };

            remove.Click += (_, _) =>
            {
                Settings.Simulations.RemoveAt(index);
                RenderSimulations();
                Store.Save();
            };

            panel.Children.Add(remove);

            SimulationPanel.Children.Add(new Border
            {
                BorderBrush = Avalonia.Media.Brushes.Gray,
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(8),
                Padding = new Avalonia.Thickness(10),
                Child = panel
            });
        }
    }

    private void SyncSimulationInputs()
    {
        var sync = _simulationSync.ToArray();

        foreach (var action in sync)
            action();
    }
}
