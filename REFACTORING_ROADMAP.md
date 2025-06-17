# Main Window Tabs Refactoring Roadmap

## Overview

This document outlines the step-by-step refactoring plan to transform the Main Window Tabs system from its current "God class" architecture to a clean, maintainable, service-based architecture that will support advanced features like drag-and-drop and detach/undock.

## Current State Analysis

### Issues Identified
- **MainWindow.xaml.cs is 6,500+ lines** - Violates Single Responsibility Principle
- **Multiple competing tab systems** - `ChromeStyleTabControl`, `TabItemModel`, `TabColorData`
- **Mixed MVVM and code-behind** patterns
- **Scattered logic** across multiple files
- **Direct UI manipulation** instead of binding
- **Tight coupling** between components

### Architecture Problems
1. Tab management spread across MainWindow, ViewModels, and Controls
2. Color handling in both ViewModels and code-behind
3. Event handling without clear ownership
4. No clear separation of concerns

## New Architecture (Already Implemented)

### Core Services ✅ DONE
- **`ITabManagerService`** - Core tab operations interface
- **`TabManagerService`** - Centralized tab management implementation
- **`TabModel`** - Unified tab data model (replaces multiple competing models)

### MVVM Layer ✅ DONE
- **`MainWindowTabsViewModel`** - Proper MVVM pattern for tab UI
- **Commands** - All tab operations as bindable commands
- **Events** - Clean event architecture with proper separation

### Benefits of New Architecture
- **Single Source of Truth** - All tab data flows through `TabManagerService`
- **Testability** - Services can be unit tested in isolation
- **Maintainability** - Clear separation of concerns
- **Extensibility** - Easy to add features like drag-and-drop
- **Performance** - Proper event handling and data binding

## Migration Plan

### Phase 1: Service Integration (NEXT STEPS)
**Goal**: Integrate the new services into MainWindow without breaking existing functionality

#### Step 1.1: Create Service Factory
- [ ] Create `TabServicesFactory` for dependency injection
- [ ] Wire up logging infrastructure
- [ ] Ensure proper disposal patterns

#### Step 1.2: Update MainWindow Constructor
- [ ] Inject `ITabManagerService` into MainWindow
- [ ] Initialize `MainWindowTabsViewModel`
- [ ] Set up data binding between ViewModel and UI

#### Step 1.3: Gradual Method Migration
- [ ] Migrate `CreateTab` methods to use service
- [ ] Migrate `CloseTab` methods to use service
- [ ] Migrate color management to use service
- [ ] Migrate pin/unpin functionality to use service

#### Step 1.4: Event Handler Migration
- [ ] Replace direct UI manipulation with ViewModel bindings
- [ ] Migrate context menu handlers to use Commands
- [ ] Update keyboard shortcuts to use Commands

### Phase 2: UI Binding Conversion
**Goal**: Convert from code-behind UI manipulation to proper data binding

#### Step 2.1: XAML Updates
- [ ] Update `MainWindowTabs.xaml` to bind to ViewModel
- [ ] Replace event handlers with Command bindings
- [ ] Implement proper data templates for tab visualization

#### Step 2.2: Remove Direct UI Manipulation
- [ ] Remove `SetTabColorDataContext` method
- [ ] Remove `ApplyColorBindingStyle` method
- [ ] Remove direct `TabBorder` property setting

#### Step 2.3: Hover Effects Integration
- [ ] Move hover effects to proper style triggers
- [ ] Remove code-behind hover handlers
- [ ] Use `TabModel.HasCustomColor` for binding

### Phase 3: Legacy Code Removal
**Goal**: Remove old tab management code and models

#### Step 3.1: Remove Obsolete Models
- [ ] Remove `TabItemModel` class
- [ ] Remove `TabColorData` class
- [ ] Update all references to use `TabModel`

#### Step 3.2: Remove Obsolete Methods
- [ ] Remove 6,000+ lines of tab management code from MainWindow
- [ ] Remove `TabManager.cs` (replaced by service)
- [ ] Remove scattered helper methods

#### Step 3.3: Clean Up ChromeStyleTabControl
- [ ] Update to work with new service architecture
- [ ] Remove duplicate functionality
- [ ] Ensure consistent styling

### Phase 4: Advanced Features Implementation
**Goal**: Implement drag-and-drop and detach/undock using the clean architecture

#### Step 4.1: Drag-and-Drop Service
- [ ] Create `ITabDragDropService`
- [ ] Implement drag preview generation
- [ ] Handle drop validation and execution
- [ ] Integrate with `TabManagerService`

#### Step 4.2: Window Management Service
- [ ] Create `ITabWindowService`
- [ ] Implement tab detachment to new windows
- [ ] Handle tab reattachment
- [ ] Manage cross-window tab transfers

#### Step 4.3: UI Components
- [ ] Add drag-and-drop visual feedback
- [ ] Implement detach/undock UI elements
- [ ] Create window docking zones

## Testing Strategy

### Unit Tests
- [ ] `TabManagerService` operations
- [ ] `TabModel` behavior and validation
- [ ] `MainWindowTabsViewModel` command execution
- [ ] Service integration scenarios

### Integration Tests
- [ ] Service lifecycle management
- [ ] Event propagation between layers
- [ ] MVVM binding functionality
- [ ] Drag-and-drop operations

### UI Tests
- [ ] Tab creation and closing
- [ ] Color customization
- [ ] Pin/unpin functionality
- [ ] Keyboard navigation

## Performance Considerations

### Memory Management
- [x] Proper disposal patterns in services ✅
- [x] Event handler cleanup ✅
- [ ] Tab hibernation integration
- [ ] Weak reference patterns for cross-window scenarios

### Event Handling
- [x] Centralized event architecture ✅
- [ ] Debounced UI updates
- [ ] Batch operations for multiple tab changes

## Migration Timeline

### Week 1: Foundation (CURRENT)
- [x] Create core services and interfaces ✅
- [x] Implement `TabModel` and `TabManagerService` ✅
- [x] Create `MainWindowTabsViewModel` ✅

### Week 2: Integration
- [ ] Phase 1: Service Integration
- [ ] Basic functionality working with new architecture
- [ ] Regression testing

### Week 3: UI Migration
- [ ] Phase 2: UI Binding Conversion
- [ ] Remove code-behind dependencies
- [ ] Style and template updates

### Week 4: Cleanup & Advanced Features
- [ ] Phase 3: Legacy Code Removal
- [ ] Phase 4: Begin drag-and-drop implementation
- [ ] Performance optimization

## Risk Mitigation

### Backwards Compatibility
- Maintain existing public APIs during migration
- Use adapter patterns where necessary
- Incremental rollout with feature flags

### Testing Coverage
- Comprehensive unit test suite before refactoring
- Integration tests for service interactions
- UI automation tests for regression prevention

### Performance Monitoring
- Memory usage tracking during migration
- Event handler count monitoring
- UI responsiveness metrics

## Success Metrics

### Code Quality
- **Lines of Code**: Reduce MainWindow from 6,500+ to <1,000 lines
- **Cyclomatic Complexity**: Reduce complexity score by 80%
- **Test Coverage**: Achieve 90%+ coverage for tab management

### Maintainability
- **Separation of Concerns**: Clear service boundaries
- **Single Responsibility**: Each class has one clear purpose
- **Loose Coupling**: Services can be tested in isolation

### Feature Readiness
- **Drag-and-Drop**: Ready for implementation
- **Detach/Undock**: Architecture supports cross-window operations
- **Extensibility**: Easy to add new tab features

## Next Steps

1. **START HERE**: Implement Phase 1.1 - Create Service Factory
2. Integrate services into MainWindow constructor
3. Begin gradual migration of existing methods
4. Update XAML for proper data binding
5. Remove legacy code incrementally

---

*This refactoring will transform the tab system from a maintenance nightmare into a clean, testable, and extensible architecture ready for advanced features.* 