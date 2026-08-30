# WinUI migration tracker

This document tracks the incremental migration from the Android app to the native WinUI 3 shell in this repo.

## Migration rule

This is a platform migration only. The frontend must remain visually and structurally identical to the Android app. No redesign, no product behavior changes, and no UX changes are allowed during the WinUI port; only the platform implementation changes.

## Current status

The WinUI port has completed its milestone: **Visual Fixes + Complete End-to-End Interactivity & Audio Playback Pipeline**.

All visual glitches (Chinese "英" font corruption, clipped bottom bar labels, broken hit-testing) have been fixed. Full audio streaming with `Windows.Media.Playback.MediaPlayer`, click-to-play card interaction, search result playback, and live mini-player controls are completely functional and building with **0 errors and 0 warnings**.

---

## Completed

### Visual & Font Repairs
- **FontIcon Glyphs Fixed**: Fixed Chinese character ("英") corruption across all cards, navigation bars, and search inputs by enforcing `Segoe Fluent Icons, Segoe MDL2 Assets` and removing conflicting implicit font bindings.
- **Library Icons Fixed**: Replaced raw string labels in `LibraryTile` with valid Segoe MDL2 / Fluent Unicode icon codepoints (`\uE896` Downloads, `\uEC4F` Local Music, `\uEB52` Liked Songs, `\uE90B` Playlists).
- **Floating Bottom Bar Geometry**: Fixed label clipping and hit-testing by adding `Grid.ColumnSpan="4"` and `IsHitTestVisible="False"` to the animated selection indicator, perfecting margins (`16,0,16,18`), and enabling instantaneous tab switching.
- **Theme & Title Bar**: Preserved light and dark theme dictionaries, smooth acrylic fade scrims, and window chrome.

### Interactive UI & Navigation
- **Click-to-Play on Feed Cards**: Hero cards and compact shelf cards in Listen Now and Explore are fully interactive with pointer cursor, hover states, and tap-to-play event routing.
- **Click-to-Play on Search Results**: Tapping any song or browse item in `SearchView` initiates audio streaming immediately.
- **Library Navigation**: On-Device tiles and "Your Replay" banner are interactive and navigate seamlessly.
- **Debounced Live Search & Filter Chips**: Live suggestions, full query execution, and interactive accent-highlighted filter chips (Songs / Albums / Artists / Playlists).

### Audio Engine & Mini Player
- **Windows Media Player Engine**: Integrated `Windows.Media.Playback.MediaPlayer` and `Windows.Media.Core.MediaSource` for high-fidelity audio playback.
- **Stream Resolver Pipeline**: Added `GetStreamUrlAsync` in `BitChordService` which automatically resolves direct, unciphered audio stream URLs from AndroidMusic, AndroidVr, and TvHtml5 Innertube endpoints.
- **Interactive Mini-Player Bar**: Frosted pill with artwork thumbnail, track/artist label, play/pause toggle with loading spinner (`ProgressRing`), and skip-next control.

### Launch & Build Pipeline
- `winui/launch.ps1`: Pure ASCII PowerShell script with `try/finally` and universal `Read-Host` pause that builds and launches the app reliably without exiting prematurely.
- **Build Status**: Verified clean build with **0 Warnings, 0 Errors**.

---

## Next Steps

1. Full-screen **Now Playing Sheet** (`NowPlayingScreen.kt` port with lyrics, queue reordering, and seek bar).
2. Detail page views for Album, Artist, and Playlist collections.
3. Offline caching and download manager integration for device storage.
