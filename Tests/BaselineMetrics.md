# Baseline Metrics (ChromeStyleTabControl)

Record current performance numbers before any migration.

## How to Capture
- Build Release configuration; run app normally.
- Use a stopwatch or simple logging around tab operations if available.
- Observe Task Manager or Performance Monitor for memory/CPU; capture after steady state (10s).

## Metrics
- App startup time (cold): ____ ms
- App startup time (warm): ____ ms
- New tab creation time (avg of 10): ____ ms
- Close tab time (avg of 10): ____ ms
- Switch tab latency (avg of 20): ____ ms
- Memory usage with 1 tab (steady): ____ MB
- Memory usage with 10 tabs (steady): ____ MB
- CPU during drag (peak/avg): ____ % / ____ %

## Suggested Manual Steps
1. Launch app; start timer at splash; stop when main UI interactive.
2. Create 10 tabs sequentially and time each.
3. Switch through tabs rapidly; note responsiveness.
4. Perform long drag across window; monitor CPU.
5. Detach a tab; observe window creation latency.

## Notes
- Keep environment stable between runs (close background processes).
- If you have telemetry (`ConsoleTelemetryService`), consider adding temporary timing logs; remove after capture.
