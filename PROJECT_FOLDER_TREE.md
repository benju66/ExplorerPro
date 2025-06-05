# ExplorerPro - Project Folder Tree

```
ExplorerPro/                                    # Enhanced File Explorer WPF Application
├── ExplorerPro.sln                            # Visual Studio solution file
├── ExplorerPro.csproj                         # .NET 9.0 WPF project file
├── NuGet.Config                               # NuGet package source configuration
├── .gitignore                                 # Git ignore rules
├── README.md                                  # Project overview and features
├── App.xaml                                   # WPF application definition and resources
├── App.xaml.cs                               # Application startup and configuration logic
├── AssemblyInfo.cs                           # Assembly metadata and version info
├── TestWindow.xaml                           # Development test window
├── TestWindow.xaml.cs                        # Test window code-behind
├── structure.txt                             # Project structure documentation
│
├── Documentation/                            # Project Documentation
│   ├── PROJECT_FOLDER_TREE.md               # This file - project structure overview
│   ├── CONTEXT_MENU_COLOR_FIX.md            # Context menu color fix documentation
│   ├── README_TreeView_Selection_Optimization.md  # TreeView selection optimization guide
│   ├── TREEVIEW_SELECTION_OPTIMIZATION_SUMMARY.md # TreeView optimization summary
│   ├── DEADLOCK_FIX_SUMMARY.md              # Deadlock fix implementation summary
│   ├── CRITICAL_FIXES_IMPLEMENTATION.md     # Critical fixes implementation notes
│   ├── ADDITIONAL_IMPROVEMENTS_NEEDED.md    # Additional improvements needed
│   ├── PERFORMANCE_OPTIMIZATIONS_COMPLETED.md # Completed performance optimizations
│   ├── PERFORMANCE_OPTIMIZATION_ANALYSIS.md # Performance optimization analysis
│   ├── REFACTORING_COMPLETE.md              # Refactoring completion summary
│   ├── REFACTORING_NEXT_STEPS.md            # Next steps for refactoring
│   └── REFACTORING_SUMMARY.md               # Refactoring summary
│
├── UI/                                        # User Interface Components
│   ├── MainWindow/                           # Main application window
│   │   ├── MainWindow.xaml                   # Main window layout definition
│   │   ├── MainWindow.xaml.cs               # Main window logic and event handlers
│   │   ├── MainWindowContainer.xaml          # Container layout for main window
│   │   ├── MainWindowContainer.xaml.cs      # Container logic and management
│   │   ├── MainWindowTabs.xaml              # Tab management interface
│   │   └── MainWindowTabs.xaml.cs           # Tab functionality and navigation
│   │
│   ├── FileTree/                            # File tree view components
│   │   ├── ImprovedFileTreeListView.xaml    # Enhanced file tree list view XAML
│   │   ├── ImprovedFileTreeListView.xaml.cs # Enhanced file tree list view logic
│   │   ├── FileTreeItem.cs                  # File tree item model and logic
│   │   ├── ContextMenuProvider.cs           # Context menu provider for file tree
│   │   ├── IFileTree.cs                     # File tree interface definition
│   │   ├── CustomFileSystemModel.cs         # Custom file system model
│   │   ├── SelectionRectangleAdorner.cs     # Selection rectangle visual adorner
│   │   ├── Converters.cs                    # File tree specific converters
│   │   ├── TreeViewItemExtensions.cs        # TreeView item extension methods
│   │   ├── FileTreeDemo.xaml                # File tree demo window XAML
│   │   ├── FileTreeDemo.xaml.cs             # File tree demo window logic
│   │   ├── Example_OptimizedTreeViewIntegration.cs # TreeView optimization example
│   │   │
│   │   ├── Managers/                        # File tree managers
│   │   │   ├── OptimizedTreeViewIndexer.cs  # Optimized tree view indexing
│   │   │   ├── FileTreeEventManager.cs      # File tree event management
│   │   │   ├── FileTreeLoadChildrenManager.cs # Children loading management
│   │   │   ├── OptimizedFileTreePerformanceManager.cs # Optimized performance manager
│   │   │   ├── FileTreePerformanceManager.cs # File tree performance management
│   │   │   ├── FileTreeUIEventManager.cs    # UI event management
│   │   │   ├── FileTreeColumnManager.cs     # Column management
│   │   │   └── TreeViewPerformanceOptimizationGuide.md # Performance optimization guide
│   │   │
│   │   ├── Services/                        # File tree services
│   │   │   ├── FileTreeService.cs           # Core file tree service
│   │   │   ├── IFileTreeService.cs          # File tree service interface
│   │   │   ├── FileTreeDragDropService.cs   # Drag and drop service
│   │   │   ├── IFileTreeDragDropService.cs  # Drag drop service interface
│   │   │   ├── FileTreeDragDropServiceAdapter.cs # Drag drop service adapter
│   │   │   ├── FileTreeCacheService.cs      # File tree caching service
│   │   │   ├── IFileTreeCache.cs            # Cache service interface
│   │   │   ├── FileTreeThemeService.cs      # Theme service for file tree
│   │   │   ├── SelectionService.cs          # Selection management service
│   │   │   └── OutlookDataExtractor.cs      # Outlook data extraction service
│   │   │
│   │   ├── Commands/                        # File tree commands
│   │   │   ├── FileOperationHandler.cs      # File operation command handler
│   │   │   └── DragDropCommand.cs           # Drag and drop command
│   │   │
│   │   ├── Helpers/                         # File tree helpers
│   │   │   └── FileTreeOperationHelper.cs   # File tree operation helper methods
│   │   │
│   │   ├── Utilities/                       # File tree utilities
│   │   │   └── VisualTreeHelper.cs          # Visual tree manipulation utilities
│   │   │
│   │   ├── Dialogs/                         # File tree dialogs
│   │   ├── Coordinators/                    # File tree coordinators
│   │   ├── Behaviors/                       # File tree behaviors
│   │   ├── Resources/                       # File tree resources
│   │   ├── DragDrop/                        # Drag and drop components
│   │   └── Examples/                        # File tree examples
│   │
│   ├── Toolbar/                             # Application toolbar controls
│   │   ├── Toolbar.xaml                     # Toolbar layout definition
│   │   └── Toolbar.xaml.cs                 # Toolbar logic and event handlers
│   │
│   ├── TabManagement/                       # Tab system components
│   │
│   ├── Panels/                              # Various UI panels
│   │   ├── ToDoPanel/                       # To-do panel components
│   │   │   ├── ToDoPanel.xaml               # To-do panel XAML layout
│   │   │   └── ToDoPanel.xaml.cs            # To-do panel logic
│   │   │
│   │   ├── PinnedPanel/                     # Pinned items panel
│   │   │   ├── PinnedPanel.xaml             # Pinned panel XAML layout
│   │   │   ├── PinnedPanel.xaml.cs          # Pinned panel logic
│   │   │   └── EventArgs.cs                 # Event arguments for pinned panel
│   │   │
│   │   ├── BookmarksPanel/                  # Bookmarks panel components
│   │   └── ProcoreLinksPanel/               # Procore links panel components
│   │
│   ├── Dialogs/                             # Modal dialogs and popups
│   │   ├── AddItemDialog.xaml               # Add item dialog XAML
│   │   ├── AddItemDialog.xaml.cs            # Add item dialog logic
│   │   ├── InputDialog.cs                   # Generic input dialog
│   │   ├── ManageRecurringItemsDialog.xaml  # Recurring items management dialog XAML
│   │   ├── ManageRecurringItemsDialog.xaml.cs # Recurring items dialog logic
│   │   ├── RecurringItemViewModel.cs        # Recurring item view model
│   │   ├── SettingsDialog.xaml              # Settings dialog XAML
│   │   ├── SettingsDialog.xaml.cs           # Settings dialog logic
│   │   ├── EditRecurrenceDialog.xaml        # Edit recurrence dialog XAML
│   │   ├── EditRecurrenceDialog.xaml.cs     # Edit recurrence dialog logic
│   │   ├── TextInputDialog.xaml             # Text input dialog XAML
│   │   ├── TextInputDialog.xaml.cs          # Text input dialog logic
│   │   ├── FilePropertiesDialog.xaml        # File properties dialog XAML
│   │   └── FilePropertiesDialog.xaml.cs     # File properties dialog logic
│   │
│   ├── Converters/                          # XAML value converters
│   │   └── CommonConverters.cs              # Common XAML converters
│   │
│   └── Controls/                            # Custom user controls
│       ├── DateEditControl.xaml             # Date edit control XAML
│       ├── DateEditControl.xaml.cs          # Date edit control logic
│       └── PreviewHandlers/                 # File preview handlers
│           ├── ImagePreviewControl.xaml     # Image preview control XAML
│           ├── ImagePreviewControl.xaml.cs  # Image preview control logic
│           ├── PdfPreviewControl.xaml       # PDF preview control XAML
│           └── PdfPreviewControl.xaml.cs    # PDF preview control logic
│
├── Models/                                   # Data Models and Business Logic
│   ├── MetadataManager.cs                   # File metadata handling and storage
│   ├── SettingsManager.cs                   # Application settings management
│   ├── SearchEngine.cs                      # File search and indexing engine
│   ├── PinnedManager.cs                     # Pinned items and favorites manager
│   ├── RecurringTaskManager.cs              # Scheduled task management
│   ├── UndoManager.cs                       # Undo/redo operation tracking
│   ├── UndoCommands.cs                      # Undo command implementations
│   ├── ConfigManager.cs                     # Configuration file management
│   └── Command.cs                           # Base command interface
│
├── FileOperations/                          # File System Operations
│   ├── IFileOperations.cs                  # File operations interface
│   ├── FileOperations.cs                   # Core file manipulation logic
│   ├── FileIconProvider.cs                 # File type icon resolution
│   └── FileSystemWatcher.cs                # File system change monitoring
│
├── Utilities/                               # Helper Functions and Extensions
│   ├── Extensions.cs                       # General extension methods
│   ├── PathUtils.cs                        # File path manipulation utilities
│   ├── IconProvider.cs                     # Icon loading and caching
│   ├── DateFormatter.cs                    # Date/time formatting utilities
│   ├── FileSizeFormatter.cs                # File size display formatting
│   └── DragCopyCommand.cs                  # Drag and drop operations
│
├── Themes/                                  # Application Theming
│   ├── ThemeManager.cs                     # Theme switching and management
│   ├── BaseTheme.xaml                      # Base theme styles and resources
│   ├── LightTheme.xaml                     # Light mode theme definition
│   └── DarkTheme.xaml                      # Dark mode theme definition
│
├── Assets/                                  # Static Resources
│   └── Icons/                              # Application icons and imagery
│       └── app.ico                         # Main application icon
│
├── Data/                                    # Application Data Files
│   ├── settings.json                       # User preferences and configuration
│   ├── metadata.json                       # File metadata cache
│   ├── pinned_items.json                   # Pinned files and folders
│   ├── notes.json                          # User notes and annotations
│   ├── tasks.json                          # Recurring tasks configuration
│   ├── recurrence.json                     # Task recurrence patterns
│   └── procore_links.json                  # External links and shortcuts
│
├── Native/                                  # Native Libraries
│   └── pdfium.dll                          # PDF rendering library (26MB)
│
├── Examples/                                # Sample files and demonstrations
├── bin/                                     # Build output directory
├── obj/                                     # Build intermediate files
├── .git/                                    # Git version control
└── .vscode/                                 # Visual Studio Code configuration
```

## Project Overview

**ExplorerPro** is an enhanced file explorer application built with:
- **.NET 9.0** and **WPF** for the user interface
- **Modern tabbed interface** for better file management
- **Advanced features** including file metadata, pinned items, and recurring tasks
- **PDF and image preview** capabilities
- **Theming support** with light and dark modes
- **Search engine** with indexing for fast file discovery
- **Optimized TreeView** with performance enhancements and selection optimization
- **Comprehensive drag-and-drop** support with file operations
- **Extensible architecture** with service-oriented design

## Key Dependencies
- MahApps.Metro (Modern WPF UI)
- PdfiumViewer.Updated (PDF rendering)
- DocumentFormat.OpenXml (Office document support)
- FuzzySharp (Smart search functionality)
- Newtonsoft.Json (Configuration management)

## Architecture Highlights
- **Service-oriented design** with clear separation of concerns
- **Performance-optimized TreeView** with indexing and caching
- **Comprehensive theming system** supporting light and dark modes
- **Modular UI components** with reusable panels and controls
- **Advanced file operations** with undo/redo support
- **Extensible preview system** for various file types 