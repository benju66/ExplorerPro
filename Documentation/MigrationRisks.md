# Migration Risk Register

## High-Risk Items

| Risk | Probability | Impact | Mitigation Strategy |
|------|-------------|--------|---------------------|
| Tab detachment not available in Modern | High | High | Implement window management API for Modern; prototype and test first; keep feature flag to fall back to Chrome control.
| Cross-window transfer missing | High | High | Design cross-window coordination (shared registry of windows, drag-drop targets) before cutover; staged rollout.
| Direct `Items` manipulation replaced by service | Medium | High | Introduce adapter layer in MainWindow to translate old operations to `ITabManagerService` during transition.
| Event signature differences | High | Medium | Create event adapter mapping old events to Modern’s service events; update subscribers incrementally.

## Medium-Risk Items

| Risk | Probability | Impact | Mitigation Strategy |
|------|-------------|--------|---------------------|
| Multi-select behavior absent | Medium | Medium | Scope MVP without multi-select; schedule follow-up; or provide basic selection model first.
| Pinning behavior parity (insert rules) | Medium | Medium | Implement pin insertion logic in service; add unit tests for edge cases.
| Visual differences (styles/gradients) | Medium | Medium | Review `ModernTabStyles.xaml`; adjust to match expectations; user acceptance testing.
| Performance regressions in drag | Medium | Medium | Benchmark drag/drop and sizing updates; profile and tune manager algorithms.

## Low-Risk Items

| Risk | Probability | Impact | Mitigation Strategy |
|------|-------------|--------|---------------------|
| Middle-click close behavior missing | High | Low | Add routed behavior to Modern or command binding.
| Minor UI glitches | High | Low | QA sweep post-integration; fix as discovered.
| Memory leaks due to event wiring | Low | Medium | Ensure manager/service events are unsubscribed in `Dispose`; add smoke tests.

## Rollback Considerations

- Keep Chrome control path intact under a feature flag.
- Ability to swap controls in XAML by style key or conditional injection.
- Monitor telemetry around tab operations; instant rollback on elevated error rates.
