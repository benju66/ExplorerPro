# Icon Resource Management System

## Overview
This directory contains the centralized icon resource management system for ExplorerPro. All application icons are defined in `IconResources.xaml` using a standardized naming convention.

## File Structure
```
UI/Resources/
├── IconResources.xaml     # Main icon resource dictionary
└── README_Icons.md        # This documentation file
```

## Naming Convention
Icons follow a hierarchical naming pattern: `Icon_{Category}_{Name}`

### Categories:
- **Navigation**: Toolbar navigation icons (arrows, refresh, undo/redo)
- **Panel**: Sidebar and panel toggle icons
- **Content**: Content-related icons (pin, star, link, todo)
- **Window**: Application and window icons

### Examples:
```xml
<!-- Navigation Icons -->
<BitmapImage x:Key="Icon_Navigation_ArrowUp" ... />
<BitmapImage x:Key="Icon_Navigation_Refresh" ... />

<!-- Panel Icons -->
<BitmapImage x:Key="Icon_Panel_Left" ... />
<BitmapImage x:Key="Icon_Panel_Right" ... />

<!-- Content Icons -->
<BitmapImage x:Key="Icon_Content_Pin" ... />
<BitmapImage x:Key="Icon_Content_Star" ... />
```

## Usage in XAML
```xml
<!-- Use the new standardized names -->
<Image Source="{StaticResource Icon_Navigation_Refresh}" Width="20" Height="20" />

<!-- Legacy names still work (backward compatibility) -->
<Image Source="{StaticResource RefreshIcon}" Width="20" Height="20" />
```

## Adding New Icons

### 1. Add the SVG file to `/Assets/Icons/`
### 2. Add the resource definition to `IconResources.xaml`:
```xml
<BitmapImage x:Key="Icon_{Category}_{Name}" UriSource="/Assets/Icons/your-icon.svg" />
```

### 3. (Optional) Add legacy alias for backward compatibility:
```xml
<StaticResource x:Key="YourOldIconName" ResourceKey="Icon_{Category}_{Name}" />
```

## Best Practices

### ✅ DO:
- Use the standardized naming convention for new icons
- Group icons by category in the resource file
- Add comments to separate categories
- Use descriptive names that indicate the icon's purpose
- Test icons at different sizes (16px, 20px, 24px)

### ❌ DON'T:
- Define icons directly in individual XAML files
- Use inconsistent naming patterns
- Remove legacy aliases without checking usage
- Use overly generic names like "Icon1" or "Button"

## Migration Notes
- All existing icon references continue to work through legacy aliases
- New code should use the standardized `Icon_{Category}_{Name}` format
- Legacy aliases can be gradually phased out in future updates

## Troubleshooting

### Icons not displaying?
1. Check that `IconResources.xaml` is included in `App.xaml` merged dictionaries
2. Verify the SVG file exists in `/Assets/Icons/`
3. Ensure the resource key matches exactly (case-sensitive)
4. Check that the SVG file is included as a Resource in the project file

### Performance considerations:
- Icons are loaded as `BitmapImage` resources (cached in memory)
- SVG files are embedded as resources in the compiled assembly
- No runtime file system access required for icon loading 