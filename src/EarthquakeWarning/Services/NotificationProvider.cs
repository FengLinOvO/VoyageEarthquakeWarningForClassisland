using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Services.NotificationProviders;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Models.Notification;
using ClassIsland.Shared.Models.Notification;
using Voyage.EarthquakeWarning.Models;

namespace Voyage.EarthquakeWarning.Services;

[NotificationProviderInfo("D39B2CB3-81A1-44E5-8CE8-7D8F6415E32A", "地震预警", "在收到预警信息时，根据用户设置弹出地震预警")]
public sealed class EewNotificationProvider : NotificationProviderBase
{
    public static EewNotificationProvider? Instance { get; private set; }

    private const double BannerSidePadding = 32;

    private readonly object _gate = new();
    private readonly List<TextBlock> _plainTexts = [];
    private readonly List<TextBlock> _accentTexts = [];

    private TextBlock? _shockTimeText;
    private TextBlock? _placeText;
    private TextBlock? _magnitudeText;
    private TextBlock? _intensityText;
    private TextBlock? _countdownText;

    public EewNotificationProvider()
    {
        Instance = this;
    }

    public static void Push(WarningState state, bool firstReport, bool overrideSettings)
    {
        Instance?.PushInternal(state, firstReport, overrideSettings);
    }

    public static void UpdateReport(WarningState state)
    {
        Instance?.UpdateInternal(state);
    }

    public static void UpdateCountdown(WarningState state)
    {
        Instance?.UpdateInternal(state);
    }

    private void UpdateInternal(WarningState state)
    {
        TextBlock[] plainTexts;
        TextBlock[] accentTexts;
        TextBlock? shockTimeText;
        TextBlock? placeText;
        TextBlock? magnitudeText;
        TextBlock? intensityText;
        TextBlock? countdownText;

        lock (_gate)
        {
            plainTexts = [.. _plainTexts];
            accentTexts = [.. _accentTexts];
            shockTimeText = _shockTimeText;
            placeText = _placeText;
            magnitudeText = _magnitudeText;
            intensityText = _intensityText;
            countdownText = _countdownText;
        }

        var foregroundHex = state.ForegroundHex;
        var accentHex = TierAccentHex(state.Tier);
        var shockTime = state.ShockTimeText;
        var place = state.PlaceName;
        var magnitude = state.MagnitudeText;
        var intensity = state.LocalIntensityText;
        var countdown = state.Arrived ? "地震横波已到达" : $"倒计时：{state.CountdownText}";

        Dispatcher.UIThread.Post(() =>
        {
            var foreground = new SolidColorBrush(Color.Parse(foregroundHex));
            var accent = new SolidColorBrush(Color.Parse(accentHex));

            foreach (var text in plainTexts)
                text.Foreground = foreground;

            foreach (var text in accentTexts)
                text.Foreground = accent;

            if (shockTimeText is not null)
                shockTimeText.Text = $"发震时间：{shockTime}";

            if (placeText is not null)
                placeText.Text = $"震中：{place}";

            if (magnitudeText is not null)
                magnitudeText.Text = $"震级：{magnitude}";

            if (intensityText is not null)
                intensityText.Text = intensity;

            if (countdownText is not null)
                countdownText.Text = countdown;
        });
    }

    private void PushInternal(WarningState state, bool firstReport, bool overrideSettings)
    {
        var color = Color.Parse(state.BackgroundHex);
        var foreground = new SolidColorBrush(Color.Parse(state.ForegroundHex));
        var accent = new SolidColorBrush(Color.Parse(TierAccentHex(state.Tier)));

        var plainTexts = new List<TextBlock>();
        var accentTexts = new List<TextBlock>();

        var maskTitleText = new TextBlock
        {
            Text = "地震预警",
            FontSize = 38,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            Foreground = accent,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        accentTexts.Add(maskTitleText);

        var mask = new NotificationContent
        {
            Content = new Border
            {
                Padding = new Avalonia.Thickness(BannerSidePadding, 0, BannerSidePadding, 0),
                Child = maskTitleText
            },
            Duration = TimeSpan.FromSeconds(1.2),
            Color = new SolidColorBrush(color)
        };

        var line = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            Margin = new Avalonia.Thickness(BannerSidePadding, 0, BannerSidePadding, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        TextBlock AddText(string text, IBrush brush, bool accentText)
        {
            var block = new TextBlock
            {
                Text = text,
                FontSize = 24,
                Foreground = brush,
                VerticalAlignment = VerticalAlignment.Center
            };

            line.Children.Add(block);

            if (accentText)
                accentTexts.Add(block);
            else
                plainTexts.Add(block);

            return block;
        }

        AddText("地震预警", accent, true);
        var shockTimeText = AddText($"发震时间：{state.ShockTimeText}", foreground, false);
        var placeText = AddText($"震中：{state.PlaceName}", foreground, false);
        var magnitudeText = AddText($"震级：{state.MagnitudeText}", foreground, false);
        AddText("预估本地烈度：", accent, true);

        var intensityText = new TextBlock
        {
            Text = state.LocalIntensityText,
            FontSize = 26,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            Foreground = accent,
            VerticalAlignment = VerticalAlignment.Center
        };

        line.Children.Add(intensityText);
        accentTexts.Add(intensityText);

        var countdownText = new TextBlock
        {
            Text = state.Arrived
                ? "地震横波已到达"
                : $"倒计时：{state.CountdownText}",
            FontSize = 24,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            Foreground = accent,
            VerticalAlignment = VerticalAlignment.Center
        };

        line.Children.Add(countdownText);
        accentTexts.Add(countdownText);

        var durationSeconds = Math.Max(8, Math.Max(0, state.CountdownSeconds)
            + Plugin.Current!.Settings.ReleaseSeconds + 2);

        var overlay = new NotificationContent
        {
            Content = line,
            Duration = TimeSpan.FromSeconds(durationSeconds),
            Color = new SolidColorBrush(color)
        };

        var request = new ClassIsland.Core.Models.Notification.NotificationRequest
        {
            MaskContent = mask,
            OverlayContent = overlay,
            RequestNotificationSettings = new NotificationSettings
            {
                IsSettingsEnabled = false,
                IsNotificationEnabled = true,
                IsNotificationEffectEnabled = overrideSettings && firstReport && state.LocalIntensity >= 3,
                IsNotificationSoundEnabled = false,
                IsNotificationTopmostEnabled = overrideSettings && Plugin.Current!.Settings.TopMost,
                IsSpeechEnabled = false
            }
        };

        lock (_gate)
        {
            _plainTexts.Clear();
            _plainTexts.AddRange(plainTexts);
            _accentTexts.Clear();
            _accentTexts.AddRange(accentTexts);
            _shockTimeText = shockTimeText;
            _placeText = placeText;
            _magnitudeText = magnitudeText;
            _intensityText = intensityText;
            _countdownText = countdownText;
        }

        ShowNotification(request);
    }

    public static void ShowStrongEffect(WarningState state)
    {
        Instance?.ShowStrongEffectInternal(state);
    }

    private void ShowStrongEffectInternal(WarningState state)
    {
        var color = Color.Parse(state.BackgroundHex);
        var accent = new SolidColorBrush(Color.Parse(TierAccentHex(state.Tier)));

        var request = new ClassIsland.Core.Models.Notification.NotificationRequest
        {
            MaskContent = new NotificationContent
            {
                Content = new Border
                {
                    Padding = new Avalonia.Thickness(BannerSidePadding, 0, BannerSidePadding, 0),
                    Child = new TextBlock
                    {
                        Text = "地震预警",
                        FontSize = 38,
                        FontWeight = Avalonia.Media.FontWeight.Bold,
                        Foreground = accent,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                },
                Duration = TimeSpan.FromSeconds(1.2),
                Color = new SolidColorBrush(color)
            },
            OverlayContent = NotificationContent.Empty,
            RequestNotificationSettings = new NotificationSettings
            {
                IsSettingsEnabled = false,
                IsNotificationEnabled = true,
                IsNotificationEffectEnabled = true,
                IsNotificationSoundEnabled = false,
                IsNotificationTopmostEnabled = Plugin.Current!.Settings.TopMost,
                IsSpeechEnabled = false
            }
        };

        ShowNotification(request);
    }

    private static string TierAccentHex(WarningTier tier) => tier switch
    {
        WarningTier.BlueNoFeel or WarningTier.BlueFeel => "#3764FF",
        WarningTier.Yellow => "#FAE600",
        WarningTier.Orange => "#F09614",
        _ => "#DC2828"
    };
}
