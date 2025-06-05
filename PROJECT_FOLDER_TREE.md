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
│
├── UI/                                        # User Interface Components
│   ├── MainWindow/                           # Main application window
│   │   ├── MainWindow.xaml                   # Main window layout definition
│   │   ├── MainWindow.xaml.cs               # Main window logic and event handlers
│   │   ├── MainWindowContainer.xaml          # Container layout for main window
│   │   ├── MainWindowContainer.xaml.cs      # Container logic and management
│   │   ├── MainWindowTabs.xaml              # Tab management interface
│   │   └── MainWindowTabs.xaml.cs           # Tab functionality and navigation
│   ├── FileTree/                            # File tree view components
│   ├── Toolbar/                             # Application toolbar controls
│   ├── TabManagement/                       # Tab system components
│   ├── Panels/                              # Various UI panels
│   ├── Dialogs/                             # Modal dialogs and popups
│   ├── Converters/                          # XAML value converters
│   └── Controls/                            # Custom user controls
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
└── obj/                                     # Build intermediate files
```

## Project Overview

**ExplorerPro** is an enhanced file explorer application built with:
- **.NET 9.0** and **WPF** for the user interface
- **Modern tabbed interface** for better file management
- **Advanced features** including file metadata, pinned items, and recurring tasks
- **PDF and image preview** capabilities
- **Theming support** with light and dark modes
- **Search engine** with indexing for fast file discovery

## Key Dependencies
- MahApps.Metro (Modern WPF UI)
- PdfiumViewer.Updated (PDF rendering)
- DocumentFormat.OpenXml (Office document support)
- FuzzySharp (Smart search functionality)
- Newtonsoft.Json (Configuration management) 