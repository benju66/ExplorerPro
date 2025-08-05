# TabModelResolver Integration Guide

## Quick Start (Day 1-3 Implementation)

### 1. Add Initialization to App.xaml.cs

Add the following to the `InitializeServices()` method in App.xaml.cs:

```csharp
private void InitializeServices()
{
    // ... existing service initialization ...
    
    // Initialize TabModelResolver with existing services
    ExplorerPro.Core.TabManagement.TabModelResolver.Initialize(
        _loggerFactory?.CreateLogger<ExplorerPro.Core.TabManagement.TabModelResolver>(),
        new ConsoleTelemetryService(), // Or your existing telemetry service
        ResourceMonitor,
        new SettingsService(Settings, _loggerFactory?.CreateLogger<SettingsService>())
    );
    
    Console.WriteLine("TabModelResolver initialized");
}
```

### 2. Update ChromeStyleTabControl.cs

Replace the existing `GetTabModel` method with:

```csharp
private TabModel GetTabModel(TabItem tabItem)
{
    // Use centralized resolver
    return TabModelResolver.GetTabModel(tabItem);
}
```

### 3. Update MainWindow.cs Tab Creation

In `CreateTabItem` method:

```csharp
private TabItem CreateTabItem(TabModel model)
{
    var tabItem = new TabItem
    {
        Content = model.Content
    };
    
    // Use resolver to set model properly
    TabModelResolver.SetTabModel(tabItem, model);
    
    // ... rest of the method
}
```

### 4. Feature Flag Configuration

Set environment variable to control feature:
```bash
# Enable (default)
set FF_USE_TAB_MODEL_RESOLVER=true

# Disable to rollback
set FF_USE_TAB_MODEL_RESOLVER=false
```

Or add to settings.json:
```json
{
  "FeatureFlags": {
    "UseTabModelResolver": true
  }
}
```

### 5. Monitoring Dashboard

Create a simple monitoring view in your logs or telemetry:

```csharp
// Add to a timer or diagnostic endpoint
var stats = TabModelResolver.GetStats();
_logger.LogInformation(
    "TabResolver Health: DataContext={DC}, TagFallback={TF}, FallbackRate={FR:F1}%",
    stats.DataContextHits,
    stats.TagFallbacks,
    stats.TagFallbackRate
);
```

### 6. Success Metrics

Monitor these KPIs after deployment:

1. **Tag Fallback Rate** - Should decrease over time as tabs migrate
   - Target: < 5% after 1 week
   - Alert if > 20%

2. **Migration Count** - Should spike initially then level off
   - Indicates successful automatic migration

3. **Not Found Count** - Should remain near zero
   - Alert if > 1% of total resolutions

### 7. Testing

Run manual tests:
```csharp
// In Program.cs or test runner
ExplorerPro.Tests.Manual.Phase1CriticalFixesManualTests.RunAllTests();
```

### 8. Rollback Plan

If issues arise:
1. Set `FF_USE_TAB_MODEL_RESOLVER=false`
2. Restart application
3. Monitor for stability
4. Investigate logs for root cause

## Integration Checklist

- [ ] Add TabModelResolver initialization to App.xaml.cs
- [ ] Update GetTabModel calls in ChromeStyleTabControl
- [ ] Update CreateTabItem in MainWindow
- [ ] Configure feature flag
- [ ] Deploy to 10% of users
- [ ] Monitor Tag fallback rate for 24 hours
- [ ] If stable (fallback rate < 20%), roll out to 50%
- [ ] After 3 days at 50%, roll out to 100%
- [ ] After 1 week, review migration success

## Common Issues & Solutions

### Issue: High Tag Fallback Rate
**Solution**: Check if new tabs are being created with old pattern. Search for `tab.Tag = model` and update to use `TabModelResolver.SetTabModel`.

### Issue: Null Reference Exceptions
**Solution**: Ensure TabModelResolver is initialized before any tab operations. Add null checks in GetTabModel usage.

### Issue: Performance Degradation
**Solution**: Check if migration is happening too frequently. Ensure each tab is only migrated once.

## Next Steps

After successful TabModelResolver deployment:
1. Week 2: Implement TabDisposalCoordinator
2. Week 3: Implement EventCleanupManager
3. Week 4: Full integration testing and performance validation