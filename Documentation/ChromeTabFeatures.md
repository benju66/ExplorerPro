# ChromeStyleTabControl Feature Inventory

## Core Features (Must Have)

| Feature | Usage (file:line) | Classification | Notes |
|--------|--------------------|----------------|-------|
| Basic tab creation | `UI/MainWindow/MainWindow.xaml.cs` L5757-L5785 (OnNewTabRequested) | Critical | Creates container, initializes file tree, assigns content, sets default title.
| Tab deletion (close) | `UI/MainWindow/MainWindow.xaml.cs` L3638-L3726 (CloseTab), L3732-L3799 (CloseCurrentTab) | Critical | Preserves last tab by refreshing to Home; checks `IsPinned` to prevent closing pinned tabs.
| Tab selection & switching | `UI/MainWindow/MainWindow.xaml.cs` L3560-L3569 (SelectionChanged handler) | Critical | Updates address bar and toolbar state on selection change.
| Close via middle-click | `UI/MainWindow/MainWindow.xaml.cs` L5694-L5752 (OnTabControlMouseDown) | Important | Middle-click closes tab with `CanClose` check; prevents last-tab close.
| New tab button (+) | `UI/Controls/ChromeStyleTabControl.cs` constructor and routed events; `NewTabRequested` wired in `MainWindow.xaml.cs` L5659-L5663 | Critical | New tab requests raised by control; handled in MainWindow.
| Tab title display/updates | `UI/MainWindow/MainWindow.xaml.cs` L5772-L5779 (title on create), L3666-L3677 (title updates when refreshing) | Critical | Title set at creation and updated during operations.

## Advanced Features (Currently Used)

| Feature | Usage (file:line) | Classification | Notes |
|--------|--------------------|----------------|-------|
| Drag-reorder within window | `UI/Controls/ChromeStyleTabControl.cs` L1187-L1195 → `Core/TabManagement/TabOperationsManager.cs` L34-L153 | Critical | Reorder computed by `CalculateDropIndex` and executed with animation/logging; selection preserved.
| Detach tab to new window | `UI/Controls/ChromeStyleTabControl.cs` L1198-L1221 (HandleDetachDrop), `UI/MainWindow/MainWindow.xaml.cs` L3460-L3533 (DetachTabToNewWindow), `Core/TabManagement/SimpleDetachedWindowManager.cs` L24-L102 | Critical | Fully implemented with content transfer, window creation/positioning, lifecycle tracking.
| Transfer tab between windows | `UI/Controls/ChromeStyleTabControl.cs` L1223-L1240 (HandleTransferDrop), `Core/TabManagement/TabOperationsManager.cs` L158-L207 | Important | Transfers `TabItem` between different `ChromeStyleTabControl` instances.
| Multi-tab selection (Ctrl+Click) | `UI/Controls/ChromeStyleTabControl.cs` L260-L266 (state fields) | Important | Internal state exists; full input handling not exhaustively reviewed; appears supported in control.
| Context menu operations | `UI/MainWindow/MainWindow.xaml.cs` L3409-L3417 (Close), L3432-L3453 (Detach), plus additional menu handlers throughout | Important | Uses `_contextMenuTab` with debounce and cleanup; integrates with pinning.
| Tab pinning | `UI/MainWindow/MainWindow.xaml.cs` L3335-L3361 (IsTabPinned), L3300-L3317 (pin toggle path) | Important | Pinned tabs inserted at correct position (L3366-L3400) and protected from closing.
| Tab metadata storage | `UI/Controls/ChromeStyleTabControl.cs` events; `Core/TabManagement/SimpleDetachedWindowManager.cs` L190-L191 (Metadata["SourceWindow"]) | Important | `TabModel.Metadata` used to track source window and other data; `TabMetadataChanged` event exists.
| Tab preview on hover | Unknown | Nice-to-have | Not conclusively found; there is `TabPreviewManager.cs` (Core) and drag preview windows (`_dragVisualWindow`, `_detachPreviewWindow`), but no explicit hover-preview.
| Tab animations/transitions | `UI/Controls/ChromeStyleTabControl.cs` L1264-L1299+ (animations), plus many Storyboard uses | Nice-to-have | Custom animations for drag, fade, reorder feedback.
| Tab overflow handling | `UI/Controls/ChromeTabSizingHelper.cs` (helper), sizing logic via width estimates in `TabOperationsManager` L395-L416 | Important | Helper present; evidence of width estimation and layout adjustments.
| Auto tab width sizing | `UI/Controls/ChromeTabSizingHelper.cs`, `UI/Controls/ChromeStyleTabControl.cs` layout updates | Important | Present via helper and layout refreshes.

## Event Handling

| Event | Declared In | Subscribed In | Purpose |
|------|-------------|---------------|---------|
| NewTabRequested | `UI/Controls/ChromeStyleTabControl.cs` (~L200-L240 region) | `UI/MainWindow/MainWindow.xaml.cs` L5659-L5663 | Create and initialize new tab content/container.
| TabCloseRequested | `UI/Controls/ChromeStyleTabControl.cs` (~L200-L240 region) | `UI/MainWindow/MainWindow.xaml.cs` L5666-L5669 | Centralized close pipeline; coordinates disposal and last-tab behavior.
| TabDragged | `UI/Controls/ChromeStyleTabControl.cs` (~L200-L240 region) | `UI/MainWindow/MainWindow.xaml.cs` L5672-L5675 | Drag lifecycle integration and UI updates.
| TabMetadataChanged | `UI/Controls/ChromeStyleTabControl.cs` L200-L203 | `UI/MainWindow/MainWindow.xaml.cs` L5678-L5681 | Sync/update consumers when model metadata changes.
| SelectionChanged | WPF TabControl event | `UI/MainWindow/MainWindow.xaml.cs` L3560-L3569 | Update address bar and toolbar state.
| MouseDown (middle-click) | Routed event | `UI/MainWindow/MainWindow.xaml.cs` L5694-L5752 | Close tab on middle-click with safety checks.

## Visual Features

| Visual Feature | Usage (file:line) | Classification | Notes |
|----------------|--------------------|----------------|-------|
| Insertion indicators | `UI/Controls/ChromeStyleTabControl.cs` L268 (`_insertionIndicator`) | Important | Visual feedback during drag.
| Drag preview window | `UI/Controls/ChromeStyleTabControl.cs` L269-L271 (`_detachPreviewWindow`, `_dragVisualWindow`) | Nice-to-have | Drag visuals.
| Active tab highlighting, hover effects, gradients | `Themes/ChromeTabStyles.xaml` (styles), control visual states | Nice-to-have | Present via theme resources; exact lines not enumerated here.

Notes:
- Where “~Lxxx” is listed, event declarations cluster in that region of `ChromeStyleTabControl.cs`.
- This inventory focuses on features exercised by `MainWindow` and core managers; auxiliary menus and panels hook into the same tab APIs.
