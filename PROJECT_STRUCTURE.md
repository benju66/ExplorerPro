# ExplorerPro - Project Structure

```
ExplorerPro/                              # Root directory - Enhanced WPF File Explorer application
├── .git/                                 # Git version control repository
├── .gitignore                            # Git ignore patterns for build artifacts and temp files
├── .vscode/                              # Visual Studio Code workspace settings
├── ExplorerPro.sln                       # Visual Studio solution file
├── ExplorerPro.csproj                    # .NET 9 WPF project configuration with dependencies
├── NuGet.Config                          # NuGet package source configuration
├── README.md                             # Project documentation and feature overview
├── AssemblyInfo.cs                       # Assembly metadata and versioning information
├── App.xaml                              # WPF application resource definitions and startup config
├── App.xaml.cs                           # Application entry point and initialization logic
├── TestWindow.xaml                       # Test/debugging window XAML definition
├── TestWindow.xaml.cs                    # Test window code-behind logic
│
├── Assets/                               # Static application resources
│   └── Icons/                            # Application icons and UI graphics
│       ├── app.ico                       # Main application icon (multiple sizes)
│       ├── app_16x16.ico                 # 16x16 application icon
│       ├── app_24x24.ico                 # 24x24 application icon
│       ├── app_32x32.ico                 # 32x32 application icon
│       ├── app_48x48.ico                 # 48x48 application icon
│       ├── pin.svg                       # Pin/favorite item icon
│       ├── star.svg                      # Star/rating icon
│       ├── link.svg                      # Link/hyperlink icon
│       └── list-todo.svg                 # Todo list icon
│
├── bin/                                  # Compiled binary output directory
├── obj/                                  # Intermediate build files and objects
│
├── Data/                                 # Application data storage
│   ├── settings.json                     # User preferences and application settings
│   ├── metadata.json                     # File metadata cache
│   ├── pinned_items.json                 # User-pinned files and folders
│   ├── notes.json                        # User notes and annotations
│   ├── tasks.json                        # Recurring tasks data
│   ├── recurrence.json                   # Task recurrence patterns
│   └── procore_links.json               # External links and integrations
│
├── FileOperations/                       # Core file system operations
│   ├── IFileOperations.cs                # File operations interface definition
│   ├── FileOperations.cs                 # Core file management implementation
│   ├── FileIconProvider.cs               # File type icon resolution
│   └── FileSystemWatcher.cs              # File system change monitoring
│
├── Models/                               # Data models and business logic
│   ├── Command.cs                        # Base command pattern implementation
│   ├── ConfigManager.cs                  # Configuration management
│   ├── MetadataManager.cs                # File metadata handling and caching
│   ├── PinnedManager.cs                  # Pinned items management
│   ├── RecurringTaskManager.cs           # Recurring tasks scheduling and execution
│   ├── SearchEngine.cs                   # File search and indexing engine
│   ├── SettingsManager.cs                # Application settings persistence
│   ├── UndoCommands.cs                   # Undoable command implementations
│   └── UndoManager.cs                    # Undo/redo operation management
│
├── Native/                               # Native library dependencies
│   └── pdfium.dll                        # PDF rendering native library
│
├── Themes/                               # UI theming and styling
│   ├── ThemeManager.cs                   # Theme switching and management logic
│   ├── BaseTheme.xaml                    # Base theme resource definitions
│   ├── LightTheme.xaml                   # Light theme color scheme and styles
│   └── DarkTheme.xaml                    # Dark theme color scheme and styles
│
├── UI/                                   # User interface components
│   ├── Controls/                         # Custom WPF controls
│   │   ├── PreviewHandlers/              # File preview control implementations
│   │   ├── DateEditControl.xaml          # Date picker control XAML
│   │   └── DateEditControl.xaml.cs       # Date picker control logic
│   │
│   ├── Converters/                       # WPF value converters for data binding
│   ├── Dialogs/                          # Modal dialogs and popup windows
│   ├── FileTree/                         # File tree navigation component
│   ├── MainWindow/                       # Primary application window
│   │   ├── MainWindow.xaml               # Main window layout and design
│   │   ├── MainWindow.xaml.cs            # Main window code-behind logic
│   │   ├── MainWindowContainer.xaml      # Main window container layout
│   │   ├── MainWindowContainer.xaml.cs   # Container logic and event handling
│   │   ├── MainWindowTabs.xaml           # Tabbed interface layout
│   │   └── MainWindowTabs.xaml.cs        # Tab management and switching logic
│   │
│   ├── Panels/                           # UI panel components (sidebar, properties, etc.)
│   ├── TabManagement/                    # Tab creation, switching, and management
│   └── Toolbar/                          # Application toolbar and menu components
│
├── Utilities/                            # Helper functions and extensions
│   ├── DateFormatter.cs                  # Date/time formatting utilities
│   ├── DragCopyCommand.cs                # Drag and drop operation handling
│   ├── Extensions.cs                     # C# extension methods and helpers
│   ├── FileSizeFormatter.cs              # File size display formatting
│   ├── IconProvider.cs                   # System icon extraction and caching
│   └── PathUtils.cs                      # File path manipulation utilities
│
└── Documentation/                        # Project documentation files
    ├── CRITICAL_FIXES_IMPLEMENTATION.md  # Critical bug fixes and implementation notes
    ├── ADDITIONAL_IMPROVEMENTS_NEEDED.md # Planned enhancements and feature requests
    ├── PERFORMANCE_OPTIMIZATIONS_COMPLETED.md # Completed performance improvements
    ├── PERFORMANCE_OPTIMIZATION_ANALYSIS.md # Performance analysis and recommendations
    ├── REFACTORING_COMPLETE.md           # Completed refactoring documentation
    ├── REFACTORING_NEXT_STEPS.md         # Planned refactoring tasks
    ├── REFACTORING_SUMMARY.md            # Refactoring overview and summary
    ├── ExplorerProStructure.txt          # Project structure overview
    └── structure.txt                     # Detailed structural documentation
```

## Project Overview

**ExplorerPro** is a modern, enhanced file explorer application built with WPF and .NET 9. It provides advanced file management capabilities including:

- **Tabbed Interface**: Multiple folder views in tabs
- **File Metadata Management**: Enhanced file properties and annotations
- **Pinned Items**: Quick access to frequently used files and folders
- **Search Engine**: Advanced file search and indexing
- **Preview Capabilities**: PDF and image file previews
- **Theme Support**: Light and dark themes
- **Recurring Tasks**: Automated file management tasks
- **Undo/Redo**: Full operation history with undo capabilities

## Key Technologies

- **Framework**: .NET 9 with WPF
- **UI Library**: MahApps.Metro for modern UI components
- **PDF Processing**: PdfiumViewer and PdfPig
- **Document Processing**: DocumentFormat.OpenXml for Office documents
- **Search**: FuzzySharp for fuzzy string matching
- **Architecture**: MVVM pattern with dependency injection 