using System.IO;
using System.Text.Json;
using BitChord.Core;
using Xunit;
using YoutubeExplode.Videos.Streams;

namespace BitChord.Core.Tests;

public class BackendTests
{
    [Fact]
    public void LrcParser_ShouldParseStandardAndWordSyncedLyrics()
    {
        string sampleLrc = """
            [00:12.50]Yeah, yeah, yeah
            [00:15.80]I've been on my own for long enough
            [00:19.20]Maybe you can show me how to love, maybe
            [00:25.00]
            [00:30.10]<00:30.10>I'm <00:30.50>going <00:30.90>through <00:31.20>withdrawals
            """;

        var lines = LrcLibClient.ParseLrc(sampleLrc);

        Assert.NotEmpty(lines);
        Assert.Equal(6, lines.Count);

        // Intro gap check (00:00.00)
        Assert.Equal(0, lines[0].TimeMs);
        Assert.True(lines[0].IsGap);

        // First lyric line check
        Assert.Equal(12500, lines[1].TimeMs);
        Assert.Equal("Yeah, yeah, yeah", lines[1].Text);

        // Gap check
        Assert.True(lines[4].IsGap);

        // Word-synced check
        var wordSyncedLine = lines[5];
        Assert.Equal("I'm going through withdrawals", wordSyncedLine.Text);
        Assert.NotNull(wordSyncedLine.Words);
        Assert.Equal(4, wordSyncedLine.Words.Count);
        Assert.Equal("I'm", wordSyncedLine.Words[0].Text);
    }

    [Fact]
    public async Task LrcLibClient_ShouldFetchRealLyricsForKnownSong()
    {
        var client = new LrcLibClient();
        var lyrics = await client.GetLyricsAsync("Blinding Lights", "The Weeknd", 200000);

        Assert.NotNull(lyrics);
        Assert.NotEmpty(lyrics);
        Assert.Contains(lyrics, l => l.Text.Contains("blinded by the lights", StringComparison.OrdinalIgnoreCase) ||
                                     l.Text.Contains("I've been on my own", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BitChordService_ShouldFetchExploreAndHomeShelves()
    {
        var service = new BitChordService();
        var home = await service.GetHomeAsync();
        Assert.NotEmpty(home);

        var explore = await service.GetExploreAsync();
        Assert.NotEmpty(explore);
    }

    [Fact]
    public async Task AudioStream_ShouldResolveAndCacheStream()
    {
        var yt = new YoutubeExplode.YoutubeClient();
        var manifest = await yt.Videos.Streams.GetManifestAsync("5qZQEq_C3vc");
        var audioStream = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();
        Assert.NotNull(audioStream);

        string tempPath = Path.Combine(Path.GetTempPath(), "test_5qZQEq_C3vc.m4a");
        await yt.Videos.Streams.DownloadAsync(audioStream, tempPath);

        Assert.True(File.Exists(tempPath));
        Assert.True(new FileInfo(tempPath).Length > 1 * 1024 * 1024);
        File.Delete(tempPath);
    }
}
