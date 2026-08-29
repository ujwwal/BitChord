# BitChord for WinUI 3

This directory contains the incremental native Windows port of BitChord.

## UI parity rule

The Android app is the visual source of truth. The WinUI port must preserve its existing layout, navigation, colors, typography, spacing, icon shapes, loading states, and interaction hierarchy. Platform APIs may differ, but migration work must not redesign the product.

The current shell ports:

- the four tabs in their existing order: Play, Explore, Library, Search;
- the floating, capped pill navigation bar and animated selection indicator;
- the frosted top and bottom treatments;
- the large in-page headings and fixed Search field;
- Home/Explore/Library loading states;
- the Library Replay and On Device entry points;
- the bundled SF Pro Display type scale and current light/dark color tokens.

Data loading and playback are not connected yet, so feed areas intentionally render the same skeleton state used by Android.

## Projects

- `src/BitChord.Core` — platform-neutral domain models and application logic.
- `src/BitChord.WinUI` — WinUI 3 application, controls, and views.

## Requirements

- Windows 10 version 1809 or newer
- Visual Studio with Windows application development tooling
- .NET 8 SDK

The app uses Windows App SDK 2.4 and is configured as a self-contained, unpackaged app.

## Build

Open `BitChord.WinUI.slnx` in Visual Studio, select `x64` or `ARM64`, and run `BitChord.WinUI`.

From a Developer PowerShell with the .NET SDK installed:

```powershell
dotnet restore .\BitChord.WinUI.slnx
dotnet build .\BitChord.WinUI.slnx -p:Platform=x64
```
