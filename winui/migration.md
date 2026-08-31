# WinUI migration tracker

This document tracks the incremental migration from the Android app to the native WinUI 3 shell in this repo.

## Migration rule

This is a platform migration only. The frontend must remain visually and structurally identical to the Android app. No redesign, no product behavior changes, and no UX changes are allowed during the WinUI port; only the platform implementation changes.

## Current status

The WinUI port has completed:
1. **Live Album Artwork & Thumbnails**: Real YouTube Music artwork is loaded asynchronously via native XAML `Image` bindings on Hero cards, Compact shelf cards, Search results, and Mini-Player.
2. **Card & Row Hit-Testing (Click-to-Play Everywhere)**: Cards and search rows are wrapped in hit-testable containers (`Background="Transparent"`, `IsHitTestVisible="True"`) with reliable DataContext extraction, allowing clicks on images, text, and empty padding to trigger playback immediately.
3. **In-Process Audio Streaming Engine**: Solved Windows Media Foundation 403 Forbidden gating by retrieving streams with exact matching mobile headers and buffering via `MediaSource.CreateFromStream`.
4. **Responsive Desktop Shell**: Shelves and content containers stretch horizontally across the window, and floating navigation/player pills stay centered with responsive max-widths.
5. **Clean Compilation**: Verified clean build with **0 Warnings, 0 Errors**.

---

## Completed Milestones

### Core Backend & Innertube Integration
- Live Home shelves (`FEmusic_home`) and Explore charts (`FEmusic_explore`, `FEmusic_charts`) parsing.
- Song search parser prioritizing Track objects (`videoId`) over general browse pages.
- Unciphered audio stream resolution using direct `adaptiveFormats` audio extractors from Android and TV player clients.
- Thread-safe `DispatcherQueue` marshaling on `AppShellViewModel`.

### Shell and UI Parity
- Tab navigation (Listen Now, Explore, Library, Search) with smooth cubic easing animations.
- Hero-shelf landscape cards (16:10) and compact square shelf cards.
- Live debounced search with query execution and interactive filter chips (Songs, Albums, Artists, Playlists).
- On-Device Library tiles and "Your Replay" gradient hero banner.
- Mini-player pill floating above bottom navigation with live track info, loading spinner, and play/pause controls.
- Light and dark theme dictionaries matching Android color specifications.

### Launch Script
- `winui/launch.ps1`: Pure ASCII PowerShell script with universal `Read-Host` pause that stays alive on completion or failure.
- **Build Status**: Verified clean build with **0 Warnings, 0 Errors**.

---

## Next Steps

1. Full-screen **Now Playing Sheet** with lyrics, queue reordering, and seek bar.
2. Collection detail views (Albums, Artists, Playlists).
3. Local disk caching and download manager integration.
