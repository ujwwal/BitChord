using System.IO;
using System.Net.Http;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace BitChord.Core;

public sealed class AudioStreamCache
{
    private const int ChunkSize = 256 * 1024;
    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly YoutubeClient YtClient = new();
    private readonly string _cacheDirectory;

    public AudioStreamCache(string? customCacheDir = null)
    {
        _cacheDirectory = customCacheDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BitChord",
            "Cache",
            "Audio");

        Directory.CreateDirectory(_cacheDirectory);
    }

    public string? GetCachedFilePath(string videoId)
    {
        string[] candidates = [$"{videoId}.m4a", $"{videoId}.webm", $"{videoId}.mp4"];
        foreach (var name in candidates)
        {
            string path = Path.Combine(_cacheDirectory, name);
            if (File.Exists(path) && new FileInfo(path).Length > 100 * 1024)
            {
                return path;
            }
        }
        return null;
    }

    public async Task<string> DownloadAndCacheStreamAsync(
        string videoId,
        ResolvedStream? streamInfo = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string? existing = GetCachedFilePath(videoId);
        if (existing is not null)
        {
            AppLogger.Info($"Using existing cached audio file for {videoId}: {existing}");
            return existing;
        }

        string targetPath = Path.Combine(_cacheDirectory, $"{videoId}.m4a");
        string tempPath = Path.Combine(_cacheDirectory, $"{videoId}.m4a.tmp");

        try
        {
            AppLogger.Info($"Resolving and downloading deobfuscated audio stream for {videoId} via YoutubeExplode engine...");
            var manifest = await YtClient.Videos.Streams.GetManifestAsync(videoId, cancellationToken).ConfigureAwait(false);
            var audioStream = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();
            if (audioStream is null)
            {
                throw new InvalidOperationException($"No audio stream found in manifest for {videoId}");
            }

            AppLogger.Info($"Downloading stream: {audioStream.Container.Name} @ {audioStream.Bitrate.KiloBitsPerSecond:F0} kbps ({audioStream.Size.MegaBytes:F1} MB)...");

            var progressHandler = progress is not null
                ? new Progress<double>(p => progress.Report(p))
                : null;

            await YtClient.Videos.Streams.DownloadAsync(audioStream, tempPath, progressHandler, cancellationToken).ConfigureAwait(false);

            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
            File.Move(tempPath, targetPath);

            AppLogger.Info($"Audio track for {videoId} successfully cached ({new FileInfo(targetPath).Length / 1024} KB).");
            return targetPath;
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Primary stream download failed for {videoId}: {ex.Message}. Attempting fallback...");
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }

            if (streamInfo is not null)
            {
                return await DownloadChunkedFallbackAsync(videoId, streamInfo, progress, cancellationToken).ConfigureAwait(false);
            }
            throw;
        }
    }

    private async Task<string> DownloadChunkedFallbackAsync(
        string videoId,
        ResolvedStream streamInfo,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        string ext = streamInfo.MimeType.Contains("mp4", StringComparison.OrdinalIgnoreCase) ||
                     streamInfo.MimeType.Contains("m4a", StringComparison.OrdinalIgnoreCase)
            ? "m4a"
            : "webm";

        string targetPath = Path.Combine(_cacheDirectory, $"{videoId}.{ext}");
        string tempPath = Path.Combine(_cacheDirectory, $"{videoId}.{ext}.tmp");

        AppLogger.Info($"Downloading chunked stream fallback for {videoId} ({streamInfo.MimeType}) to {targetPath}...");

        var headers = streamInfo.MediaHeaders.Count > 0
            ? streamInfo.MediaHeaders
            : InnertubePlayerClient.ForStreamUrl(streamInfo.Url).GetMediaHeaders();

        try
        {
            using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read, 81920, useAsync: true))
            {
                long currentOffset = 0;
                long? totalLength = null;

                while (!cancellationToken.IsCancellationRequested)
                {
                    long rangeEnd = currentOffset + ChunkSize - 1;
                    if (totalLength.HasValue && rangeEnd >= totalLength.Value)
                    {
                        rangeEnd = totalLength.Value - 1;
                    }

                    using var request = new HttpRequestMessage(HttpMethod.Get, streamInfo.Url);
                    foreach (var (key, value) in headers)
                    {
                        request.Headers.TryAddWithoutValidation(key, value);
                    }
                    request.Headers.TryAddWithoutValidation("Range", $"bytes={currentOffset}-{rangeEnd}");

                    using var response = await SharedHttpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken).ConfigureAwait(false);

                    if ((int)response.StatusCode == 416)
                    {
                        break;
                    }

                    if (!response.IsSuccessStatusCode && (int)response.StatusCode != 206)
                    {
                        throw new HttpRequestException($"Chunk download failed with status {(int)response.StatusCode} ({response.ReasonPhrase}) at offset {currentOffset}");
                    }

                    if (!totalLength.HasValue && response.Content.Headers.ContentRange?.Length is long len)
                    {
                        totalLength = len;
                    }

                    byte[] chunkBuffer = new byte[64 * 1024];
                    int bytesReadInChunk = 0;
                    using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                    {
                        int r;
                        while ((r = await contentStream.ReadAsync(chunkBuffer, 0, chunkBuffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                        {
                            await fileStream.WriteAsync(chunkBuffer.AsMemory(0, r), cancellationToken).ConfigureAwait(false);
                            currentOffset += r;
                            bytesReadInChunk += r;

                            if (totalLength.HasValue && totalLength.Value > 0)
                            {
                                progress?.Report((double)currentOffset / totalLength.Value);
                            }
                        }
                    }

                    if (bytesReadInChunk == 0 || (totalLength.HasValue && currentOffset >= totalLength.Value))
                    {
                        break;
                    }
                }
            }

            if (new FileInfo(tempPath).Length < 100 * 1024)
            {
                throw new InvalidOperationException($"Cached audio file was too small ({new FileInfo(tempPath).Length} bytes)");
            }

            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
            File.Move(tempPath, targetPath);

            AppLogger.Info($"Audio stream for {videoId} successfully downloaded and cached via fallback ({new FileInfo(targetPath).Length / 1024} KB).");
            return targetPath;
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
            throw;
        }
    }
}
