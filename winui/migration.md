# WinUI migration tracker

This document tracks the incremental migration from the Android app to the native WinUI 3 shell in this repo.

## Migration rule

This is a platform migration only. The frontend must remain visually and structurally identical to the Android app. No redesign, no product behavior changes, and no UX changes are allowed during the WinUI port; only the platform implementation changes.

## Current status

The WinUI port is now at the shell-and-content-model stage: the app window, tab navigation, window chrome, theme tokens, loading states, and a shared data model layer are in place, and the tab views are now bound to richer content models instead of static placeholders. The remaining work is in connecting the actual Android behavior and data flow, especially the audio/media, local-library, and account-backed features that drive the app beyond the shell.

## Completed

### Shell and UI scaffolding
- WinUI app entry point and main window shell created.
- Four-tab layout implemented in the same order as the Android app: Play, Explore, Library, Search.
- Floating pill bottom navigation with animated selection indicator implemented.
- Frosted top and bottom fade overlays and title bar treatment added.
- Light and dark theme resource dictionaries created to match the Android visual language.
- SF Pro Display font assets wired into the app resources.
- Search tab focus behavior added so re-selecting Search focuses the search box.
- Loading skeleton placeholders created for the feed and library pages.

### Shared core layer
- Core record types for songs, search results, browse pages, home shelves, and stream metadata added.
- Basic `AnonymousInnertubeClient` bootstrap and request flow implemented for YouTube Music API calls.
- Search, browse, continuation, player, and next-track request helper methods added.
- `InnertubeParser` provides initial parsing for home shelves, search, browse pages, and queue data.

### Porting alignment
- Android app UI structure used as the source of truth for layout and hierarchy.
- The WinUI shell now has richer content-state modeling for feed, library, and search screens.
- The UI remains intentionally faithful to the Android product rather than redesigned into a new experience.

## In progress / not yet complete

### Real data binding
- Hooking the WinUI views up to the actual `MainViewModel`-style feed data flow from Android.
- Replacing the static skeleton screens with real home, explore, library, and search results.
- Mapping the Android shelf models to WinUI view models and list rendering.

### Playback and media stack
- No native playback engine is connected yet.
- Audio streaming, queue resolution, track metadata playback, and now-playing state are still missing.
- Background media/session handling, playback controls, and queue management still need to be ported.

### Library and account features
- Replay data, on-device folders, downloads, local library scans, and playlists still need WinUI integration.
- Google account state and sign-in flow are not yet implemented in the native app.
- Playlist creation/editing, deletion flows, and collection browsing remain to be ported.

### Search and browsing
- Real search suggestions and result rendering remain unconnected.
- Detail pages for albums, artists, playlists, and track browsing still need WinUI implementations.
- Pagination/continuation logic is only partially represented in the shared core layer.

### Polish and parity work
- Full card rendering parity with Android: artwork, gradients, cropping, hover states, gestures, and interaction behavior.
- Reproducing the exact content density, spacing, and typography from the Android screens.
- Animations, loading transitions, and state handling need closer parity checks.

## Remaining high-priority tasks

1. Define the WinUI app state model that mirrors the Android feed and library state.
2. Build a concrete browse/search/detail view layer driven by real JSON from the core client.
3. Port the playback and queue pipeline needed to drive the main player view.
4. Connect local library and download integration for the Library page.
5. Add settings/account persistence and Windows-specific runtime behaviors.
6. Validate the app in a Windows environment with a .NET SDK installed and an actual WinUI build.

## Blockers and notes

- Local verification is currently blocked in this environment because the .NET SDK is not installed here; the WinUI project cannot be built until the SDK is available on the machine.
- The current migration is intentionally "visual shell first" and does not yet claim product parity beyond the initial navigation and styling pass.

## Recommended next milestone

The next milestone should be: "connect real WinUI feed rendering + browse/search data flow without playback".

That will bring the app from the static shell to a working content-fed experience while keeping the migration incremental and reviewable.
