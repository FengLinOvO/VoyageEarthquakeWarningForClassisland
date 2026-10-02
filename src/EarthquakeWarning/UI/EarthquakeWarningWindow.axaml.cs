using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Voyage.EarthquakeWarning.Models;

namespace Voyage.EarthquakeWarning.UI;

public partial class EarthquakeWarningWindow : Window
{
    private const double MarqueeSpeed = 80;
    private const double MarqueeGap = 20;
    private const double SecondsMaxOffsetX = 64;
    private const double SecondsBottomMargin = 44;

    private sealed class Marquee
    {
        public required TextBlock Text { get; init; }
        public required Border Host { get; init; }
        public TranslateTransform Transform { get; } = new();
        public string Value { get; set; } = "";
        public double TextWidth { get; set; }
        public double Offset { get; set; }
    }

    private readonly List<Marquee> _marquees = [];
    private CancellationTokenSource? _marqueeCts;
    private DispatcherTimer? _scaleWatcher;
    private double _lastScaling;
    private Avalonia.PixelRect _lastBounds;

    public bool UserRequestedClose { get; private set; }

    public EarthquakeWarningWindow()
    {
        InitializeComponent();

        CloseButton.Click += (_, _) =>
        {
            UserRequestedClose = true;
            Close();
        };

        Opened += (_, _) =>
        {
            StartMarquee();
            StartScaleWatcher();
        };

        Closed += (_, _) =>
        {
            _marqueeCts?.Cancel();
            _scaleWatcher?.Stop();
        };
    }

    private void StartScaleWatcher()
    {
        UpdateLayoutForScreen();

        _scaleWatcher = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _scaleWatcher.Tick += (_, _) => CheckScreen();
        _scaleWatcher.Start();
    }

    private void CheckScreen()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null) return;

        var bounds = screen.Bounds;
        if (Math.Abs(screen.Scaling - _lastScaling) < 0.001 && bounds == _lastBounds) return;

        UpdateLayoutForScreen();
    }

    private void UpdateLayoutForScreen()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null) return;

        var bounds = screen.Bounds;
        _lastScaling = screen.Scaling;
        _lastBounds = bounds;

        var scale = Math.Max(1.0, screen.Scaling);
        var width = Math.Max(1100, bounds.Width / scale / 2.0);
        var height = Math.Max(620, bounds.Height / scale / 2.0);

        Width = width;
        Height = height;

        var area = screen.WorkingArea;
        var widthPx = width * scale;
        var heightPx = height * scale;

        Position = new Avalonia.PixelPoint(
            area.X + (int)Math.Round((area.Width - widthPx) / 2.0),
            area.Y + (int)Math.Round((area.Height - heightPx) / 2.0));
    }

    private void StartMarquee()
    {
        _marquees.Add(new Marquee { Text = ShockTimeText, Host = ShockTimeHost });
        _marquees.Add(new Marquee { Text = PlaceText, Host = PlaceHost });

        foreach (var marquee in _marquees)
            marquee.Text.RenderTransform = marquee.Transform;

        _marqueeCts = new CancellationTokenSource();
        _ = RunMarqueeAsync(_marqueeCts.Token);
    }

    private async Task RunMarqueeAsync(CancellationToken token)
    {
        var last = DateTime.UtcNow;

        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(20, token);

                var now = DateTime.UtcNow;
                var elapsed = (now - last).TotalSeconds;
                last = now;

                foreach (var marquee in _marquees)
                    UpdateMarquee(marquee, elapsed);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static void UpdateMarquee(Marquee marquee, double elapsed)
    {
        var value = marquee.Text.Text ?? "";

        if (value != marquee.Value)
        {
            marquee.Value = value;
            marquee.Offset = 0;
            marquee.TextWidth = MeasureTextWidth(marquee.Text, value);
        }

        var available = marquee.Host.Bounds.Width;

        if (available <= 0 || marquee.TextWidth <= available)
        {
            marquee.Text.Width = double.NaN;
            marquee.Text.HorizontalAlignment = HorizontalAlignment.Center;
            marquee.Offset = 0;
            marquee.Transform.X = 0;
            return;
        }

        marquee.Text.Width = marquee.TextWidth;
        marquee.Text.HorizontalAlignment = HorizontalAlignment.Left;

        marquee.Offset -= MarqueeSpeed * elapsed;
        if (marquee.Offset <= -marquee.TextWidth - MarqueeGap) marquee.Offset = available;

        marquee.Transform.X = marquee.Offset;
    }

    private static double MeasureTextWidth(TextBlock text, string value)
    {
        var typeface = new Typeface(text.FontFamily, text.FontStyle, text.FontWeight);
        var formatted = new FormattedText(
            value,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            text.FontSize,
            Brushes.Black);

        return formatted.Width;
    }

    private void UpdateSecondsPosition()
    {
        var ring = CountdownRing.Bounds.Width;
        if (ring <= 0) ring = 280;

        var numberWidth = MeasureTextWidth(CountdownMain, CountdownMain.Text ?? "");
        var left = Math.Min(ring / 2 + numberWidth / 2 + 8, ring / 2 + SecondsMaxOffsetX);

        SecondsText.Margin = new Avalonia.Thickness(left, 0, 0, SecondsBottomMargin);
    }

    public void UpdateState(WarningState state)
    {
        var foreground = Brush.Parse(state.ForegroundHex);
        RootBorder.Background = Brush.Parse(state.BackgroundHex);
        RootBorder.BorderBrush = Brush.Parse("#3C3C3C");

        foreach (var text in new TextBlock[]
        {
            TitleText,
            NetworkText,
            NoticeText,
            ArrivalLabelText,
            CountdownLabelText,
            CountdownMain,
            SecondsText,
            SensationText1,
            SensationText2,
            ShockTimeLabel,
            ShockTimeText,
            PlaceLabel,
            PlaceText,
            DistanceLabel,
            DistanceText,
            MagnitudeLabel,
            MagnitudeText,
            LocalIntensityLabel,
            LocalIntensityText
        })
        {
            text.Foreground = foreground;
        }

        CloseButton.Foreground = foreground;

        if (state.Arrived)
        {
            CountdownMain.FontSize = 96;
            CountdownMain.Text = "到达";
        }
        else
        {
            var seconds = Math.Max(0, Math.Ceiling(state.CountdownSeconds));
            CountdownMain.Text = seconds.ToString("0");
            CountdownMain.FontSize = seconds >= 100 ? 120 : 150;
        }

        SecondsText.IsVisible = !state.Arrived;
        UpdateSecondsPosition();

        var sensation = state.Sensation.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        SensationText1.Text = sensation.Length > 0 ? sensation[0] : "";
        SensationText2.Text = sensation.Length > 1 ? sensation[1] : "";

        ShockTimeText.Text = state.ShockTimeText;
        PlaceText.Text = state.PlaceName;
        DistanceText.Text = state.DistanceText;
        MagnitudeText.Text = state.MagnitudeText;
        LocalIntensityText.Text = state.LocalIntensityText;
        NetworkText.Text = $"中国地震预警网 第{state.Updates}报";
        TitleText.Text = "地震预警";

        try
        {
            var pluginDirectory = Path.GetDirectoryName(typeof(EarthquakeWarningWindow).Assembly.Location)
                ?? AppContext.BaseDirectory;
            var path = Path.Combine(pluginDirectory, "Assets", "Images", Path.GetFileName(state.IconPath));

            if (File.Exists(path))
            {
                TierImage.Source = new Avalonia.Media.Imaging.Bitmap(path);
            }
            else
            {
                TierImage.Source = null;
            }
        }
        catch
        {
            TierImage.Source = null;
        }
    }
}
