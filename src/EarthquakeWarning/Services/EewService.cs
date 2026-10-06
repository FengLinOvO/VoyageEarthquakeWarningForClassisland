using System.Globalization;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Voyage.EarthquakeWarning.Models;

namespace Voyage.EarthquakeWarning.Services;

public sealed partial class EewService : BackgroundService
{
    private const string ApiUrl = "wss://api.odysphere.tech/cea";
    private const string TokenMissingText = "未连接（未填写 API Token）";
    private const string MiuiUrlEncoded = "aHR0cHM6Ly9zcnYuc2VjLm1pdWkuY29tL2VhcnRocXVha2Uvd2FybmluZy9yZWNvcmRz";
    private const string MiuiSignEncoded = "M0YwRkM1MzA4QUFBQkFGNjY2RTg3MEJFQ0NFNzY2REU=";

    private static readonly string MiuiUrl = Decode(MiuiUrlEncoded);
    private static readonly string MiuiSign = Decode(MiuiSignEncoded);
    private static readonly TimeSpan MiuiTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MiuiPollInterval = TimeSpan.FromSeconds(2);
    private static readonly HttpClient MiuiClient = new() { Timeout = MiuiTimeout };

    private readonly WarningEngine _engine;
    private string? _lastMiuiKey;
    private CancellationTokenSource _switchSource = new();

    public EewService(WarningEngine engine) => _engine = engine;

    public void Restart()
    {
        var previous = _switchSource;
        _switchSource = new CancellationTokenSource();
        _lastMiuiKey = null;
        previous.Cancel();
    }

    private static string Decode(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value));

    private static ApiSource CurrentSource => Plugin.Current!.Settings.ApiSource;

    private static void SetStatus(ApiSource source, string text)
    {
        if (Plugin.Current!.Settings.ApiSource == source) Plugin.Current!.Settings.ApiConnectionTimeText = text;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _switchSource.Token);

            try
            {
                if (CurrentSource == ApiSource.Miui) await IterateMiuiAsync(linked.Token);
                else await IterateVoyageAsync(linked.Token);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (OperationCanceledException) { }
        }
    }

    private static DateTime NowBeijing() => DateTime.UtcNow.AddHours(8);

    private async Task IterateVoyageAsync(CancellationToken token)
    {
        var apiToken = Plugin.Current!.Settings.ApiToken?.Trim() ?? "";

        if (apiToken.Length == 0)
        {
            SetStatus(ApiSource.Voyage, TokenMissingText);
            await Task.Delay(TimeSpan.FromSeconds(3), token);
            return;
        }

        try
        {
            using var ws = new ClientWebSocket();
            await ws.ConnectAsync(new Uri(ApiUrl), token);
            token.ThrowIfCancellationRequested();
            SetStatus(ApiSource.Voyage, $"API连接于：{NowBeijing():yyyy-MM-dd HH:mm:ss}");
            await ws.SendAsync(Encoding.UTF8.GetBytes(apiToken), WebSocketMessageType.Text, true, token);
            await ReceiveLoopAsync(ws, token);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { SetStatus(ApiSource.Voyage, $"连接失败：{ex.Message}"); }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken token)
    {
        var buffer = new byte[32 * 1024];
        using var ms = new MemoryStream();

        while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
        {
            var result = await ws.ReceiveAsync(buffer, token);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                SetStatus(ApiSource.Voyage, string.IsNullOrWhiteSpace(result.CloseStatusDescription)
                    ? $"连接已被服务端关闭（{result.CloseStatus}）"
                    : $"连接已被服务端关闭：{result.CloseStatusDescription}");
                break;
            }

            if (result.MessageType != WebSocketMessageType.Text) continue;

            ms.Write(buffer, 0, result.Count);
            if (!result.EndOfMessage) continue;

            var json = Encoding.UTF8.GetString(ms.ToArray());
            ms.SetLength(0);

            EewEnvelope? envelope;

            try { envelope = JsonSerializer.Deserialize<EewEnvelope>(json); }
            catch (JsonException ex) { Plugin.Current!.Settings.ApiRecentDataText = $"数据解析失败：{ex.Message}"; continue; }

            if (envelope?.Data is null) continue;

            var d = envelope.Data;
            var depth = d.Depth ?? 0;

            Plugin.Current!.Settings.ApiRecentDataText =
                $"中国地震预警网第{d.Updates}报，{d.ShockTime}在{d.PlaceName}附近({d.Latitude:0.###},{d.Longitude:0.###})正在发生{d.Magnitude}级地震，震源深度{depth:0.#}km，预估最大烈度{d.EpiIntensity:0.#}";

            try { await _engine.ProcessAsync(d, false, token); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ErrorReporter.Report(ex, "EewService.ProcessAsync"); }
        }
    }

    private async Task IterateMiuiAsync(CancellationToken token)
    {
        try
        {
            await RequestMiuiAsync(token);
            await Task.Delay(MiuiPollInterval, token);
        }
        catch (TaskCanceledException) when (!token.IsCancellationRequested) { SetStatus(ApiSource.Miui, "最近验证失败：请求超时"); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { SetStatus(ApiSource.Miui, $"最近验证失败：{ex.Message}"); }
    }

    private async Task RequestMiuiAsync(CancellationToken token)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["version"] = "2", ["sign"] = MiuiSign });
        using var response = await MiuiClient.PostAsync(MiuiUrl, content, token);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(token);
        token.ThrowIfCancellationRequested();
        SetStatus(ApiSource.Miui, $"最近验证时间：{NowBeijing():yyyy-MM-dd HH:mm:ss}");

        MiuiEnvelope? envelope;

        try { envelope = JsonSerializer.Deserialize<MiuiEnvelope>(json); }
        catch (JsonException ex) { Plugin.Current!.Settings.ApiRecentDataText = $"数据解析失败：{ex.Message}"; return; }

        var record = envelope?.Data?.FirstOrDefault();
        if (record is null) return;

        var key = string.Create(CultureInfo.InvariantCulture, $"{record.EventId}-{record.Update}");
        if (key == _lastMiuiKey) return;
        _lastMiuiKey = key;

        var depth = record.Depth;
        var shockTime = DateTimeOffset.FromUnixTimeMilliseconds(record.StartAt).ToOffset(TimeSpan.FromHours(8)).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        var d = new EewData
        {
            Id = record.Id.ToString(CultureInfo.InvariantCulture),
            EventId = record.EventId.ToString(CultureInfo.InvariantCulture),
            ShockTime = shockTime,
            Longitude = record.Longitude,
            Latitude = record.Latitude,
            PlaceName = record.Epicenter ?? "",
            Magnitude = record.Magnitude.ToString("0.0", CultureInfo.InvariantCulture),
            Depth = depth,
            Updates = record.Update
        };

        Plugin.Current!.Settings.ApiRecentDataText =
            $"中国地震预警网第{d.Updates}报，{d.ShockTime}在{d.PlaceName}附近({d.Latitude:0.###},{d.Longitude:0.###})正在发生{d.Magnitude}级地震，震源深度{depth:0.#}km";

        try { await _engine.ProcessAsync(d, false, token); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ErrorReporter.Report(ex, "EewService.ProcessAsync"); }
    }
}
