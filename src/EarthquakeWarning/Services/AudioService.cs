using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Voyage.EarthquakeWarning.Services;

public sealed record AudioCue(string FileName, double StartSeconds, double DelaySeconds = 0);

public sealed class AudioService
{
    private readonly string _audioDirectory;
    private readonly object _gate = new();

    private CancellationTokenSource? _currentCts;
    private WaveOutEvent? _currentOutput;
    private Task _playbackTask = Task.CompletedTask;
    private readonly List<CancellationTokenSource> _overlayCts = [];
    private readonly List<WaveOutEvent> _overlayOutputs = [];
    private float? _originalMasterVolume;
    private DateTime _lastVolumeRestoreDeadline = DateTime.MinValue;
    private volatile bool _playing;

    public bool IsPlaying => _playing;

    public AudioService()
    {
        var pluginDirectory = Path.GetDirectoryName(typeof(AudioService).Assembly.Location)
            ?? AppContext.BaseDirectory;

        _audioDirectory = Path.Combine(pluginDirectory, "Assets", "Audio");
    }

    private string GetAudioPath(string fileName)
    {
        var path = Path.Combine(_audioDirectory, fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException("Audio file not found.", path);
        if (new FileInfo(path).Length <= 44)
            throw new InvalidDataException("Audio file is empty or invalid.");
        return path;
    }

    public double GetDurationSeconds(string fileName)
    {
        using var reader = new AudioFileReader(GetAudioPath(fileName));
        return reader.TotalTime.TotalSeconds;
    }

    public void PlaySequence(IReadOnlyList<AudioCue> cues, CancellationToken cancellationToken)
    {
        if (cues.Count == 0)
            return;

        lock (_gate)
        {
            var previous = _playbackTask;
            _currentCts?.Cancel();

            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _currentCts = cts;
            _playing = true;
            _playbackTask = RunSequenceAsync(previous, cues, cts);
        }
    }

    private async Task RunSequenceAsync(Task previous, IReadOnlyList<AudioCue> cues, CancellationTokenSource cts)
    {
        try { await previous.ConfigureAwait(false); }
        catch { }

        try
        {
            foreach (var cue in cues)
            {
                cts.Token.ThrowIfCancellationRequested();

                if (cue.DelaySeconds > 0)
                    await Task.Delay((int)(cue.DelaySeconds * 1000), cts.Token).ConfigureAwait(false);

                await PlayFileAsync(cue, cts.Token, false).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ErrorReporter.Report(ex, "AudioService.RunSequence", true); }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_currentCts, cts))
                {
                    _currentCts = null;
                    _currentOutput = null;
                    _playing = false;
                }
            }
        }
    }

    private async Task PlayFileAsync(AudioCue cue, CancellationToken token, bool overlay)
    {
        using var reader = new AudioFileReader(GetAudioPath(cue.FileName));

        var maxStart = Math.Max(0, reader.TotalTime.TotalSeconds - 0.05);
        var start = Math.Clamp(cue.StartSeconds, 0, maxStart);

        if (start > 0)
            reader.CurrentTime = TimeSpan.FromSeconds(start);

        using var output = new WaveOutEvent();

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        output.PlaybackStopped += (_, _) => completion.TrySetResult();

        output.Init(reader);

        using var registration = token.Register(() =>
        {
            try { output.Stop(); }
            catch { }
        });

        lock (_gate)
        {
            if (overlay)
                _overlayOutputs.Add(output);
            else
                _currentOutput = output;
        }

        output.Play();

        try
        {
            await completion.Task.ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
        }
        finally
        {
            lock (_gate)
            {
                if (overlay)
                    _overlayOutputs.Remove(output);
                else if (ReferenceEquals(_currentOutput, output))
                    _currentOutput = null;
            }
        }
    }

    public void PlayOverlay(string fileName, CancellationToken cancellationToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        lock (_gate)
            _overlayCts.Add(cts);

        _ = RunOverlayAsync(new AudioCue(fileName, 0), cts);
    }

    private async Task RunOverlayAsync(AudioCue cue, CancellationTokenSource cts)
    {
        try { await PlayFileAsync(cue, cts.Token, true).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ErrorReporter.Report(ex, "AudioService.RunOverlay", true); }
        finally
        {
            lock (_gate)
                _overlayCts.Remove(cts);
        }
    }

    public void StopAll()
    {
        CancellationTokenSource? cts;
        WaveOutEvent? output;
        CancellationTokenSource[] overlays;
        WaveOutEvent[] overlayOutputs;

        lock (_gate)
        {
            cts = _currentCts;
            output = _currentOutput;
            overlays = [.. _overlayCts];
            overlayOutputs = [.. _overlayOutputs];
            _playing = false;
        }

        try { output?.Stop(); }
        catch { }

        foreach (var overlayOutput in overlayOutputs)
        {
            try { overlayOutput.Stop(); }
            catch { }
        }

        try { cts?.Cancel(); }
        catch { }

        foreach (var overlay in overlays)
        {
            try { overlay.Cancel(); }
            catch { }
        }
    }

    public void ForceMasterVolume100()
    {
        try
        {
            var endpoint = new MMDeviceEnumerator()
                .GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            _originalMasterVolume ??= endpoint.AudioEndpointVolume.MasterVolumeLevelScalar;

            endpoint.AudioEndpointVolume.MasterVolumeLevelScalar = 1.0f;
            _lastVolumeRestoreDeadline = DateTime.UtcNow.AddSeconds(10);
        }
        catch (Exception ex) { ErrorReporter.Report(ex, "AudioService.ForceMasterVolume", true); }
    }

    public void RestoreMasterVolume()
    {
        if (_originalMasterVolume is null)
            return;

        try
        {
            var endpoint = new MMDeviceEnumerator()
                .GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            endpoint.AudioEndpointVolume.MasterVolumeLevelScalar = _originalMasterVolume.Value;
        }
        catch (Exception ex) { ErrorReporter.Report(ex, "AudioService.RestoreMasterVolume", true); }

        _originalMasterVolume = null;
    }

    public void ScheduleRestoreAfter10Seconds()
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        _lastVolumeRestoreDeadline = deadline;

        _ = Task.Run(async () =>
        {
            var wait = deadline - DateTime.UtcNow;

            if (wait > TimeSpan.Zero)
                await Task.Delay(wait);

            if (_lastVolumeRestoreDeadline == deadline)
                RestoreMasterVolume();
        });
    }
}
