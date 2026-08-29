using System.Text.Json;

namespace BitChord.Core;

public sealed class BitChordService
{
    private readonly AnonymousInnertubeClient _client;

    public BitChordService(AnonymousInnertubeClient? client = null)
    {
        _client = client ?? AnonymousInnertubeClient.Shared;
    }

    public async Task<IReadOnlyList<HomeShelf>> GetHomeAsync(CancellationToken cancellationToken = default)
    {
        JsonElement response = await _client.BrowseAsync("FEmusic_home", cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return InnertubeParser.ParseHome(response).ToList();
    }

    public async Task<IReadOnlyList<HomeShelf>> GetExploreAsync(CancellationToken cancellationToken = default)
    {
        JsonElement exploreResponse = await _client.BrowseAsync("FEmusic_explore", cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        JsonElement chartsResponse = await _client.BrowseAsync("FEmusic_charts", cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        List<HomeShelf> shelves = new();
        shelves.AddRange(InnertubeParser.ParseHome(exploreResponse));
        shelves.AddRange(InnertubeParser.ParseHome(chartsResponse));

        return shelves
            .GroupBy(shelf => shelf.Title, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        SearchFilter filter,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<SearchResult>();
        }

        JsonElement response = await _client.SearchAsync(query, filter.GetParameters(), cancellationToken)
            .ConfigureAwait(false);

        return InnertubeParser.ParseSearch(response).ToList();
    }

    public async Task<IReadOnlyList<string>> GetSearchSuggestionsAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Array.Empty<string>();
        }

        JsonElement response = await _client.SearchSuggestionsAsync(input, cancellationToken)
            .ConfigureAwait(false);

        return InnertubeParser.ParseSearchSuggestions(response).ToList();
    }
}
