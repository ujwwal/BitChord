using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace BitChord.Core;

/// <summary>
/// Long-lived, anonymous client for the YouTube Music Innertube API.
/// </summary>
public sealed partial class AnonymousInnertubeClient
{
    private const string MusicApiBase = "https://music.youtube.com/youtubei/v1/";
    private const string YouTubeApiBase = "https://www.youtube.com/youtubei/v1/";
    private const string MusicOrigin = "https://music.youtube.com";
    private const string WebRemixClientId = "67";
    private const string FallbackWebRemixVersion = "1.20260707.12.00";
    private const string WebUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36";

    private static readonly HttpClient SharedHttpClient = CreateHttpClient();
    private static readonly TimeSpan BootstrapLifetime = TimeSpan.FromHours(6);
    private static readonly TimeSpan FailedBootstrapDelay = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan PlayerTimeout = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromSeconds(1)
    ];

    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _bootstrapGate = new(1, 1);
    private string _clientVersion = FallbackWebRemixVersion;
    private string? _visitorData;
    private DateTimeOffset _nextBootstrapAt = DateTimeOffset.MinValue;

    public AnonymousInnertubeClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public static AnonymousInnertubeClient Shared { get; } = new(SharedHttpClient);

    public string ClientVersion => _clientVersion;

    public string? VisitorData => _visitorData;

    public async Task<JsonElement> BrowseAsync(
        string browseId,
        string? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(browseId);

        JsonObject body = CreateWebRemixBody();
        body["browseId"] = browseId;
        if (!string.IsNullOrWhiteSpace(parameters))
        {
            body["params"] = parameters;
        }

        return await PostMusicAsync("browse", body, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<JsonElement> BrowseContinuationAsync(
        string continuation,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(continuation);

        JsonObject body = CreateWebRemixBody();
        body["continuation"] = continuation;
        Dictionary<string, string> query = new(StringComparer.Ordinal)
        {
            ["ctoken"] = continuation,
            ["continuation"] = continuation,
            ["type"] = "next"
        };

        return await PostMusicAsync("browse", body, query, cancellationToken).ConfigureAwait(false);
    }

    public async Task<JsonElement> SearchAsync(
        string query,
        string? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        JsonObject body = CreateWebRemixBody();
        body["query"] = query;
        if (!string.IsNullOrWhiteSpace(parameters))
        {
            body["params"] = parameters;
        }

        return await PostMusicAsync("search", body, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<JsonElement> SearchSuggestionsAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        JsonObject body = CreateWebRemixBody();
        body["input"] = input;
        return await PostMusicAsync(
            "music/get_search_suggestions",
            body,
            null,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<JsonElement> NextAsync(
        string videoId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoId);

        JsonObject body = CreateWebRemixBody();
        body["videoId"] = videoId;
        body["playlistId"] = $"RDAMVM{videoId}";
        body["isAudioOnly"] = true;
        return await PostMusicAsync("next", body, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<JsonElement> PlayerAsync(
        string videoId,
        InnertubePlayerClient playerClient,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoId);
        ArgumentNullException.ThrowIfNull(playerClient);

        await EnsureBootstrapAsync(cancellationToken).ConfigureAwait(false);

        JsonObject client = new()
        {
            ["clientName"] = playerClient.ClientName,
            ["clientVersion"] = playerClient.ClientVersion,
            ["hl"] = "en",
            ["gl"] = "US"
        };
        AddIfPresent(client, "osName", playerClient.OsName);
        AddIfPresent(client, "osVersion", playerClient.OsVersion);
        AddIfPresent(client, "deviceMake", playerClient.DeviceMake);
        AddIfPresent(client, "deviceModel", playerClient.DeviceModel);
        if (playerClient.AndroidSdkVersion is not null)
        {
            client["androidSdkVersion"] = playerClient.AndroidSdkVersion.Value;
        }
        AddIfPresent(client, "visitorData", _visitorData);

        JsonObject body = new()
        {
            ["context"] = new JsonObject
            {
                ["client"] = client
            },
            ["videoId"] = videoId,
            ["contentCheckOk"] = true,
            ["racyCheckOk"] = true
        };

        Uri apiBase = playerClient.UsesMusicHost
            ? new Uri(MusicApiBase, UriKind.Absolute)
            : new Uri(YouTubeApiBase, UriKind.Absolute);
        Uri endpoint = new(apiBase, "player?prettyPrint=false");

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(PlayerTimeout);

        JsonElement response = await SendJsonWithRetryAsync(
            endpoint,
            body,
            request => AddPlayerHeaders(request, playerClient),
            timeout.Token,
            cancellationToken).ConfigureAwait(false);

        string? status = response.GetString("playabilityStatus", "status");
        if (!string.IsNullOrEmpty(status) && !string.Equals(status, "OK", StringComparison.Ordinal))
        {
            string reason = response.GetString("playabilityStatus", "reason") ?? status;
            throw new InnertubePlayerException(reason, status);
        }

        return response;
    }

    public async Task RefreshBootstrapAsync(CancellationToken cancellationToken = default)
    {
        _nextBootstrapAt = DateTimeOffset.MinValue;
        await EnsureBootstrapAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<JsonElement> PostMusicAsync(
        string endpoint,
        JsonObject body,
        IReadOnlyDictionary<string, string>? query,
        CancellationToken cancellationToken)
    {
        await EnsureBootstrapAsync(cancellationToken).ConfigureAwait(false);
        UpdateWebContext(body);

        StringBuilder url = new(MusicApiBase);
        url.Append(endpoint)
           .Append("?prettyPrint=false");

        if (query is not null)
        {
            foreach ((string key, string value) in query)
            {
                url.Append('&')
                    .Append(Uri.EscapeDataString(key))
                    .Append('=')
                    .Append(Uri.EscapeDataString(value));
            }
        }

        JsonElement response = await SendJsonWithRetryAsync(
            new Uri(url.ToString(), UriKind.Absolute),
            body,
            AddWebRemixHeaders,
            cancellationToken,
            cancellationToken).ConfigureAwait(false);

        string? visitor = response.GetString("responseContext", "visitorData");
        if (!string.IsNullOrWhiteSpace(visitor))
        {
            _visitorData = visitor;
        }

        return response;
    }

    private async Task<JsonElement> SendJsonWithRetryAsync(
        Uri endpoint,
        JsonObject body,
        Action<HttpRequestMessage> addHeaders,
        CancellationToken requestToken,
        CancellationToken callerToken)
    {
        string json = body.ToJsonString();

        for (int attempt = 0; ; attempt++)
        {
            callerToken.ThrowIfCancellationRequested();
            using HttpRequestMessage request = new(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            addHeaders(request);

            try
            {
                using HttpResponseMessage response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    requestToken).ConfigureAwait(false);
                string responseBody = await response.Content.ReadAsStringAsync(requestToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new InnertubeHttpException(response.StatusCode, responseBody);
                }

                using JsonDocument document = JsonDocument.Parse(responseBody);
                return document.RootElement.Clone();
            }
            catch (HttpRequestException exception) when (
                exception.StatusCode is null && attempt < RetryDelays.Length)
            {
                await Task.Delay(RetryDelays[attempt], callerToken).ConfigureAwait(false);
            }
            catch (IOException) when (attempt < RetryDelays.Length)
            {
                await Task.Delay(RetryDelays[attempt], callerToken).ConfigureAwait(false);
            }
        }
    }

    private async Task EnsureBootstrapAsync(CancellationToken cancellationToken)
    {
        if (DateTimeOffset.UtcNow < _nextBootstrapAt)
        {
            return;
        }

        await _bootstrapGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (DateTimeOffset.UtcNow < _nextBootstrapAt)
            {
                return;
            }

            bool discovered = false;
            try
            {
                string shell = await GetTextWithRetryAsync(
                    new Uri(MusicOrigin + "/", UriKind.Absolute),
                    cancellationToken).ConfigureAwait(false);
                Match version = ClientVersionRegex().Match(shell);
                if (version.Success)
                {
                    _clientVersion = WebUtility.HtmlDecode(version.Groups[1].Value);
                    discovered = true;
                }

                Match visitor = VisitorConfigRegex().Match(shell);
                if (visitor.Success)
                {
                    _visitorData = visitor.Groups[1].Value;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
            }
            catch (HttpRequestException)
            {
            }
            catch (IOException)
            {
            }

            if (string.IsNullOrWhiteSpace(_visitorData))
            {
                try
                {
                    string bootstrap = await GetTextWithRetryAsync(
                        new Uri("https://www.youtube.com/sw.js_data", UriKind.Absolute),
                        cancellationToken).ConfigureAwait(false);
                    Match visitor = VisitorDataRegex().Match(bootstrap);
                    if (visitor.Success)
                    {
                        _visitorData = visitor.Value;
                        discovered = true;
                    }
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                }
                catch (HttpRequestException)
                {
                }
                catch (IOException)
                {
                }
            }

            _nextBootstrapAt = DateTimeOffset.UtcNow +
                (discovered ? BootstrapLifetime : FailedBootstrapDelay);
        }
        finally
        {
            _bootstrapGate.Release();
        }
    }

    private async Task<string> GetTextWithRetryAsync(Uri uri, CancellationToken cancellationToken)
    {
        for (int attempt = 0; ; attempt++)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("User-Agent", WebUserAgent);
            request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");

            try
            {
                using HttpResponseMessage response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken).ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new InnertubeHttpException(response.StatusCode, body);
                }

                return body;
            }
            catch (HttpRequestException exception) when (
                exception.StatusCode is null && attempt < RetryDelays.Length)
            {
                await Task.Delay(RetryDelays[attempt], cancellationToken).ConfigureAwait(false);
            }
            catch (IOException) when (attempt < RetryDelays.Length)
            {
                await Task.Delay(RetryDelays[attempt], cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private JsonObject CreateWebRemixBody()
    {
        JsonObject client = new()
        {
            ["clientName"] = "WEB_REMIX",
            ["clientVersion"] = _clientVersion,
            ["hl"] = "en",
            ["gl"] = "US"
        };
        AddIfPresent(client, "visitorData", _visitorData);

        return new JsonObject
        {
            ["context"] = new JsonObject
            {
                ["client"] = client,
                ["user"] = new JsonObject
                {
                    ["lockedSafetyMode"] = false
                },
                ["request"] = new JsonObject
                {
                    ["useSsl"] = true
                }
            }
        };
    }

    private void UpdateWebContext(JsonObject body)
    {
        JsonObject context = (JsonObject)body["context"]!;
        JsonObject client = (JsonObject)context["client"]!;
        client["clientVersion"] = _clientVersion;
        if (!string.IsNullOrWhiteSpace(_visitorData))
        {
            client["visitorData"] = _visitorData;
        }
    }

    private void AddWebRemixHeaders(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("X-Origin", MusicOrigin);
        request.Headers.TryAddWithoutValidation("Origin", MusicOrigin);
        request.Headers.Referrer = new Uri(MusicOrigin + "/", UriKind.Absolute);
        request.Headers.TryAddWithoutValidation("User-Agent", WebUserAgent);
        request.Headers.TryAddWithoutValidation("X-YouTube-Client-Name", WebRemixClientId);
        request.Headers.TryAddWithoutValidation("X-YouTube-Client-Version", _clientVersion);
        AddVisitorHeader(request);
    }

    private void AddPlayerHeaders(HttpRequestMessage request, InnertubePlayerClient playerClient)
    {
        request.Headers.TryAddWithoutValidation("User-Agent", playerClient.UserAgent);
        request.Headers.TryAddWithoutValidation("X-YouTube-Client-Name", playerClient.ClientId);
        request.Headers.TryAddWithoutValidation("X-YouTube-Client-Version", playerClient.ClientVersion);
        if (playerClient.Origin is not null)
        {
            request.Headers.TryAddWithoutValidation("Origin", playerClient.Origin);
            request.Headers.Referrer = new Uri(playerClient.Origin + "/", UriKind.Absolute);
        }
        AddVisitorHeader(request);
    }

    private void AddVisitorHeader(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_visitorData))
        {
            request.Headers.TryAddWithoutValidation("X-Goog-Visitor-Id", _visitorData);
        }
    }

    private static void AddIfPresent(JsonObject target, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target[name] = value;
        }
    }

    private static HttpClient CreateHttpClient()
    {
        SocketsHttpHandler handler = new()
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2)
        };
        HttpClient client = new(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    [GeneratedRegex("\\\"INNERTUBE_CLIENT_VERSION\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"")]
    private static partial Regex ClientVersionRegex();

    [GeneratedRegex("\\\"VISITOR_DATA\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"")]
    private static partial Regex VisitorConfigRegex();

    [GeneratedRegex("Cg[A-Za-z0-9_%-]{40,}")]
    private static partial Regex VisitorDataRegex();
}

public sealed record InnertubePlayerClient(
    string ClientName,
    string ClientVersion,
    string ClientId,
    string UserAgent,
    string? OsName = null,
    string? OsVersion = null,
    string? DeviceMake = null,
    string? DeviceModel = null,
    int? AndroidSdkVersion = null,
    string? Origin = null)
{
    public static readonly InnertubePlayerClient AndroidMusic = new(
        ClientName: "ANDROID_MUSIC",
        ClientVersion: "8.39.42",
        ClientId: "21",
        UserAgent: "com.google.android.apps.youtube.music/8.39.42 (Linux; U; Android 15; en_US; Pixel 9 Pro; Build/AP4A.250205.002) gzip",
        OsName: "Android",
        OsVersion: "15",
        DeviceMake: "Google",
        DeviceModel: "Pixel 9 Pro",
        AndroidSdkVersion: 35
    );

    public static readonly InnertubePlayerClient AndroidVr = new(
        ClientName: "ANDROID_VR",
        ClientVersion: "1.65.10",
        ClientId: "28",
        UserAgent: "com.google.android.apps.youtube.vr.oculus/1.65.10 (Linux; U; Android 12L; eureka-user Build/SQ3A.220605.009.A1) gzip",
        OsName: "Android",
        OsVersion: "12L",
        DeviceMake: "Oculus",
        DeviceModel: "Quest 3",
        AndroidSdkVersion: 32
    );

    public static readonly InnertubePlayerClient TvHtml5 = new(
        ClientName: "TVHTML5",
        ClientVersion: "7.20260707.12.00",
        ClientId: "85",
        UserAgent: "Mozilla/5.0 (ChromiumStylePlatform; Linux x86_64; Cobalt/24.lts.5.1034175-gold) Cobalt/24.lts.5.1034175-gold (unlike Gecko) Starboard/16, Unknown_Device_Platform_name/Unknown_Device_Model_name (Unknown, Unknown_Device_Brand_name)",
        Origin: "https://www.youtube.com"
    );

    public bool UsesMusicHost => string.Equals(
        Origin,
        "https://music.youtube.com",
        StringComparison.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> GetMediaHeaders()
    {
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase)
        {
            ["User-Agent"] = UserAgent
        };
        if (Origin is not null)
        {
            headers["Origin"] = Origin;
            headers["Referer"] = Origin + "/";
        }
        return headers;
    }
}

public sealed class InnertubeHttpException : HttpRequestException
{
    public InnertubeHttpException(HttpStatusCode statusCode, string responseBody)
        : base(
            $"Innertube returned HTTP {(int)statusCode} ({statusCode}).",
            null,
            statusCode)
    {
        ResponseBody = responseBody;
    }

    public string ResponseBody { get; }
}

public sealed class InnertubePlayerException : InvalidOperationException
{
    public InnertubePlayerException(string reason, string status)
        : base($"Track unavailable: {reason}")
    {
        Reason = reason;
        Status = status;
    }

    public string Reason { get; }

    public string Status { get; }
}

internal static class InnertubeClientJsonExtensions
{
    public static string? GetString(this JsonElement element, params string[] path)
    {
        JsonElement current = element;
        foreach (string name in path)
        {
            if (current.ValueKind != JsonValueKind.Object ||
                !current.TryGetProperty(name, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : current.ToString();
    }
}
