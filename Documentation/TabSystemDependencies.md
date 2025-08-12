# Tab System Dependencies

## Direct Dependencies (Reference ChromeStyleTabControl)

| Component | File | Dependency Type | Migration Impact |
|-----------|------|-----------------|------------------|
| MainWindow (code-behind) | `UI/MainWindow/MainWindow.xaml.cs` | Control host, event handling, tab operations | High — extensive direct usage of `MainTabs.Items`, selection, detachment, pinning.
| MainWindow (XAML) | `UI/MainWindow/MainWindow.xaml` | XAML control reference/name `MainTabs` | Medium — control swap and template updates required.
| ChromeStyleTabControl | `UI/Controls/ChromeStyleTabControl.cs` | Current tab control implementation | High — replaced by `ModernTabControl` with different architecture and events.
| TabOperationsManager | `Core/TabManagement/TabOperationsManager.cs` | Programmatic reorder/transfer/close | Medium — logic migrates to `ITabManagerService` and drag-drop manager.
| SimpleDetachedWindowManager | `Core/TabManagement/SimpleDetachedWindowManager.cs` | Detachment/reattachment/window mgmt | High — Modern lacks built-in detach; needs integration.
| Services using `MainTabs` | `Services/TabManagementService.cs` | Manipulates `newWindow.MainTabs` | Medium — replace with service-driven creation/transfer.

## Indirect Dependencies (Use tab functionality)

| Component | File | Interface Used | Migration Notes |
|-----------|------|----------------|-----------------|
| MainWindowContainer | `UI/MainWindow/MainWindowContainer.xaml.cs` (and related) | Hosts tab content | Low — content model independent; binding may need adjustments.
| ViewModels | `ViewModels/MainWindowTabsViewModel.cs`, `ViewModels/TabViewModel.cs`, `Models/TabModel.cs` | Tab data, titles, pinning | Medium — ensure binding aligns with Modern’s `ITabManagerService` data exposure.
| Pane/Toolbar | `UI/Toolbar/Toolbar.xaml.cs`, panels under `UI/Panels/*` | Commands/routes that act on active tab | Low/Medium — use Modern’s service to query active tab.

## Event Subscribers

| Event | Subscriber | File:Line | Purpose | Migration Required |
|------|------------|-----------|---------|-------------------|
| NewTabRequested | `MainWindow.OnNewTabRequested` | `UI/MainWindow/MainWindow.xaml.cs` L5757-L5785 | Create content and initialize | Yes — Modern uses service `TabCreated` events and VM.
| TabCloseRequested | `MainWindow.OnTabCloseRequested` | `UI/MainWindow/MainWindow.xaml.cs` L5790+ | Centralized close pipeline | Yes — Modern’s service raises `TabClosed`; adjust signature.
| TabDragged | `MainWindow.OnTabDragged` | `UI/MainWindow/MainWindow.xaml.cs` ~L5672-L5675 | Drag lifecycle hooks | Yes — Modern delegates via `ITabDragDropManager` events.
| TabMetadataChanged | `MainWindow.OnTabMetadataChanged` | `UI/MainWindow/MainWindow.xaml.cs` ~L5678-L5681 | Sync metadata changes | Likely — map to VM/property change notifications.
| SelectionChanged | `MainWindow.MainTabs_SelectionChanged` | `UI/MainWindow/MainWindow.xaml.cs` L3560-L3569 | Update UI on selection | Maybe — Modern still has selection change but model-driven.
| MouseDown (middle-click) | `MainWindow.OnTabControlMouseDown` | `UI/MainWindow/MainWindow.xaml.cs` L5694-L5752 | Middle-click close | Maybe — replicate via command or attach behavior.

## Additional References to `MainTabs` (illustrative)

- `UI/MainWindow/MainWindow.xaml.cs`: multiple direct accesses to `MainTabs.Items`, `MainTabs.SelectedItem`, inserts/adds (e.g., L3366-L3400, L3500-L3513, L3723-L3726, L3845-L3866).
- `Services/TabManagementService.cs`: uses `newWindow.MainTabs.Items.Clear()` and `Add` when creating a windowed tab.

Impact summary:
- Control swap requires replacing direct `Items` manipulation with `ITabManagerService` APIs and VM-binding.
- Detachment/transfer flows must be rethought under Modern (missing features).
