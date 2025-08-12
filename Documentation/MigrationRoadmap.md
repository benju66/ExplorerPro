# Tab System Migration Roadmap

## Phase 2 (This Phase) — Assessment and Planning
- Complete feature inventory and dependency map (done in this folder).
- Identify gaps and risks; define test plan and baseline metrics.

## Phase 3 — Critical Feature Porting (Week 1-2)
1. Implement detachment in Modern
   - Define window registry and APIs compatible with `SimpleDetachedWindowManager` semantics.
   - Enable detach via `ITabDragDropManager.DetachRequested` → service → window creation.
2. Implement transfer between windows
   - Cross-window drop detection and `MoveTabAsync` across instances; shared registry.
3. Provide middle-click close behavior
   - Routed behavior or attachable handler; respect last-tab and pin rules.
4. Establish metadata change flow
   - Replace `TabMetadataChanged` with VM/property-changed notifications.

## Phase 4 — Integration and A/B (Week 2)
1. Introduce feature flag `FeatureFlags.UseModernTabs`.
2. Wire `ModernTabControl` alongside Chrome (hidden) and validate bindings.
3. A/B testing window: dogfood internally; collect metrics and feedback.
4. Optimize animations/sizing with managers; address UI polish.

## Phase 5 — Full Cutover and Cleanup
1. Switch flag to Modern by default; keep rollback path.
2. Remove direct `Items` manipulations from `MainWindow` in favor of service calls.
3. Consolidate detachment/transfer logic under service; deprecate Chrome-specific helpers.

## Rollback Plan
- Keep Chrome control XAML and code-behind paths intact behind flag.
- Single-config toggle to revert; no data migration required.
- Monitor telemetry dashboards; pre-baked hotfix to revert.
