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
    private const string MiuiUrl = "https://srv.sec.miui.com/earthquake/warning/records";
    private const string MiuiSign = "3F0FC5308AAABAF666E870BECCE766DE";
    private const string TokenMissingText = "未连接（未填写 API Token）";

    private static readonly TimeSpan MiuiTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MiuiPollInterval = TimeSpan.FromSeconds(2);
    private static readonly HttpClient MiuiClient = new() { Timeout = MiuiTimeout };

    private readonly WarningEngine _engine;
    private string? _lastMiuiKey;

    public EewService(WarningEngine engine)
    {
        _engine = engine;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (Plugin.Current!.Settings.ApiSource == ApiSource.Miui)
                    await IterateMiuiAsync(stoppingToken);
                else
                    await IterateVoyageAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private static DateTime NowBeijing() => DateTime.UtcNow.AddHours(8);

    private async Task IterateVoyageAsync(CancellationToken stoppingToken)
    {
        var token =
            Plugin.Current!.Settings.ApiToken?.Trim() ?? "";

        if (token.Length == 0)
        {
            Plugin.Current!.Settings.ApiConnectionTimeText =
                TokenMissingText;

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            return;
        }

        try
        {
            using var ws = new ClientWebSocket();
            await ws.ConnectAsync(new Uri(ApiUrl), stoppingToken);
            Plugin.Current!.Settings.ApiConnectionTimeText =
                $"API连接于：{NowBeijing():yyyy-MM-dd HH:mm:ss}";

            await ws.SendAsync(
                Encoding.UTF8.GetBytes(token),
                WebSocketMessageType.Text,
                true,
                stoppingToken);

            await ReceiveLoopAsync(ws, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Plugin.Current!.Settings.ApiConnectionTimeText =
                $"连接失败：{ex.Message}";
        }
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
                Plugin.Current!.Settings.ApiConnectionTimeText =
                    string.IsNullOrWhiteSpace(result.CloseStatusDescription)
                        ? $"连接已被服务端关闭（{result.CloseStatus}）"
                        : $"连接已被服务端关闭：{result.CloseStatusDescription}";

                break;
            }

            if (result.MessageType != WebSocketMessageType.Text) continue;

            ms.Write(buffer, 0, result.Count);
            if (!result.EndOfMessage) continue;

            var json = Encoding.UTF8.GetString(ms.ToArray());
            ms.SetLength(0);

            EewEnvelope? envelope;

            try
            {
                envelope = JsonSerializer.Deserialize<EewEnvelope>(json);
            }
            catch (JsonException ex)
            {
                Plugin.Current!.Settings.ApiRecentDataText =
                    $"数据解析失败：{ex.Message}";

                continue;
            }

            if (envelope?.Data is null)
                continue;

            var d = envelope.Data;
            var depth = d.Depth ?? 0;

            Plugin.Current!.Settings.ApiRecentDataText =
                $"中国地震预警网第{d.Updates}报，{d.ShockTime}在{d.PlaceName}附近({d.Latitude:0.###},{d.Longitude:0.###})正在发生{d.Magnitude}级地震，震源深度{depth:0.#}km，预估最大烈度{d.EpiIntensity:0.#}";

            try
            {
                await _engine.ProcessAsync(d, false, token);
            }
            catch
            {
            }
        }
    }

    private async Task IterateMiuiAsync(CancellationToken stoppingToken)
    {
        try
        {
            await RequestMiuiAsync(stoppingToken);
            await Task.Delay(MiuiPollInterval, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Plugin.Current!.Settings.ApiConnectionTimeText =
                $"最近验证失败：{ex.Message}";
        }
    }

    private async Task RequestMiuiAsync(CancellationToken token)
    {
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["version"] = "2",
                ["sign"] = MiuiSign
            });

        using var response =
            await MiuiClient.PostAsync(MiuiUrl, content, token);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(token);

        Plugin.Current!.Settings.ApiConnectionTimeText =
            $"最近验证时间：{NowBeijing():yyyy-MM-dd HH:mm:ss}";

        MiuiEnvelope? envelope;

        try
        {
            envelope = JsonSerializer.Deserialize<MiuiEnvelope>(json);
        }
        catch (JsonException ex)
        {
            Plugin.Current!.Settings.ApiRecentDataText =
                $"数据解析失败：{ex.Message}";

            return;
        }

        var record = envelope?.Data?.FirstOrDefault();

        if (record is null)
            return;

        var key = string.Create(
            CultureInfo.InvariantCulture,
            $"{record.EventId}-{record.Update}");

        if (key == _lastMiuiKey)
            return;

        _lastMiuiKey = key;

        var depth = record.Depth;

        var shockTime = DateTimeOffset
            .FromUnixTimeMilliseconds(record.StartAt)
            .ToOffset(TimeSpan.FromHours(8))
            .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

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

        await _engine.ProcessAsync(d, false, token);
    }
}
