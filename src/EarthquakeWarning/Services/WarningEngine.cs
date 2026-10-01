using System.Globalization;
using Voyage.EarthquakeWarning.Models;
using Voyage.EarthquakeWarning.Settings;

namespace Voyage.EarthquakeWarning.Services;

public sealed class WarningEngine
{
    private const string ArrivedAudio = "eew_arrived.mp3";
    private const string UpdateAudio = "eew_update.mp3";
    private const double LongAudioOffsetSeconds = 8;
    private const double SecondsAudioOffsetSeconds = 12;
    private const double LongAudioCountdownSeconds = 20;
    private const double DriftToleranceSeconds = 3;
    private const double HistoricalSeconds = 200;
    private const double ArrivedGraceSeconds = 20;
    private const double BlueNoFeelLongThreshold = 5;

    private readonly AudioService _audio;
    private readonly WarningWindowService _window;
    private readonly SettingsStore _settingsStore;
    private readonly object _sync = new();

    private WarningState? _state;
    private WarningTier? _lastTier;
    private double _lastCountdown;
    private DateTime _lastReceivedBeijing;
    private bool _audioSuppressed;
    private bool _isActive;
    private Task? _ticker;

    public WarningEngine(AudioService audio, WarningWindowService window, SettingsStore settingsStore)
    {
        _audio = audio;
        _window = window;
        _settingsStore = settingsStore;
        _window.UserClosed += OnWindowUserClosed;
    }

    private void OnWindowUserClosed(object? sender, EventArgs e)
    {
        _audioSuppressed = true;
        _audio.StopAll();
    }

    public Task ProcessAsync(EewData data, bool isSimulation, CancellationToken cancellationToken = default)
    {
        var settings = Plugin.Current!.Settings;

        if (!TryParseShockTime(data.ShockTime, out var shockTime)) return Task.CompletedTask;

        var now = NowBeijing();
        var age = (now - shockTime).TotalSeconds;

        if (!isSimulation && (age > HistoricalSeconds || age < -5)) return Task.CompletedTask;

        if (!TryGetLocation(settings, out var userLat, out var userLng)) return Task.CompletedTask;

        var distance = GeoDistance.HaversineKm(data.Latitude, data.Longitude, userLat, userLng);

        var localIntensity = IntensityCalculator.Calculate(ParseDouble(data.Magnitude), data.Depth ?? 0, distance);

        var roundedTrigger = (int)Math.Floor(localIntensity + 0.5);

        if (roundedTrigger < settings.TriggerThreshold) return Task.CompletedTask;

        var countdown = WaveArrivalCalculator.GetCountdownSeconds(distance, shockTime, now);

        var isArrived = countdown <= 0;

        bool isWarningActive;

        lock (_sync) isWarningActive = _isActive;

        if (!isSimulation && !isWarningActive && countdown < -ArrivedGraceSeconds) return Task.CompletedTask;

        var tier = GetTier(localIntensity);
        var state = CreateState(data, shockTime, now, distance, localIntensity, tier);

        bool isFirst;
        bool isUpdateReport;
        bool tierChanged;
        bool driftExceeded;
        bool allowReopen;

        lock (_sync)
        {
            var previousState = _state;

            isFirst = !_isActive || previousState is null;

            var reportInterval = isFirst ? 0 : (now - _lastReceivedBeijing).TotalSeconds;

            var predictedCountdown = _lastCountdown - reportInterval;

            tierChanged = !isFirst && _lastTier!.Value != tier;

            driftExceeded = !isFirst && Math.Abs(countdown - predictedCountdown) > DriftToleranceSeconds;

            isUpdateReport = !isFirst && previousState is not null &&
                IsSameEvent(previousState.EventId, data.EventId) &&
                previousState.Updates != data.Updates;

            allowReopen = isFirst || isUpdateReport;

            _state = state;
            _lastTier = tier;
            _lastCountdown = countdown;
            _lastReceivedBeijing = now;
            _isActive = true;

            if (allowReopen) _audioSuppressed = false;
        }

        if (settings.ForceVolume && settings.AdvancedOverride) _audio.ForceMasterVolume100();

        _settingsStore.Save();

        if (settings.WarningMode == WarningMode.NativeBanner)
        {
            if (isUpdateReport) EewNotificationProvider.UpdateReport(state);
            else EewNotificationProvider.Push(state, isFirst, settings.AdvancedOverride);
        }
        else
        {
            _window.ShowOrUpdate(state, settings.TopMost, allowReopen);

            if (isFirst && settings.AdvancedOverride && localIntensity >= 3) EewNotificationProvider.ShowStrongEffect(state);
        }

        if (isFirst) StartTicker(cancellationToken);

        var inAudioWindow = countdown > 0 && countdown <= LongAudioCountdownSeconds;

        var mayPlayAudio = isSimulation || inAudioWindow || (isArrived && isWarningActive) || state.Tier == WarningTier.BlueNoFeel;

        if (settings.EnableAlertSound && !_audioSuppressed && mayPlayAudio)
            PlayReportAudio(state, countdown, isFirst, isUpdateReport, tierChanged, driftExceeded, cancellationToken);

        return Task.CompletedTask;
    }

    public async Task RunSimulationAsync(CancellationToken token)
    {
        ResetForSimulation();

        var source = Plugin.Current!.Settings.Simulations ?? [];
        var list = source
            .Select(x => new SimulationReport
            {
                PlaceName = x.PlaceName,
                Longitude = x.Longitude,
                Latitude = x.Latitude,
                Magnitude = x.Magnitude,
                EpiIntensity = x.EpiIntensity,
                Depth = x.Depth,
                Updates = x.Updates,
                AlertDelaySeconds = x.AlertDelaySeconds
            })
            .ToList();

        var simulationEventId = "SIM-" + DateTime.Now.ToString("yyyyMMddHHmmssfff");

        var shockTime = DateTime.UtcNow.AddHours(8);

        try
        {
            foreach (var sim in list)
            {
                token.ThrowIfCancellationRequested();

                var elapsed = (DateTime.UtcNow.AddHours(8) - shockTime).TotalSeconds;

                var wait = (sim.AlertDelaySeconds ?? 0) - elapsed;

                if (wait > 0) await Task.Delay(TimeSpan.FromSeconds(wait), token);

                var data = new EewData
                {
                    Id = Guid.NewGuid().ToString("N"),
                    EventId = $"{simulationEventId}.{(sim.Updates ?? 0):0000}",
                    ShockTime = shockTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    Longitude = sim.Longitude ?? 0,
                    Latitude = sim.Latitude ?? 0,
                    PlaceName = sim.PlaceName,
                    Magnitude = (sim.Magnitude ?? 0).ToString("0.0", CultureInfo.InvariantCulture),
                    EpiIntensity = sim.EpiIntensity ?? 0,
                    Depth = sim.Depth,
                    Updates = sim.Updates ?? 0
                };

                await ProcessAsync(data, true, token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception) { }
    }

    private void ResetForSimulation()
    {
        lock (_sync)
        {
            _isActive = false;
            _state = null;
            _lastTier = null;
            _lastCountdown = 0;
            _lastReceivedBeijing = default;
            _audioSuppressed = false;
        }

        _audio.StopAll();

        if (Plugin.Current!.Settings.WarningMode == WarningMode.IndependentUi) _window.Close();
    }

    private void StartTicker(CancellationToken parentToken)
    {
        if (_ticker is { IsCompleted: false }) return;

        _ticker = Task.Run(async () =>
        {
            try
            {
                while (!parentToken.IsCancellationRequested)
                {
                    WarningState? state;

                    lock (_sync) state = _state;

                    if (state is null || !_isActive) break;

                    var now = NowBeijing();

                    try
                    {
                        state.UpdateCountdown(now);

                        if (Plugin.Current!.Settings.WarningMode == WarningMode.IndependentUi)
                        {
                            _window.ShowOrUpdate(state, Plugin.Current!.Settings.TopMost, false);
                        }
                        else
                        {
                            EewNotificationProvider.UpdateCountdown(state);
                        }

                        if (state.Arrived)
                        {
                            var releaseAt = state.ArrivalTimeBeijing.AddSeconds(Plugin.Current!.Settings.ReleaseSeconds);

                            if (now >= releaseAt)
                            {
                                StopWarning();
                                break;
                            }
                        }
                    }
                    catch (Exception) { }

                    await Task.Delay(200, parentToken);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                lock (_sync) _ticker = null;
            }
        }, parentToken);
    }

    private void StopWarning()
    {
        lock (_sync)
        {
            _isActive = false;
            _state = null;
            _audioSuppressed = false;
        }

        _audio.StopAll();

        if (Plugin.Current!.Settings.WarningMode == WarningMode.IndependentUi) _window.Close();

        _audio.ScheduleRestoreAfter10Seconds();
    }

    private void PlayReportAudio(WarningState state, double countdown, bool isFirst, bool isUpdateReport, bool tierChanged, bool driftExceeded, CancellationToken token)
    {
        var cues = BuildAudioCues(state.Tier, countdown, isFirst, tierChanged, driftExceeded);

        if (cues.Count == 0) return;

        _audio.PlaySequence(cues, token);

        if (isUpdateReport) _audio.PlayOverlay(UpdateAudio, token);
    }

    private List<AudioCue> BuildAudioCues(WarningTier tier, double countdown, bool isFirst, bool tierChanged, bool driftExceeded)
    {
        if (tier == WarningTier.BlueNoFeel)
        {
            if (!isFirst && !tierChanged && !driftExceeded) return [];

            return BuildBlueNoFeelCues(countdown, isFirst);
        }

        if (countdown <= 0)
        {
            if (!isFirst && !_audio.IsPlaying) return [];

            return ArrivalCues(tier);
        }

        if (isFirst)
        {
            if (countdown >= LongAudioCountdownSeconds)
            {
                return [new AudioCue(LongFile(tier), 0), BuildSecondsCue(tier, countdown, LongAudioOffsetSeconds)];
            }

            return [BuildSecondsCue(tier, countdown, SecondsAudioOffsetSeconds)];
        }

        if (!tierChanged && !driftExceeded) return [];

        return [BuildSecondsCue(tier, countdown, SecondsAudioOffsetSeconds)];
    }

    private AudioCue BuildSecondsCue(WarningTier tier, double countdown, double offsetSeconds)
    {
        var file = SecondsFile(tier);
        var duration = _audio.GetDurationSeconds(file);

        var start = Math.Max(0, duration - offsetSeconds - countdown);

        return new AudioCue(file, start);
    }

    private static List<AudioCue> ArrivalCues(WarningTier tier) =>
        [new AudioCue(ArrivedAudio, 0), new AudioCue(ArrivedFile(tier), 0)];

    private List<AudioCue> BuildBlueNoFeelCues(double countdown, bool isFirst)
    {
        if (countdown <= 0)
        {
            if (!isFirst && !_audio.IsPlaying) return [];

            return ArrivalCues(WarningTier.BlueNoFeel);
        }

        if (countdown >= BlueNoFeelLongThreshold)
        {
            const string blue = "eew_blue_nofeel.mp3";

            var oneBlue = _audio.GetDurationSeconds(blue);
            var delayToArrival = Math.Max(0, countdown - oneBlue);

            return
            [
                new AudioCue(blue, 0),
                new AudioCue(ArrivedAudio, 0, delayToArrival),
                new AudioCue(ArrivedFile(WarningTier.BlueNoFeel), 0)
            ];
        }

        if (!isFirst && !_audio.IsPlaying) return [];

        return ArrivalCues(WarningTier.BlueNoFeel);
    }

    private static bool IsSameEvent(string previousEventId, string currentEventId)
    {
        if (string.IsNullOrWhiteSpace(previousEventId) || string.IsNullOrWhiteSpace(currentEventId)) return false;

        static string BaseEventId(string eventId)
        {
            var dot = eventId.IndexOf('.');

            return dot >= 0 ? eventId[..dot] : eventId;
        }

        return string.Equals(BaseEventId(previousEventId), BaseEventId(currentEventId), StringComparison.Ordinal);
    }

    private static string LongFile(WarningTier tier) => tier switch
    {
        WarningTier.BlueNoFeel => "eew_blue_nofeel.mp3",
        WarningTier.BlueFeel => "eew_blue_feel.mp3",
        WarningTier.Yellow => "eew_yellow.mp3",
        WarningTier.Orange => "eew_orange.mp3",
        _ => "eew_red.mp3"
    };

    private static string SecondsFile(WarningTier tier) => tier switch
    {
        WarningTier.BlueNoFeel => "eew_blue_nofeel_seconds.mp3",
        WarningTier.BlueFeel => "eew_blue_feel_seconds.mp3",
        WarningTier.Yellow => "eew_yellow_seconds.mp3",
        WarningTier.Orange => "eew_orange_seconds.mp3",
        _ => "eew_red_seconds.mp3"
    };

    private static string ArrivedFile(WarningTier tier) => tier switch
    {
        WarningTier.BlueNoFeel => "arrived_blue_nofeel.mp3",
        WarningTier.BlueFeel => "arrived_blue_feel.mp3",
        WarningTier.Yellow => "arrived_yellow.mp3",
        WarningTier.Orange => "arrived_orange.mp3",
        _ => "arrived_red.mp3"
    };

    private WarningState CreateState(EewData data, DateTime shockTime, DateTime received, double distance, double localIntensity, WarningTier tier)
    {
        var (bg, fg, name, sensation, icon) = TierVisual(tier);

        var state = new WarningState
        {
            EventId = data.EventId,
            PlaceName = data.PlaceName,
            ShockTimeText = shockTime.ToString("yyyy-MM-dd HH:mm:ss"),
            MagnitudeText = data.Magnitude,
            DepthKm = data.Depth ?? 0,
            EpicenterIntensity = data.EpiIntensity,
            Updates = data.Updates,
            Tier = tier,
            LocalIntensity = localIntensity,
            DistanceText = $"{distance:0.#} km",
            TierText = name,
            Sensation = sensation,
            BackgroundHex = bg,
            ForegroundHex = fg,
            IconPath = icon,
            ShockTimeBeijing = shockTime,
            ReceivedAtBeijing = received,
            ArrivalTimeBeijing = received.AddSeconds(Math.Max(0, WaveArrivalCalculator.GetCountdownSeconds(distance, shockTime, received)))
        };

        state.LocalIntensityText = localIntensity.ToString("0.0", CultureInfo.InvariantCulture);

        state.UpdateCountdown(received);

        return state;
    }

    private static (string Bg, string Fg, string Name, string Sensation, string Icon) TierVisual(WarningTier tier)
    {
        var pluginDirectory = Path.GetDirectoryName(typeof(WarningEngine).Assembly.Location) ?? AppContext.BaseDirectory;
        var img = Path.Combine(pluginDirectory, "Assets", "Images");

        return tier switch
        {
            WarningTier.BlueNoFeel => ("#3764FF", "#FFFFFF", "蓝色地震预警（无感）", "无感地震\n请勿惊慌", Path.Combine(img, "blue.png")),
            WarningTier.BlueFeel => ("#3764FF", "#FFFFFF", "蓝色地震预警（有感）", "轻微有感地震\n请勿惊慌", Path.Combine(img, "blue.png")),
            WarningTier.Yellow => ("#FAE600", "#000000", "黄色地震预警", "显著有感地震\n注意避险", Path.Combine(img, "yellow.png")),
            WarningTier.Orange => ("#F09614", "#000000", "橙色地震预警", "破坏性地震\n就近避险", Path.Combine(img, "orange.png")),
            _ => ("#DC2828", "#FFFFFF", "红色地震预警", "严重破坏性地震\n紧急避险", Path.Combine(img, "red.png"))
        };
    }

    private static WarningTier GetTier(double intensity) => intensity switch
    {
        < 0.5 => WarningTier.BlueNoFeel,
        < 2.5 => WarningTier.BlueFeel,
        < 4.5 => WarningTier.Yellow,
        < 6.5 => WarningTier.Orange,
        _ => WarningTier.Red
    };

    private static bool TryGetLocation(PluginSettings settings, out double lat, out double lng)
    {
        lat = 0;
        lng = 0;

        if (!double.TryParse(settings.Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out lat)) return false;

        if (!double.TryParse(settings.Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out lng)) return false;

        return lat is >= -90 and <= 90 && lng is >= -180 and <= 180;
    }

    private static bool TryParseShockTime(string text, out DateTime value) =>
        DateTime.TryParseExact(text, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out value);

    private static double ParseDouble(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static DateTime NowBeijing() => DateTime.UtcNow.AddHours(8);
}
