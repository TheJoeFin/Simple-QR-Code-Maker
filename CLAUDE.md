# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Simple QR Code Maker is a WinUI 3 Windows desktop application (.NET 10, C#) for generating and decoding QR codes. It is published on the Microsoft Store and targets x64 and ARM64.

## Build Commands

The project uses MSBuild (not `dotnet build`) because it is a WinUI 3/MSIX packaged app. The solution file is `Simple QR Code Maker.slnx`. The default target platform is `arm64`.

```bash
# Restore NuGet packages
dotnet msbuild "Simple QR Code Maker.slnx" -t:Restore -p:Configuration=Release -p:Platform=arm64

# Build (Debug)
dotnet msbuild "Simple QR Code Maker.slnx" -p:Configuration=Debug -p:Platform=arm64

# Build (Release)
dotnet msbuild "Simple QR Code Maker.slnx" -p:Configuration=Release -p:Platform=arm64

# Build for x64
dotnet msbuild "Simple QR Code Maker.slnx" -p:Configuration=Release -p:Platform=x64
```

Visual Studio 2022 with the Windows App SDK workload and MSIX packaging tools is required. See `.vsconfig` for the exact required components.

## No Automated Tests

There are no unit or integration test projects. Testing is done manually using MSIX packages in `AppPackages/`.

## Architecture

The app follows **MVVM + Dependency Injection** using `CommunityToolkit.Mvvm` and `Microsoft.Extensions.Hosting`.

### Solution Structure

- **`Simple QR Code Maker/`** — Main WinUI 3 app project
- **`Simple QR Code Maker.Core/`** — Shared library (currently just `FileService` for JSON I/O)

### App Entry & DI

`App.xaml.cs` is the entry point and owns the DI container. All services, ViewModels, and Views are registered here using `Microsoft.Extensions.Hosting`. To add a new page/viewmodel, register both as `AddTransient<T>()` in `App.xaml.cs` and add the route in `PageService`.

Access services anywhere via `App.GetService<T>()`.

### Key Directories

| Directory | Purpose |
|-----------|---------|
| `Views/` | XAML pages (ShellPage, MainPage, DecodingPage, SettingsPage, FolderSummaryPage, SpreadsheetImportPage, AboutQrCodesWebPage) |
| `ViewModels/` | MVVM ViewModels with `[ObservableProperty]` and `[RelayCommand]` source generators |
| `Controls/` | Reusable custom XAML controls and icon controls |
| `Helpers/` | Core logic: `BarcodeHelpers`, `ImageProcessingHelper`, `PerspectiveCorrectionHelper`, `BackgroundRemovalHelper`, `HistoryStorageHelper`, `DecodingHistoryStorageHelper`, `BrandStorageHelper`, `UrlBuilderHelper`, `VCardBuilderHelper`, `WifiBuilderHelper`, `EmailBuilderHelper`, `CsvParser`, `ExcelSpreadsheetHelper`, `PrintLayoutHelper`, `QrCodeDesignStateMapper`, `QrFrameTextResolver`, `EmojiLogoHelper`, `ColorHelpers`, `ColorPickerListBuilder`, `ShareHelper`, and others |
| `Services/` | App-level services (navigation, theme, settings, activation) |
| `Contracts/` | Service interfaces |
| `Converters/` | XAML value converters for data binding |
| `Models/` | Data models (`HistoryItem`, `BarcodeImageItem`, etc.) |
| `Activation/` | App activation handlers (launch, share target) |

### Navigation

`ShellPage` is the navigation host. `NavigationService` and `PageService` manage page routing. Navigation is initiated from `ShellViewModel`.

### Cross-ViewModel Communication

Uses `CommunityToolkit.Mvvm.Messaging` with message types in `Models/`:
- `SaveHistoryMessage` — triggers history save
- `RequestShowMessage` — requests a dialog
- `RequestPaneChange` — toggles pane visibility
- `OpenFolderFileItemMessage` — opens a specific file item from the folder view
- `SelectDecodingImageItemMessage` — selects a decoding image item
- `IgnoreAutoBrandMessage` — suppresses automatic brand application

### Settings Persistence

`ILocalSettingsService` serializes settings to JSON at `%LocalAppData%\Simple_QR_Code_Maker\ApplicationData\LocalSettings.json`. Use `SettingsStorageExtensions` for typed read/write.

### JSON Serialization

Uses AOT-compatible source generation. There are multiple JSON contexts in `Models/`:
- `HistoryJsonContext.cs` — generation history types
- `DecodingHistoryJsonContext.cs` — decoding history types
- `BrandJsonContext.cs` — brand/design preset types
- `LocalSettingsJsonContext.cs` — local settings types

When adding new types to serialization, register them in the appropriate context.

### Key Dependencies

- **ZXing.Net** / **ZXing.Net.Bindings.Windows.Compatibility** — QR code generation and barcode decoding
- **Magick.NET-Q16-AnyCPU** — Image processing (grayscale, contrast, perspective correction)
- **CommunityToolkit.Mvvm** — `[ObservableProperty]`, `[RelayCommand]`, `WeakReferenceMessenger`
- **CommunityToolkit.WinUI.Controls.Primitives/Segmented/SettingsControls** — WinUI community controls
- **WinUIEx** — Extended window management and `WindowEx`
- **Microsoft.Graphics.Win2D** — 2D graphics rendering
- **Microsoft.Xaml.Behaviors.WinUI.Managed** — XAML behavior support
- **WinUI.TableView** — Table/grid view control
- **PDFsharp** — PDF generation for print layouts
- **Humanizer** — Human-readable string formatting
