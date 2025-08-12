# Chrome vs Modern Tab System Comparison

## Feature Parity Matrix

| Feature | Chrome | Modern | Gap | Implementation Effort |
|---------|--------|--------|-----|----------------------|
| Basic tabs (create/close/select) | ✅ Full | ✅ Intended via `ITabManagerService` | Low | Low — wire `ITabManagerService` events and bindings.
| Drag reorder within window | ✅ Full (`TabOperationsManager`) | ⚠️ Manager-driven (`ITabDragDropManager` + service) | Behavior parity | Low/Medium — ensure service `MoveTabAsync` implemented.
| Tab detach to new window | ✅ Full (MainWindow + `SimpleDetachedWindowManager`) | ❌ None (logs only on detach) | Complete feature | High — needs window mgmt integration.
| Transfer between windows | ✅ Full (`TransferTab`) | ❌ None | Complete feature | High — requires multi-window coordination.
| Multi-select tabs | ✅ Present (state in control) | ❌ None | Complete feature | Medium — add selection model and UI.
| Middle-click close | ✅ Yes (routed) | ❌ Not specified | Feature gap | Low — add behavior to Modern or delegate to service.
| Animations/transitions | ✅ Custom Storyboards | ✅ Via `ITabAnimationManager` | Different approach | Low — ensure animation hooks called.
| Pin/unpin tabs | ✅ Yes (IsPinned, insert rules) | ⚠️ Count-aware sizing; no explicit pin manager | Behavioral nuances | Low/Medium — implement pin logic at service/VM level.
| Tab metadata | ✅ Yes (`TabMetadataChanged`) | ⚠️ Not explicit | Event gap | Medium — rely on VM change notifications.
| Overflow/auto-sizing | ✅ Helper + layout | ✅ `ITabSizingManager` | Different approach | Low — confirm config.
| Hover preview | Unknown | ❌ None | Potential feature | Medium — optional.

## Architecture Differences

| Aspect | Chrome | Modern | Migration Impact |
|--------|--------|--------|------------------|
| Structure | Monolithic control | Modular managers + service | Positive maintainability; refactor needed |
| Event handling | Direct events on control | Interface-based (services/managers) | Adapter layer required |
| Data binding | Mixed Items/Tag/DataContext | VM + service-driven | Move to binding-first |
| Detach/transfer | Implemented in control and helpers | Not implemented | High impact to port |
| Testing | Limited | Improved via service separation | Positive |

## Critical Gaps to Address Before Cutover

- Detach to new window: Implement window management and content transfer in Modern.
- Transfer across windows: Enable cross-window drop and reattachment.
- Multi-select behavior: Define model, input handling, and operations.
- Middle-click close: Add routed behavior or service command.
- Metadata change notifications: Establish VM-level events or adapters.

## Notes

- Modern control already exposes properties for theme, animations, and allows DI of managers — leverage this for cleaner responsibilities.
- Verify `ITabManagerService` provides parity methods: create/close/move/select/pin; extend if needed.
