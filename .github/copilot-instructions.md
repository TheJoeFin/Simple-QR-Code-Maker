# Copilot Instructions for Simple QR Code Maker

## Build, test, and lint commands

- This is a WinUI 3 + MSIX-packaged desktop app. Use `dotnet msbuild`, not `dotnet build`.
- Local ARM64 debug build:

```powershell
dotnet msbuild "Simple QR Code Maker.slnx" /restore /p:Configuration=Debug /p:Platform=ARM64 /nologo
```

- x64 release build used by CI:

```powershell
dotnet msbuild "Simple QR Code Maker.slnx" /restore /p:Configuration=Release /p:Platform=x64 /nologo
```

- Visual Studio 2022 with the Windows App SDK workload and MSIX tooling is required; see `.vsconfig`.
- There is no automated test project in this repository, so there is no single-test command to run.
- There is no dedicated lint command configured in the repository.

## High-level architecture

- The solution has two projects: `Simple QR Code Maker` is the WinUI 3 app, and `Simple QR Code Maker.Core` contains shared infrastructure such as `FileService`.
- `App.xaml.cs` is the composition root. It builds a `Microsoft.Extensions.Hosting` host, registers services/view models/views, and routes startup through `IActivationService`.
- Activation is split between `DefaultActivationHandler` and `ShareTargetActivationHandler`. Shared images route into `DecodingViewModel`; shared text and URIs route into `MainViewModel`.
- `ShellPage` hosts navigation. `PageService` maps view-model keys to pages, and `NavigationService` performs frame navigation and forwards `INavigationAware` lifecycle calls.
- `MainViewModel` is the center of the app: it owns QR generation state, brand CRUD/default-brand behavior, history loading/saving, settings hydration, and navigation into decoding/settings/spreadsheet import flows.
- `DecodingViewModel` handles file picker, drag/drop, clipboard, and share-target decoding. Images are normalized with the image-processing helpers and Magick.NET before ZXing decoding, then decoded content can be sent back to `MainViewModel` as a `HistoryItem`.
- Persistence is split by concern:
  - `ILocalSettingsService` stores app settings.
  - `BrandStorageHelper` reads and writes `Brands.json`.
  - `HistoryStorageHelper` reads and writes `History.json`, performs migration from older settings-based storage, and uses temp-file rename writes to avoid truncating history on shutdown.
- Cross-view-model communication uses `WeakReferenceMessenger` messages such as `SaveHistoryMessage`, `RequestShowMessage`, and `RequestPaneChange`.
- Persisted models use source-generated JSON metadata (`HistoryJsonContext`, `BrandJsonContext`) plus custom color converters and serializer option helpers.

## Key conventions

- When adding a new page, wire it in two places: register the page and view model in `App.xaml.cs`, then add the `Configure<ViewModel, Page>()` mapping in `PageService`.
- Views resolve their view models with `App.GetService<T>()` in the page constructor; they are not created directly in XAML or by `new`.
- Navigation keys are `typeof(ViewModel).FullName!`, and navigation often passes typed state objects such as `HistoryItem`, `TitleBarSearchResult`, `StorageFile`, or a shared text string.
- View models use CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`) rather than hand-written property notification/command boilerplate.
- If you change persisted models, update the matching serializer options/context instead of relying on default `JsonSerializer` behavior.
- `HistoryItem` equality is based on `CodesContent`, and `BrandItem` equality is based on `Name` case-insensitively. That affects deduping and replace behavior when saving history or brands.
- Repo-wide formatting and style come from `.editorconfig`: CRLF line endings, 4-space indentation, file-scoped namespaces, `using` directives outside namespaces, explicit types instead of `var`, and modern collection expressions where they fit existing code.
