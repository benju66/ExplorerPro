# Context Menu Color Change Navigation Fix

## Issue Description

When users right-clicked on a file in the file tree to open the context menu and then changed the file's color, the file tree would unexpectedly navigate to the file's parent directory instead of staying in the current location.

## Root Cause Analysis

The issue was caused by a sequence of events triggered when opening context menus:

1. **Right-click on file**: Triggers `OnSelectedItemChanged` in `FileTreeEventManager`
2. **Selection Change**: Calls `ItemClicked?.Invoke(this, item.Path)` 
3. **OnItemClicked Handler**: In `FileTreeCoordinator.OnItemClicked()`, the logic changed `_currentFolderPath`:
   ```csharp
   if (Directory.Exists(path))
   {
       _currentFolderPath = path;
   }
   else
   {
       _currentFolderPath = Path.GetDirectoryName(path) ?? string.Empty;
   }
   ```
   For a file, this sets `_currentFolderPath` to the file's parent directory.

4. **Color Change**: User clicks color in context menu, calling `SetItemColor()` in `ContextMenuProvider`
5. **RefreshView Call**: `SetItemColor()` called `_fileTree.RefreshView()`
6. **Navigation**: `RefreshView()` called `SetRootDirectoryAsync(_currentFolderPath)` with the parent directory
7. **Result**: The view navigated to the parent directory

## Solutions Implemented

### 1. Fixed ContextMenuProvider.SetItemColor() Method

**Problem**: `RefreshView()` was changing directories unnecessarily.

**Solution**: Replace `RefreshView()` with targeted `RefreshDirectory()` calls:

```csharp
private void SetItemColor(string path, string colorHex)
{
    if (colorHex == null)
    {
        _metadataManager.SetItemColor(path, string.Empty);
    }
    else
    {
        _metadataManager.SetItemColor(path, colorHex);
        _metadataManager.AddRecentColor(colorHex);
    }
    
    // Use RefreshDirectory on the parent directory instead of RefreshView
    // to avoid changing the current root directory
    string directoryToRefresh = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(directoryToRefresh))
    {
        _fileTree.RefreshDirectory(directoryToRefresh);
    }
}
```

**Benefits**:
- Avoids changing the root directory
- Only refreshes the specific directory containing the changed item
- Preserves user's current navigation context

### 2. Optimized SetMultipleItemsColor() Method

**Problem**: Multiple calls to `SetItemColor()` caused multiple refresh operations.

**Solution**: Batch metadata updates and targeted directory refreshes:

```csharp
private void SetMultipleItemsColor(IReadOnlyList<string> paths, string colorHex)
{
    // Update metadata for all items first
    foreach (var path in paths)
    {
        if (colorHex == null)
        {
            _metadataManager.SetItemColor(path, string.Empty);
        }
        else
        {
            _metadataManager.SetItemColor(path, colorHex);
        }
    }
    
    // Add to recent colors only once if setting a color
    if (colorHex != null)
    {
        _metadataManager.AddRecentColor(colorHex);
    }
    
    // Collect unique directories to refresh
    var directoriesToRefresh = paths
        .Select(path => Directory.Exists(path) ? path : Path.GetDirectoryName(path))
        .Where(dir => !string.IsNullOrEmpty(dir))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
    
    // Refresh each unique directory once
    foreach (var directory in directoriesToRefresh)
    {
        _fileTree.RefreshDirectory(directory);
    }
}
```

**Benefits**:
- Reduces redundant metadata operations
- Minimizes UI refresh operations
- More efficient for bulk color changes

### 3. Improved FileTreeCoordinator.OnItemClicked() Logic

**Problem**: File selection was changing the current folder path, affecting navigation.

**Solution**: Only change folder path for directory clicks, not file selections:

```csharp
private void OnItemClicked(object sender, string path)
{
    if (_disposed || string.IsNullOrEmpty(path))
        return;

    // Only change the current folder path for directory clicks, not file selection
    // This prevents navigation changes when files are selected via context menu
    if (Directory.Exists(path))
    {
        _currentFolderPath = path;
        LocationChanged?.Invoke(this, path);
    }

    FileTreeClicked?.Invoke(this, EventArgs.Empty);
}
```

**Benefits**:
- Prevents unintended navigation when files are selected
- Maintains proper navigation behavior for directory clicks
- Reduces side effects from context menu operations

## Additional Fix: Drag and Drop Metadata Preservation

### Issue Description

When a file with color metadata was dragged and dropped to a different folder, the color (and other metadata like tags, pinned status) would be removed because the metadata system tracks items by their full file path.

### Root Cause

The `DragDropCommand` class handled the physical file move operation but never updated the metadata references from the old path to the new path, leaving the metadata associated with the non-existent old path.

### Solution: Enhanced DragDropCommand with Metadata Preservation

**Modified Constructor**: Added MetadataManager dependency:

```csharp
public DragDropCommand(IFileOperations fileOperations, IEnumerable<string> sourcePaths, 
    string targetPath, DragDropEffects effect, MetadataManager metadataManager = null)
{
    _fileOperations = fileOperations ?? throw new ArgumentNullException(nameof(fileOperations));
    _metadataManager = metadataManager ?? MetadataManager.Instance;
    // ... rest of constructor
}
```

**Enhanced Move Operations**: Added metadata transfer after successful file moves:

```csharp
private void ExecuteMove(DragDropOperation op)
{
    // ... existing file move logic ...
    
    // Transfer metadata (colors, tags, pinned status, etc.) from old path to new path
    try
    {
        _metadataManager.UpdatePathReferences(op.SourcePath, op.TargetPath);
    }
    catch (Exception ex)
    {
        // Don't fail the entire operation if metadata transfer fails
        System.Diagnostics.Debug.WriteLine($"[WARNING] Failed to transfer metadata for {op.SourcePath}: {ex.Message}");
    }
}
```

**Enhanced Copy Operations**: Added metadata copying for copy operations:

```csharp
private void ExecuteCopy(DragDropOperation op)
{
    // ... existing file copy logic ...
    
    // For copy operations, copy metadata to the new path (keep original metadata intact)
    try
    {
        var metadata = _metadataManager.GetBatchMetadata(new[] { op.SourcePath });
        if (metadata.TryGetValue(op.SourcePath, out var sourceMetadata))
        {
            // Copy the metadata to the new path
            sourceMetadata.Path = op.TargetPath;
            _metadataManager.SetBatchMetadata(new[] { sourceMetadata });
        }
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"[WARNING] Failed to copy metadata for {op.SourcePath}: {ex.Message}");
    }
}
```

**Enhanced Undo Operations**: Added metadata restoration for undo operations:

```csharp
private void UndoMove(DragDropOperation op)
{
    // Move back to original location
    if (op.WasDirectory)
    {
        Directory.Move(op.TargetPath, op.SourcePath);
    }
    else
    {
        File.Move(op.TargetPath, op.SourcePath);
    }
    
    // Restore metadata to original path
    try
    {
        _metadataManager.UpdatePathReferences(op.TargetPath, op.SourcePath);
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"[WARNING] Failed to restore metadata for {op.TargetPath}: {ex.Message}");
    }
}
```

### Additional Fix: FileTreeOperationHelper Rename

**Issue**: The `FileTreeOperationHelper.RenameSelected` method was directly calling `File.Move` without preserving metadata.

**Solution**: Modified to use `FileOperationHandler.RenameItem` which properly handles metadata preservation:

```csharp
public async void RenameSelected(string newName)
{
    // ... validation logic ...
    
    // Use the FileOperationHandler to ensure metadata preservation
    bool success = _fileOperationHandler.RenameItem(selectedPath, newName, _fileTree);
    
    // ... result handling ...
}
```

## Technical Details

### Files Modified

1. `UI/FileTree/ContextMenuProvider.cs`:
   - Modified `SetItemColor()` method
   - Modified `SetMultipleItemsColor()` method

2. `UI/FileTree/Coordinators/FileTreeCoordinator.cs`:
   - Modified `OnItemClicked()` method

3. `UI/FileTree/Commands/DragDropCommand.cs`:
   - Enhanced constructor to accept MetadataManager
   - Added metadata preservation to move operations
   - Added metadata copying to copy operations
   - Added metadata restoration to undo operations

4. `UI/FileTree/Services/FileTreeDragDropService.cs`:
   - Updated DragDropCommand instantiation to pass MetadataManager

5. `UI/FileTree/Helpers/FileTreeOperationHelper.cs`:
   - Modified `RenameSelected()` to use FileOperationHandler for metadata preservation

### Key Interfaces Used

- `IFileTree.RefreshDirectory(string directoryPath)`: Refreshes a specific directory without navigation
- `MetadataManager.UpdatePathReferences(string oldPath, string newPath)`: Transfers metadata between paths
- `MetadataManager.GetBatchMetadata()` and `SetBatchMetadata()`: Efficient metadata operations

### Performance Improvements

- Reduced unnecessary UI refreshes
- Eliminated redundant metadata operations in batch scenarios
- Minimized directory tree rebuilds
- Graceful error handling for metadata operations (operations don't fail if metadata transfer fails)

## Testing Recommendations

1. **Single File Color Change**: Right-click file → change color → verify no navigation occurs
2. **Multiple File Color Change**: Select multiple files → change color → verify no navigation occurs
3. **Directory Color Change**: Right-click folder → change color → verify proper refresh
4. **Mixed Selection Color Change**: Select files and folders → change color → verify proper behavior
5. **Navigation Verification**: Ensure normal directory clicking still works properly
6. **Drag and Drop Color Preservation**: 
   - Set a color on a file → drag to different folder → verify color is preserved
   - Set colors on multiple files → drag to different folder → verify all colors are preserved
   - Test with folders that have colors and other metadata
7. **Copy Operation Metadata**: Drag with Ctrl held → verify metadata is copied to new location
8. **Rename Operations**: Rename files/folders → verify metadata is preserved
9. **Undo Operations**: Test undo for drag/drop operations → verify metadata is properly restored

## Conclusion

The fixes address both the immediate symptoms (unwanted navigation, lost metadata) and the underlying performance issues. The solution provides:

- **Robust metadata preservation** across all file operations (move, copy, rename)
- **Better separation of concerns** between selection, metadata operations, and navigation
- **Graceful error handling** that doesn't break file operations if metadata operations fail
- **Performance optimizations** with batched operations and targeted refreshes
- **Comprehensive undo support** that properly restores both files and metadata

Files can now be moved between folders while retaining their colors, tags, pinned status, and other metadata, providing a consistent user experience. 