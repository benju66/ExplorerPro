// UI/FileTree/Commands/DragDropCommand.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using ExplorerPro.Models;
using ExplorerPro.FileOperations;

namespace ExplorerPro.UI.FileTree.Commands
{
    /// <summary>
    /// Command for undoable drag and drop operations
    /// </summary>
    public class DragDropCommand : Command
    {
        #region Fields
        
        private readonly IFileOperations _fileOperations;
        private readonly List<DragDropOperation> _operations;
        private readonly DragDropEffects _effect;
        
        #endregion
        
        #region Constructor
        
        public DragDropCommand(IFileOperations fileOperations, IEnumerable<string> sourcePaths, string targetPath, DragDropEffects effect)
        {
            _fileOperations = fileOperations ?? throw new ArgumentNullException(nameof(fileOperations));
            _effect = effect;
            _operations = new List<DragDropOperation>();
            
            // Build operations list
            foreach (var sourcePath in sourcePaths)
            {
                var operation = new DragDropOperation
                {
                    SourcePath = sourcePath,
                    TargetDirectory = targetPath,
                    Effect = effect
                };
                
                // Calculate target path
                string fileName = Path.GetFileName(sourcePath);
                operation.TargetPath = Path.Combine(targetPath, fileName);
                
                _operations.Add(operation);
            }
        }
        
        #endregion
        
        #region Command Implementation
        
        public override void Execute()
        {
            var errors = new List<string>();
            
            foreach (var op in _operations)
            {
                try
                {
                    switch (_effect)
                    {
                        case DragDropEffects.Move:
                            ExecuteMove(op);
                            break;
                            
                        case DragDropEffects.Copy:
                            ExecuteCopy(op);
                            break;
                            
                        case DragDropEffects.Link:
                            ExecuteLink(op);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"{Path.GetFileName(op.SourcePath)}: {ex.Message}");
                    op.Success = false;
                }
            }
            
            if (errors.Any())
            {
                string errorMessage = "Some operations failed:\n" + string.Join("\n", errors);
                MessageBox.Show(errorMessage, "Drag/Drop Errors", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        public override void Undo()
        {
            // Process in reverse order
            foreach (var op in _operations.Where(o => o.Success).Reverse())
            {
                try
                {
                    switch (_effect)
                    {
                        case DragDropEffects.Move:
                            UndoMove(op);
                            break;
                            
                        case DragDropEffects.Copy:
                            UndoCopy(op);
                            break;
                            
                        case DragDropEffects.Link:
                            UndoLink(op);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to undo drag/drop: {ex.Message}");
                }
            }
        }
        
        #endregion
        
        #region Operation Methods
        
        private void ExecuteMove(DragDropOperation op)
        {
            // Check for conflicts
            if (File.Exists(op.TargetPath) || Directory.Exists(op.TargetPath))
            {
                op.TargetPath = GetUniqueTargetPath(op.TargetPath);
            }
            
            // Store original attributes for undo
            if (File.Exists(op.SourcePath))
            {
                op.WasDirectory = false;
                File.Move(op.SourcePath, op.TargetPath);
            }
            else if (Directory.Exists(op.SourcePath))
            {
                op.WasDirectory = true;
                Directory.Move(op.SourcePath, op.TargetPath);
            }
            else
            {
                throw new FileNotFoundException($"Source not found: {op.SourcePath}");
            }
            
            op.Success = true;
        }
        
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
        }
        
        private void ExecuteCopy(DragDropOperation op)
        {
            // Check for conflicts
            if (File.Exists(op.TargetPath) || Directory.Exists(op.TargetPath))
            {
                op.TargetPath = GetUniqueTargetPath(op.TargetPath);
            }
            
            if (File.Exists(op.SourcePath))
            {
                op.WasDirectory = false;
                File.Copy(op.SourcePath, op.TargetPath, false);
            }
            else if (Directory.Exists(op.SourcePath))
            {
                op.WasDirectory = true;
                CopyDirectory(op.SourcePath, op.TargetPath);
            }
            else
            {
                throw new FileNotFoundException($"Source not found: {op.SourcePath}");
            }
            
            op.Success = true;
        }
        
        private void UndoCopy(DragDropOperation op)
        {
            // Delete the copied item
            if (op.WasDirectory && Directory.Exists(op.TargetPath))
            {
                Directory.Delete(op.TargetPath, true);
            }
            else if (!op.WasDirectory && File.Exists(op.TargetPath))
            {
                File.Delete(op.TargetPath);
            }
        }
        
        private void ExecuteLink(DragDropOperation op)
        {
            // Create shortcut
            string linkPath = op.TargetPath;
            if (!linkPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                linkPath = Path.ChangeExtension(linkPath, ".lnk");
            }
            
            if (File.Exists(linkPath))
            {
                linkPath = GetUniqueTargetPath(linkPath);
            }
            
            op.TargetPath = linkPath;
            CreateShortcut(op.SourcePath, linkPath);
            op.Success = true;
        }
        
        private void UndoLink(DragDropOperation op)
        {
            // Delete the shortcut
            if (File.Exists(op.TargetPath))
            {
                File.Delete(op.TargetPath);
            }
        }
        
        #endregion
        
        #region Helper Methods
        
        private string GetUniqueTargetPath(string targetPath)
        {
            if (!File.Exists(targetPath) && !Directory.Exists(targetPath))
                return targetPath;
            
            string directory = Path.GetDirectoryName(targetPath);
            string nameWithoutExt = Path.GetFileNameWithoutExtension(targetPath);
            string extension = Path.GetExtension(targetPath);
            
            int counter = 1;
            string newPath;
            
            do
            {
                newPath = Path.Combine(directory, $"{nameWithoutExt} ({counter}){extension}");
                counter++;
            }
            while (File.Exists(newPath) || Directory.Exists(newPath));
            
            return newPath;
        }
        
        private void CopyDirectory(string sourcePath, string targetPath)
        {
            Directory.CreateDirectory(targetPath);
            
            // Copy files
            foreach (string file in Directory.GetFiles(sourcePath))
            {
                string destFile = Path.Combine(targetPath, Path.GetFileName(file));
                File.Copy(file, destFile, false);
            }
            
            // Copy subdirectories
            foreach (string dir in Directory.GetDirectories(sourcePath))
            {
                string destDir = Path.Combine(targetPath, Path.GetFileName(dir));
                CopyDirectory(dir, destDir);
            }
        }
        
        private void CreateShortcut(string targetPath, string shortcutPath)
        {
            // Use Windows Script Host to create shortcut
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            dynamic shell = Activator.CreateInstance(shellType);
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
            shortcut.Save();
            
            System.Runtime.InteropServices.Marshal.ReleaseComObject(shortcut);
            System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);
        }
        
        #endregion
        
        #region Nested Types
        
        private class DragDropOperation
        {
            public string SourcePath { get; set; }
            public string TargetDirectory { get; set; }
            public string TargetPath { get; set; }
            public DragDropEffects Effect { get; set; }
            public bool Success { get; set; }
            public bool WasDirectory { get; set; }
        }
        
        #endregion
    }
}