using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using ExplorerPro.Models;
using ExplorerPro.UI.FileTree.Commands;
using ExplorerPro.FileOperations;

namespace ExplorerPro.Tests
{
    /// <summary>
    /// Test class to verify that drag and drop operations preserve metadata correctly
    /// </summary>
    public class DragDropMetadataTest
    {
        private readonly string _testDirectory;
        private readonly MetadataManager _metadataManager;
        private readonly IFileOperations _fileOperations;

        public DragDropMetadataTest()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "ExplorerProDragDropTest");
            _metadataManager = new MetadataManager(Path.Combine(_testDirectory, "metadata.json"));
            _fileOperations = new FileOperations.FileOperations();
        }

        /// <summary>
        /// Runs all drag and drop metadata preservation tests
        /// </summary>
        public void RunAllTests()
        {
            try
            {
                SetupTestEnvironment();

                Console.WriteLine("=== Drag and Drop Metadata Preservation Tests ===");
                Console.WriteLine();

                TestMoveFileWithColor();
                TestMoveFileWithMultipleMetadata();
                TestCopyFileWithColor();
                TestMoveMultipleFilesWithMetadata();
                TestUndoMoveWithMetadata();
                
                Console.WriteLine("=== All Tests Completed ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test failed with exception: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                CleanupTestEnvironment();
            }
        }

        private void SetupTestEnvironment()
        {
            // Create test directories
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);

            Directory.CreateDirectory(_testDirectory);
            Directory.CreateDirectory(Path.Combine(_testDirectory, "source"));
            Directory.CreateDirectory(Path.Combine(_testDirectory, "target"));

            // Create test files
            File.WriteAllText(Path.Combine(_testDirectory, "source", "test1.txt"), "Test content 1");
            File.WriteAllText(Path.Combine(_testDirectory, "source", "test2.txt"), "Test content 2");
            File.WriteAllText(Path.Combine(_testDirectory, "source", "test3.txt"), "Test content 3");

            Console.WriteLine("Test environment setup completed.");
        }

        private void CleanupTestEnvironment()
        {
            try
            {
                if (Directory.Exists(_testDirectory))
                    Directory.Delete(_testDirectory, true);
                Console.WriteLine("Test environment cleaned up.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to cleanup test environment: {ex.Message}");
            }
        }

        private void TestMoveFileWithColor()
        {
            Console.WriteLine("Test 1: Move file with color metadata");

            var sourceFile = Path.Combine(_testDirectory, "source", "test1.txt");
            var targetDir = Path.Combine(_testDirectory, "target");
            var expectedTargetFile = Path.Combine(targetDir, "test1.txt");

            // Set color metadata
            var testColor = "#FF0000"; // Red
            _metadataManager.SetItemColor(sourceFile, testColor);

            // Verify color is set
            var colorBefore = _metadataManager.GetItemColor(sourceFile);
            if (colorBefore != testColor)
            {
                throw new Exception($"Failed to set initial color. Expected: {testColor}, Got: {colorBefore}");
            }

            // Perform drag and drop move
            var command = new DragDropCommand(_fileOperations, new[] { sourceFile }, targetDir, DragDropEffects.Move, _metadataManager);
            command.Execute();

            // Verify file was moved
            if (File.Exists(sourceFile))
            {
                throw new Exception("Source file still exists after move operation");
            }

            if (!File.Exists(expectedTargetFile))
            {
                throw new Exception("Target file does not exist after move operation");
            }

            // Verify color metadata was transferred
            var colorAfter = _metadataManager.GetItemColor(expectedTargetFile);
            if (colorAfter != testColor)
            {
                throw new Exception($"Color metadata not preserved. Expected: {testColor}, Got: {colorAfter}");
            }

            // Verify old path has no metadata
            var oldPathColor = _metadataManager.GetItemColor(sourceFile);
            if (!string.IsNullOrEmpty(oldPathColor))
            {
                throw new Exception($"Old path still has metadata: {oldPathColor}");
            }

            Console.WriteLine("✓ Move file with color metadata - PASSED");
        }

        private void TestMoveFileWithMultipleMetadata()
        {
            Console.WriteLine("Test 2: Move file with multiple metadata types");

            var sourceFile = Path.Combine(_testDirectory, "source", "test2.txt");
            var targetDir = Path.Combine(_testDirectory, "target");
            var expectedTargetFile = Path.Combine(targetDir, "test2.txt");

            // Set multiple metadata types
            var testColor = "#00FF00"; // Green
            var testTag = "important";
            _metadataManager.SetItemColor(sourceFile, testColor);
            _metadataManager.AddTag(sourceFile, testTag);
            _metadataManager.SetItemBold(sourceFile, true);
            _metadataManager.AddPinnedItem(sourceFile);

            // Perform drag and drop move
            var command = new DragDropCommand(_fileOperations, new[] { sourceFile }, targetDir, DragDropEffects.Move, _metadataManager);
            command.Execute();

            // Verify all metadata was transferred
            var colorAfter = _metadataManager.GetItemColor(expectedTargetFile);
            var tagsAfter = _metadataManager.GetTags(expectedTargetFile);
            var boldAfter = _metadataManager.GetItemBold(expectedTargetFile);
            var pinnedItems = _metadataManager.GetPinnedItems();

            if (colorAfter != testColor)
            {
                throw new Exception($"Color not preserved. Expected: {testColor}, Got: {colorAfter}");
            }

            if (!tagsAfter.Contains(testTag))
            {
                throw new Exception($"Tag not preserved. Expected: {testTag}, Got: {string.Join(",", tagsAfter)}");
            }

            if (!boldAfter)
            {
                throw new Exception("Bold status not preserved");
            }

            if (!pinnedItems.Contains(expectedTargetFile))
            {
                throw new Exception("Pinned status not preserved");
            }

            Console.WriteLine("✓ Move file with multiple metadata types - PASSED");
        }

        private void TestCopyFileWithColor()
        {
            Console.WriteLine("Test 3: Copy file with color metadata");

            var sourceFile = Path.Combine(_testDirectory, "source", "test3.txt");
            var targetDir = Path.Combine(_testDirectory, "target");
            var expectedTargetFile = Path.Combine(targetDir, "test3.txt");

            // Set color metadata
            var testColor = "#0000FF"; // Blue
            _metadataManager.SetItemColor(sourceFile, testColor);

            // Perform drag and drop copy
            var command = new DragDropCommand(_fileOperations, new[] { sourceFile }, targetDir, DragDropEffects.Copy, _metadataManager);
            command.Execute();

            // Verify file was copied (both should exist)
            if (!File.Exists(sourceFile))
            {
                throw new Exception("Source file does not exist after copy operation");
            }

            if (!File.Exists(expectedTargetFile))
            {
                throw new Exception("Target file does not exist after copy operation");
            }

            // Verify color metadata was copied
            var sourceColor = _metadataManager.GetItemColor(sourceFile);
            var targetColor = _metadataManager.GetItemColor(expectedTargetFile);

            if (sourceColor != testColor)
            {
                throw new Exception($"Source color changed. Expected: {testColor}, Got: {sourceColor}");
            }

            if (targetColor != testColor)
            {
                throw new Exception($"Target color not copied. Expected: {testColor}, Got: {targetColor}");
            }

            Console.WriteLine("✓ Copy file with color metadata - PASSED");
        }

        private void TestMoveMultipleFilesWithMetadata()
        {
            Console.WriteLine("Test 4: Move multiple files with different metadata");

            // Create additional test files
            var file1 = Path.Combine(_testDirectory, "source", "multi1.txt");
            var file2 = Path.Combine(_testDirectory, "source", "multi2.txt");
            File.WriteAllText(file1, "Multi test 1");
            File.WriteAllText(file2, "Multi test 2");

            var targetDir = Path.Combine(_testDirectory, "target");

            // Set different metadata for each file
            _metadataManager.SetItemColor(file1, "#FFFF00"); // Yellow
            _metadataManager.SetItemColor(file2, "#FF00FF"); // Magenta
            _metadataManager.AddTag(file1, "batch1");
            _metadataManager.AddTag(file2, "batch2");

            // Perform batch move
            var command = new DragDropCommand(_fileOperations, new[] { file1, file2 }, targetDir, DragDropEffects.Move, _metadataManager);
            command.Execute();

            // Verify metadata for both files
            var target1 = Path.Combine(targetDir, "multi1.txt");
            var target2 = Path.Combine(targetDir, "multi2.txt");

            var color1 = _metadataManager.GetItemColor(target1);
            var color2 = _metadataManager.GetItemColor(target2);
            var tags1 = _metadataManager.GetTags(target1);
            var tags2 = _metadataManager.GetTags(target2);

            if (color1 != "#FFFF00")
            {
                throw new Exception($"File1 color not preserved. Expected: #FFFF00, Got: {color1}");
            }

            if (color2 != "#FF00FF")
            {
                throw new Exception($"File2 color not preserved. Expected: #FF00FF, Got: {color2}");
            }

            if (!tags1.Contains("batch1"))
            {
                throw new Exception("File1 tag not preserved");
            }

            if (!tags2.Contains("batch2"))
            {
                throw new Exception("File2 tag not preserved");
            }

            Console.WriteLine("✓ Move multiple files with different metadata - PASSED");
        }

        private void TestUndoMoveWithMetadata()
        {
            Console.WriteLine("Test 5: Undo move operation preserves metadata");

            // Create test file
            var sourceFile = Path.Combine(_testDirectory, "source", "undo_test.txt");
            File.WriteAllText(sourceFile, "Undo test content");

            var targetDir = Path.Combine(_testDirectory, "target");
            var testColor = "#808080"; // Gray

            // Set metadata
            _metadataManager.SetItemColor(sourceFile, testColor);

            // Perform move
            var command = new DragDropCommand(_fileOperations, new[] { sourceFile }, targetDir, DragDropEffects.Move, _metadataManager);
            command.Execute();

            var targetFile = Path.Combine(targetDir, "undo_test.txt");

            // Verify move and metadata transfer
            if (!File.Exists(targetFile))
            {
                throw new Exception("Move operation failed");
            }

            var colorAfterMove = _metadataManager.GetItemColor(targetFile);
            if (colorAfterMove != testColor)
            {
                throw new Exception("Metadata not transferred during move");
            }

            // Perform undo
            command.Undo();

            // Verify undo restored file and metadata
            if (!File.Exists(sourceFile))
            {
                throw new Exception("Undo did not restore source file");
            }

            if (File.Exists(targetFile))
            {
                throw new Exception("Undo did not remove target file");
            }

            var colorAfterUndo = _metadataManager.GetItemColor(sourceFile);
            if (colorAfterUndo != testColor)
            {
                throw new Exception($"Undo did not restore metadata. Expected: {testColor}, Got: {colorAfterUndo}");
            }

            Console.WriteLine("✓ Undo move operation preserves metadata - PASSED");
        }

        /// <summary>
        /// Entry point for running the tests (call this method manually for testing)
        /// </summary>
        public static void RunTests()
        {
            var test = new DragDropMetadataTest();
            test.RunAllTests();
        }
    }
} 