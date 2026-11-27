# Localization to-do

This source audit covers all seven routes in `PageService`, plus their dialogs, flyouts, shared controls, and Windows integrations. Paths below are relative to the `Simple QR Code Maker` app project unless another project is named.

The extraction is in source, and both pseudo locales can be generated for Debug. The remaining work is the manual desktop pass and then the pilot translations.

## Step 1 - Extract hard-coded copy into the English resource file

- [x] Use `Strings\en-us\Resources.resw` as the English catalog and `en-US` as the fallback. Use `x:Uid` with property-qualified keys for static XAML and `Helpers\ResourceExtensions.cs` for runtime strings.
- [x] Resource entire phrases, with translator comments for format placeholders. Keep `.resw` lookups at the app boundary; map typed data or reason codes from the Core and Windows projects to display strings.
- [x] Include tooltips, accessibility names, visible shortcut hints, dialog buttons, menu items, teaching tips, empty states, notifications, and picker descriptions. Preserve automation IDs, navigation tags, command parameters, enum values, settings and backup keys, extensions, and protocol tokens. Leave user content, URLs, diagnostics, legal text, and proper names unchanged.

### 1a. Shell and About

- [x] **Shell** (`Views\ShellPage.xaml`, `MainWindow.xaml.cs`, `ViewModels\ShellViewModel.cs`, `Helpers\ShareHelper.cs`): resource navigation, pane and Store labels, search subtitles, accessibility names, and share titles and feedback. Reuse `AppDisplayName` without changing navigation tags or the Store URL.
- [x] **About QR Codes** (`Views\AboutQrCodesWebPage.xaml`, `ViewModels\AboutQrCodesWebViewModel.cs`): resource the Back to Settings label and tooltip alongside the existing loading, error, reload, browser navigation, and accessibility resources. The Wikipedia article is not app copy.

### 1b. Make Codes and spreadsheet import

- [x] **Make Codes** (`Views\MainPage.xaml`, `ViewModels\MainViewModel.cs`): resource input, import, multiline, color, error-correction and preview controls, counts, teaching tips and operation feedback. Redirector actions no longer depend on visible warning text.
- [x] **Builders** (`Controls\UrlBuilderDialog.xaml`, `VCardBuilderDialog.xaml`, `EmailBuilderDialog.xaml`, `WifiBuilderDialog.xaml`, `GenerateIdsDialog.xaml`, and corresponding view models): resource field labels, examples, instructions, preview labels, security warnings, actions, and validation. Keep URL, `mailto:`, vCard, and `WIFI:` payload syntax and authentication tokens intact.
- [x] **Brands, colors, logo, and design** (`Controls\Brand*.xaml`, color, logo, design and background-removal controls): resource labels, emoji names, picker options, preset descriptions and new suggested captions. Use stable design preset IDs in automation. Preserve saved and user-entered captions.
- [x] **QR preview, save, share, and print** (`Controls\QrCodeImageControl.xaml`, `QrExportActionsControl.xaml`, `PrintSettingsDialog.xaml`, and `Models\BarcodeImageItem.cs`): resource picker and common-brand actions, sizing explanations, accessibility text, print settings and errors, and the PDF/print-job title. Keep QR data, format identifiers and file extensions invariant.
- [x] **Generation history** (`Controls\HistoryPaneContentControl.xaml`, `HistoryRowItem.xaml`): resource pane actions, empty states, content-kind labels and fallback summaries. Preserve saved content, brand names and timestamps.
- [x] **Spreadsheet Import** (`Views\SpreadsheetImportPage.xaml`, `ViewModels\SpreadsheetImportViewModel.cs`): resource options, generated-ID format labels, synthetic column headers, counts and feedback. Identify `id` from the source header, not the localized column name, and preserve workbook values and write-back tokens.

### 1c. Read Codes and folder summary

- [x] **Read Codes** (`Views\DecodingPage.xaml`, `ViewModels\DecodingViewModel.cs`): resource file/folder, paste, camera and snip actions, drop and loading states, image actions, no-codes guidance, and errors. Use a complete FAQ link label; preserve decoded text verbatim.
- [x] **Advanced decoding** (`Controls\AdvancedToolsUserControl.xaml`, `ViewModels\AdvancedToolsViewModel.cs`, `Views\DecodingPage.xaml.cs`): resource quick fixes and guided flows, point and step labels, actions, undo/redo hints, and statuses.
- [x] **Folder, images and decoding history panes** (`Controls\FolderPaneContentControl.xaml`, `CutOutImagesPaneContentControl.xaml`, `DecodingHistoryPaneContentControl.xaml`): resource actions, empty states, source names, counts and batch feedback. Cut-out export names use stable `cutoutN` and `regionN` suffixes.
- [x] **Folder Summary** (`Views\FolderSummaryPage.xaml`, `ViewModels\FolderSummaryViewModel.cs`): resource headings, open/save actions and the CSV picker description. Preserve the exported CSV header and the batch `.txt` no-result marker.

### 1d. Settings, FAQ, and Windows integration

- [x] **Settings** (`Views\SettingsPage.xaml`, `ViewModels\SettingsViewModel.cs`): resource settings groups, toggle states, summaries, version formatting, backup feedback and About. Keep settings and backup formats compatible; surface localized guidance while logging diagnostics in English.
- [x] **Feedback and licenses** (`Controls\FeedbackDialog.xaml`, `LicensesDialog.xaml`, `ViewModels\LicensesDialogViewModel.cs`): resource prompts, actions, the prefilled feedback subject, license labels and app-authored notes. Leave the support address, library names, repository URLs, and license bodies intact.
- [x] **Shared FAQ** (`Controls\FaqsContent.xaml`, `Models\FaqItem.cs`, `ViewModels\FaqsContentViewModel.cs`): resource localized titles, answers, search hints, and support text. Built-in shortcuts use stable topics; typed searches match the displayed titles and answers.
- [x] **Windows/MSIX** (`Package.appxmanifest`): resource package and app names, app description, tile short name, and the share-target name. Keep activation parameters and package identity unchanged.

### Scope boundaries

- `AdvancedToolsControl.xaml` is excluded from compilation. The active `AdvancedToolsUserControl.xaml` is resource-backed.
- Explorer's `uap3:Verb` captions are not listed as localizable by the Windows manifest schema. They're unchanged, as are generated filenames, report formats, and proper names.
- Windows picker/print UI, WinUI.TableView filter and sort controls, Humanizer relative dates, and the remote Wikipedia article are owned by Windows or their respective dependencies.
- PDF labels that need Windows font fallback are rendered as an image rather than selectable text. The QR image and payload are unchanged.

### Step 1 exit checks

- [x] Check XAML resource IDs, literal C# lookups, and composite format arguments against the English catalog.
- [x] Build the unsigned ARM64 Debug MSIX, confirm its manifest and PRI exclude pseudo locales, and pass the Core and Windows headless suites.

## Step 2 - Pseudolocalize

- [x] Generate expanded accented (`qps-ploc`) and mirrored RTL (`qps-plocm`) resources on demand. Neither generated catalog is checked in or included in ordinary builds.
- [x] Verify resource keys and format placeholder parity, both candidates and flow directions in the generated PRI, and `en-US` fallback for an unsupported language.
- [ ] Manually walk every page and dialog in both modes. Check clipping and wrapping, keyboard access keys and Narrator labels, URLs/paths in RTL, and new captions in previews, PNG, SVG and PDF. Check relative dates and number formatting under different Windows regional settings and verify the installed tile and share-target labels. No desktop automation or app launch was performed.

To build a local preview, run these commands from the repository root in PowerShell 7.4 or newer. Use `x64` on an x64 host, and change `PseudoLocale` to `qps-plocm` for the RTL pass.

```powershell
.\scripts\New-PseudoResources.ps1
dotnet msbuild "Simple QR Code Maker.slnx" /p:Configuration=Debug /p:Platform=ARM64 /p:PseudoLocale=qps-ploc /nologo
```

Preview output goes under `bin\pseudo\<locale>` and is isolated from regular builds. Deploy it in a development environment, then open it manually. To deploy from Visual Studio, start a fresh instance from a shell with `$env:PseudoLocale = 'qps-ploc'` (or `qps-plocm`) and deploy the matching Debug platform. This changes only the app's language override, not Windows language preferences. Exit the preview and deploy and open an ordinary build without `PseudoLocale` to clear the app override.

## Step 3 - Translate and ship

- [ ] Pilot French (`fr-FR`), Spanish (`es-ES`), and Japanese (`ja-JP`). Agree on terminology, add `Strings\<locale>\Resources.resw` catalogs, and update count formatting for each locale's plural rules. Verify key and placeholder parity, access keys, installed language selection, and fallback before widening coverage. Localize the Store listing separately.
