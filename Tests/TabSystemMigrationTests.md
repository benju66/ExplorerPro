# Tab System Migration Test Checklist

## Functional Tests

### Basic Operations
- [ ] Create new tab (via UI and programmatically)
- [ ] Close tab (X button)
- [ ] Close tab (middle-click)
- [ ] Switch between tabs updates address bar and toolbar
- [ ] Tab title updates on navigation and refresh
- [ ] Tab content displays correct container

### Advanced Operations
- [ ] Drag tab to reorder within window
- [ ] Drag tab to detach to new window (Chrome baseline only; Modern TBD)
- [ ] Transfer tab between windows (Chrome baseline only; Modern TBD)
- [ ] Multi-select tabs (Ctrl+Click) and perform close/move
- [ ] Context menu: close, detach, pin/unpin
- [ ] Pin/unpin tabs preserves ordering and protects from close

## Service/Manager Integration (Modern)
- [ ] `ITabManagerService` raises TabCreated/TabClosed/ActiveTabChanged
- [ ] `ITabDragDropManager` reorder raises MoveTab to service
- [ ] `ITabAnimationManager` hooks invoked on selection/add/remove
- [ ] Pinning logic enforced by service/VM

## Performance Tests
- [ ] Startup time with 1 tab
- [ ] Startup time with 10 tabs
- [ ] Tab creation speed (avg over 10 runs)
- [ ] Tab switching latency (UI thread idle within 16ms target)
- [ ] Memory usage per tab (steady state)
- [ ] CPU usage during drag

## Visual Tests
- [ ] Tab renders correctly in Light/Dark themes
- [ ] Hover effects and active highlighting
- [ ] Animations smooth with no hitches
- [ ] Insertion indicators visible and correct
- [ ] No visual glitches on detach/reattach

## Notes
- For Chrome baseline, use existing flows in `MainWindow` and `SimpleDetachedWindowManager` to validate expected behavior.
- For Modern, mark unsupported tests as pending and track in the roadmap.
