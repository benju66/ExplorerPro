// UI/FileTree/Services/OutlookDataExtractor.cs - Fixed for proper filename encoding
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ExplorerPro.UI.FileTree.Services
{
    /// <summary>
    /// Utility class for extracting files from Outlook drag operations with improved filename preservation
    /// </summary>
    public static class OutlookDataExtractor
    {
        // Outlook data format constants
        private const string CFSTR_FILEDESCRIPTOR = "FileGroupDescriptor";
        private const string CFSTR_FILECONTENTS = "FileContents";
        private const string CFSTR_FILEDESCRIPTORW = "FileGroupDescriptorW"; // Unicode version
        
        // Structure offset constants for file descriptor
        private const int FILEDESCRIPTOR_FILENAME_OFFSET = 76; // Offset to filename in FILEGROUPDESCRIPTOR structure
        private const int FILEDESCRIPTOR_FILENAME_LENGTH = 260; // Max filename length in FILEGROUPDESCRIPTOR

        /// <summary>
        /// Results of an Outlook extraction operation
        /// </summary>
        public class ExtractionResult
        {
            /// <summary>
            /// Gets whether the extraction was successful
            /// </summary>
            public bool Success { get; set; }
            
            /// <summary>
            /// Gets a list of extracted file paths
            /// </summary>
            public List<string> ExtractedFiles { get; set; }
            
            /// <summary>
            /// Gets any error message if the extraction failed
            /// </summary>
            public string ErrorMessage { get; set; }
            
            /// <summary>
            /// Gets the stage of extraction that was completed
            /// </summary>
            public string Stage { get; set; }
            
            /// <summary>
            /// Gets the total number of files processed
            /// </summary>
            public int TotalFiles { get; set; }
            
            /// <summary>
            /// Gets the number of files that were skipped during extraction
            /// </summary>
            public int FilesSkipped { get; set; }
            
            /// <summary>
            /// Gets the number of errors that occurred during extraction
            /// </summary>
            public int Errors { get; set; }
            
            /// <summary>
            /// Default constructor for ExtractionResult
            /// </summary>
            public ExtractionResult()
            {
                ExtractedFiles = new List<string>();
            }
            
            /// <summary>
            /// Creates a successful result
            /// </summary>
            public static ExtractionResult CreateSuccess(List<string> extractedFiles, int totalFiles, string stage)
            {
                return new ExtractionResult
                {
                    Success = true,
                    ExtractedFiles = extractedFiles ?? new List<string>(),
                    TotalFiles = totalFiles,
                    Stage = stage,
                    FilesSkipped = 0,
                    Errors = 0
                };
            }
            
            /// <summary>
            /// Creates a failure result
            /// </summary>
            public static ExtractionResult CreateFailure(string errorMessage, string stage)
            {
                return new ExtractionResult
                {
                    Success = false,
                    ExtractedFiles = new List<string>(),
                    ErrorMessage = errorMessage,
                    Stage = stage,
                    FilesSkipped = 0,
                    Errors = 1
                };
            }
        }
        
        /// <summary>
        /// Extracts files from Outlook data object asynchronously
        /// </summary>
        /// <param name="dataObject">The data object containing Outlook data</param>
        /// <param name="targetPath">Target directory to save files</param>
        /// <returns>Result of the extraction process</returns>
        public static async Task<ExtractionResult> ExtractOutlookFilesAsync(IDataObject dataObject, string targetPath)
        {
            // Execute the synchronous method on a background thread
            return await Task.Run(() => ExtractOutlookFiles(dataObject, targetPath)).ConfigureAwait(false);
        }
        
        /// <summary>
        /// Extracts files from Outlook data object asynchronously with progress reporting
        /// </summary>
        /// <param name="dataObject">The data object containing Outlook data</param>
        /// <param name="targetPath">Target directory to save files</param>
        /// <param name="progress">Progress callback function</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of the extraction process</returns>
        public static async Task<ExtractionResult> ExtractOutlookFilesAsync(
            IDataObject dataObject, 
            string targetPath, 
            IProgress<string> progress, 
            CancellationToken cancellationToken)
        {
            // Report initial progress if progress callback is provided
            progress?.Report("Starting Outlook extraction...");
            
            // Execute on a background thread
            return await Task.Run(() => {
                try
                {
                    // Validate parameters
                    if (dataObject == null || string.IsNullOrEmpty(targetPath) || !Directory.Exists(targetPath))
                    {
                        progress?.Report("Validation failed: Invalid parameters");
                        return ExtractionResult.CreateFailure("Invalid arguments for Outlook extraction", "Validation");
                    }
                    
                    // Check for cancellation
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return ExtractionResult.CreateFailure("Operation cancelled by user", "Initialization");
                    }
                    
                    progress?.Report("Analyzing Outlook data...");
                    
                    // Try the Unicode descriptor version first (more reliable for international characters)
                    if (dataObject.GetDataPresent(CFSTR_FILEDESCRIPTORW))
                    {
                        progress?.Report("Found Unicode file descriptors, extracting attachments...");
                        return ExtractWithFileDescriptor(dataObject, targetPath, true, progress, cancellationToken);
                    }
                    
                    // Then try the standard FileGroupDescriptor
                    if (dataObject.GetDataPresent(CFSTR_FILEDESCRIPTOR))
                    {
                        progress?.Report("Found file descriptors, extracting attachments...");
                        return ExtractWithFileDescriptor(dataObject, targetPath, false, progress, cancellationToken);
                    }
                    
                    // Check for cancellation
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return ExtractionResult.CreateFailure("Operation cancelled by user", "Format detection");
                    }
                    
                    // Fallback to checking individual formats
                    progress?.Report("Trying alternative extraction methods...");
                    foreach (string format in dataObject.GetFormats())
                    {
                        // Check for cancellation
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return ExtractionResult.CreateFailure("Operation cancelled by user", "Format iteration");
                        }
                        
                        try
                        {
                            if (format.StartsWith("FileContents", StringComparison.OrdinalIgnoreCase))
                            {
                                progress?.Report($"Found format: {format}, extracting...");
                                return ExtractSingleFile(dataObject, format, targetPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[WARN] Error checking format {format}: {ex.Message}");
                        }
                    }
                    
                    progress?.Report("No suitable formats found for extraction");
                    return ExtractionResult.CreateFailure("No supported Outlook data formats found", "Format detection");
                }
                catch (COMException comEx)
                {
                    string errorMessage = $"COM error: {comEx.Message} (HRESULT: 0x{comEx.HResult:X})";
                    progress?.Report($"Error: {errorMessage}");
                    return ExtractionResult.CreateFailure(errorMessage, "COM interaction");
                }
                catch (Exception ex)
                {
                    progress?.Report($"Error: {ex.Message}");
                    return ExtractionResult.CreateFailure($"Error: {ex.Message}", "General extraction");
                }
            }, cancellationToken);
        }
        
        /// <summary>
        /// Extracts files from Outlook data object using Windows Explorer-like approach to preserve original filenames
        /// </summary>
        /// <param name="dataObject">The data object containing Outlook data</param>
        /// <param name="targetPath">Target directory to save files</param>
        /// <returns>Result of the extraction process</returns>
        public static ExtractionResult ExtractOutlookFiles(IDataObject dataObject, string targetPath)
        {
            try
            {
                if (dataObject == null || string.IsNullOrEmpty(targetPath) || !Directory.Exists(targetPath))
                {
                    return ExtractionResult.CreateFailure("Invalid arguments for Outlook extraction", "Validation");
                }
                
                System.Diagnostics.Debug.WriteLine("[INFO] Beginning Outlook attachment extraction");
                
                // Try the Unicode descriptor version first (more reliable for international characters)
                if (dataObject.GetDataPresent(CFSTR_FILEDESCRIPTORW))
                {
                    return ExtractWithFileDescriptor(dataObject, targetPath, true, null, CancellationToken.None);
                }
                
                // Then try the standard FileGroupDescriptor
                if (dataObject.GetDataPresent(CFSTR_FILEDESCRIPTOR))
                {
                    return ExtractWithFileDescriptor(dataObject, targetPath, false, null, CancellationToken.None);
                }
                
                // Fallback to checking individual formats
                foreach (string format in dataObject.GetFormats())
                {
                    try
                    {
                        if (format.StartsWith("FileContents", StringComparison.OrdinalIgnoreCase))
                        {
                            return ExtractSingleFile(dataObject, format, targetPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WARN] Error checking format {format}: {ex.Message}");
                    }
                }
                
                System.Diagnostics.Debug.WriteLine("[WARN] No supported Outlook data formats found");
                return ExtractionResult.CreateFailure("No supported Outlook data formats found", "Format detection");
            }
            catch (COMException comEx)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] COM error extracting Outlook files: {comEx.Message} (HRESULT: 0x{comEx.HResult:X})");
                return ExtractionResult.CreateFailure($"COM error: {comEx.Message} (HRESULT: 0x{comEx.HResult:X})", "COM interaction");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Error extracting Outlook files: {ex.Message}");
                return ExtractionResult.CreateFailure($"Error: {ex.Message}", "General extraction");
            }
        }

        /// <summary>
        /// Extracts Outlook attachments using the FileGroupDescriptor format (preserves original filenames)
        /// </summary>
        private static ExtractionResult ExtractWithFileDescriptor(
            IDataObject dataObject, 
            string targetPath,
            bool useUnicode,
            IProgress<string> progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Get descriptor data as memory stream
                MemoryStream descriptorStream = null;
                string descriptorFormat = useUnicode ? CFSTR_FILEDESCRIPTORW : CFSTR_FILEDESCRIPTOR;
                var descriptorData = dataObject.GetData(descriptorFormat);
                
                if (descriptorData is MemoryStream memStream)
                {
                    descriptorStream = memStream;
                }
                else if (descriptorData is byte[] dataBytes)
                {
                    descriptorStream = new MemoryStream(dataBytes);
                }
                else
                {
                    string error = $"Unexpected descriptor data type: {descriptorData?.GetType()}";
                    System.Diagnostics.Debug.WriteLine($"[WARN] {error}");
                    progress?.Report($"Error: {error}");
                    return ExtractionResult.CreateFailure(error, "Descriptor parsing");
                }
                
                // Check for cancellation
                if (cancellationToken.IsCancellationRequested)
                {
                    return ExtractionResult.CreateFailure("Operation cancelled by user", "Before descriptor parsing");
                }
                
                // Now read the file descriptors which contain the original filenames
                return ReadAndExtractFiles(descriptorStream, dataObject, targetPath, useUnicode, progress, cancellationToken);
            }
            catch (Exception ex)
            {
                string error = $"Failed to extract with file descriptor: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[ERROR] {error}");
                progress?.Report($"Error: {error}");
                return ExtractionResult.CreateFailure(error, "Descriptor extraction");
            }
        }

        /// <summary>
        /// Reads file descriptors and extracts files with original filenames
        /// </summary>
        private static ExtractionResult ReadAndExtractFiles(
            MemoryStream descriptorStream, 
            IDataObject dataObject, 
            string targetPath,
            bool useUnicode,
            IProgress<string> progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                descriptorStream.Seek(0, SeekOrigin.Begin);
                using (var reader = new BinaryReader(descriptorStream))
                {
                    // First 4 bytes are the count of files in the descriptor
                    uint fileCount = reader.ReadUInt32();
                    System.Diagnostics.Debug.WriteLine($"[INFO] Found {fileCount} files in Outlook drag data");
                    progress?.Report($"Found {fileCount} files to extract");
                    
                    if (fileCount == 0 || fileCount > 100) // Sanity check
                    {
                        string error = $"Invalid file count: {fileCount}";
                        System.Diagnostics.Debug.WriteLine($"[WARN] {error}");
                        progress?.Report($"Error: {error}");
                        return ExtractionResult.CreateFailure(error, "File count validation");
                    }

                    var result = new ExtractionResult
                    {
                        Success = true,
                        TotalFiles = (int)fileCount,
                        Stage = "File descriptor extraction",
                        ExtractedFiles = new List<string>(),
                        FilesSkipped = 0,
                        Errors = 0
                    };
                    
                    for (int i = 0; i < fileCount; i++)
                    {
                        // Check for cancellation
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return ExtractionResult.CreateFailure("Operation cancelled by user", "During extraction");
                        }
                        
                        try
                        {
                            string fileName = ReadFileNameFromDescriptor(reader, useUnicode);
                            if (string.IsNullOrEmpty(fileName))
                            {
                                // If no filename found, generate one
                                fileName = $"Attachment_{i+1}_{DateTime.Now:yyyyMMdd_HHmmss}";
                                System.Diagnostics.Debug.WriteLine($"[WARN] No filename found for item {i}, using: {fileName}");
                                progress?.Report($"No filename found for item {i+1}, using generated name");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"[INFO] Found filename: {fileName}");
                                progress?.Report($"Extracting file {i+1}/{fileCount}: {fileName}");
                            }
                            
                            // Clean filename of invalid characters
                            fileName = SanitizeFileName(fileName);
                            
                            // Ensure we have a valid file extension
                            if (!Path.HasExtension(fileName))
                            {
                                // Try to detect file type and add appropriate extension
                                string extension = DetectFileExtension(dataObject, i);
                                if (!string.IsNullOrEmpty(extension))
                                {
                                    fileName += extension;
                                }
                                else
                                {
                                    fileName += ".dat"; // Default extension
                                }
                            }
                            
                            // Extract the file content using the index
                            string extractedPath = ExtractFileContent(dataObject, i, fileName, targetPath);
                            if (!string.IsNullOrEmpty(extractedPath))
                            {
                                result.ExtractedFiles.Add(extractedPath);
                                progress?.Report($"Successfully extracted: {Path.GetFileName(extractedPath)}");
                            }
                            else
                            {
                                result.FilesSkipped++;
                                result.Errors++;
                                progress?.Report($"Failed to extract: {fileName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            result.Errors++;
                            System.Diagnostics.Debug.WriteLine($"[ERROR] Error processing attachment {i}: {ex.Message}");
                            progress?.Report($"Error processing file {i+1}: {ex.Message}");
                            // Continue with next file
                        }
                    }

                    // Update success status based on extracted files
                    result.Success = result.ExtractedFiles.Count > 0;
                    
                    if (result.Success)
                    {
                        string message = $"Successfully extracted {result.ExtractedFiles.Count} of {fileCount} files";
                        System.Diagnostics.Debug.WriteLine($"[SUCCESS] {message}");
                        progress?.Report(message);
                    }
                    else if (result.Errors > 0)
                    {
                        result.ErrorMessage = $"Failed to extract any files. {result.Errors} errors occurred.";
                        progress?.Report(result.ErrorMessage);
                    }
                    
                    return result;
                }
            }
            catch (Exception ex)
            {
                string error = $"Error reading file descriptors: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[ERROR] {error}");
                progress?.Report($"Error: {error}");
                return ExtractionResult.CreateFailure(error, "Descriptor parsing");
            }
        }

        /// <summary>
        /// Reads a filename from the file descriptor structure with improved Unicode support
        /// </summary>
        private static string ReadFileNameFromDescriptor(BinaryReader reader, bool useUnicode)
        {
            try
            {
                long currentPos = reader.BaseStream.Position;
                
                // Skip to the filename field in the FILEDESCRIPTOR structure
                reader.BaseStream.Seek(currentPos + FILEDESCRIPTOR_FILENAME_OFFSET, SeekOrigin.Begin);
                
                string fileName;
                if (useUnicode)
                {
                    // Read filename as Unicode (260 characters maximum)
                    byte[] nameBytes = new byte[FILEDESCRIPTOR_FILENAME_LENGTH * 2]; // 260 * 2 bytes for Unicode
                    int bytesRead = reader.Read(nameBytes, 0, nameBytes.Length);
                    
                    // Convert to string and trim null terminators
                    fileName = Encoding.Unicode.GetString(nameBytes, 0, bytesRead).TrimEnd('\0');
                }
                else
                {
                    // Read filename as ANSI (260 characters maximum)
                    byte[] nameBytes = new byte[FILEDESCRIPTOR_FILENAME_LENGTH];
                    int bytesRead = reader.Read(nameBytes, 0, nameBytes.Length);
                    
                    // Convert to string and trim null terminators
                    // Use the default Windows encoding (usually Windows-1252 or equivalent)
                    Encoding ansiEncoding = Encoding.GetEncoding(1252);
                    fileName = ansiEncoding.GetString(nameBytes, 0, bytesRead).TrimEnd('\0');
                }
                
                // Seek to the end of this descriptor for the next one
                // FILEDESCRIPTOR is typically 592 bytes total
                reader.BaseStream.Seek(currentPos + 592, SeekOrigin.Begin);
                
                // Check if we have a valid filename
                if (!string.IsNullOrEmpty(fileName) && fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
                {
                    return fileName;
                }
                else
                {
                    // Try to extract at least some valid part of the filename
                    string cleanedName = SanitizeFileName(fileName);
                    if (!string.IsNullOrEmpty(cleanedName) && cleanedName.Length > 2)
                    {
                        return cleanedName;
                    }
                }
                
                return string.Empty;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Error reading filename from descriptor: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Detects file extension based on content analysis
        /// </summary>
        private static string DetectFileExtension(IDataObject dataObject, int index)
        {
            try
            {
                // Try to get the file content to detect its type
                object content = null;
                
                // Try different methods to get content
                string indexedFormat = $"FileContents{index}";
                if (dataObject.GetDataPresent(indexedFormat))
                {
                    content = dataObject.GetData(indexedFormat);
                }
                
                if (content == null)
                {
                    string colonFormat = $"FileContents:{index}";
                    if (dataObject.GetDataPresent(colonFormat))
                    {
                        content = dataObject.GetData(colonFormat);
                    }
                }
                
                if (content == null && dataObject.GetDataPresent(CFSTR_FILECONTENTS))
                {
                    var allContents = dataObject.GetData(CFSTR_FILECONTENTS);
                    if (allContents is object[] contentsArray && index < contentsArray.Length)
                    {
                        content = contentsArray[index];
                    }
                    else if (index == 0) // Single file case
                    {
                        content = allContents;
                    }
                }
                
                if (content != null)
                {
                    byte[] bytes = null;
                    
                    if (content is MemoryStream memStream)
                    {
                        bytes = new byte[Math.Min(memStream.Length, 16)]; // Read first 16 bytes for signature detection
                        memStream.Position = 0;
                        memStream.Read(bytes, 0, bytes.Length);
                        memStream.Position = 0; // Reset position
                    }
                    else if (content is byte[] contentBytes)
                    {
                        bytes = new byte[Math.Min(contentBytes.Length, 16)];
                        Array.Copy(contentBytes, bytes, bytes.Length);
                    }
                    else if (content is Stream stream)
                    {
                        bytes = new byte[16];
                        stream.Position = 0;
                        stream.Read(bytes, 0, bytes.Length);
                        stream.Position = 0; // Reset position
                    }
                    
                    if (bytes != null && bytes.Length >= 4)
                    {
                        // Check for PDF signature (%PDF)
                        if (bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46)
                        {
                            return ".pdf";
                        }
                        
                        // Check for JPEG signature
                        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
                        {
                            return ".jpg";
                        }
                        
                        // Check for PNG signature
                        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
                        {
                            return ".png";
                        }
                        
                        // Check for GIF signature
                        if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38)
                        {
                            return ".gif";
                        }
                        
                        // Check for ZIP/Office document signatures (PKZip header)
                        if (bytes[0] == 0x50 && bytes[1] == 0x4B && bytes[2] == 0x03 && bytes[3] == 0x04)
                        {
                            // This could be a ZIP, DOCX, XLSX, PPTX, etc.
                            // For now, just return .zip - could be enhanced to check deeper
                            return ".zip";
                        }
                    }
                }
                
                return string.Empty; // No detectable extension
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Error detecting file extension: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Extracts a single file content by index using multiple approaches to ensure compatibility
        /// </summary>
        private static string ExtractFileContent(IDataObject dataObject, int index, string fileName, string targetPath)
        {
            try
            {
                string filePath = GetUniqueFilePath(Path.Combine(targetPath, fileName));
                object content = null;
                
                // Try multiple methods to get content - the format can vary based on Outlook version
                
                // Method 1: Try direct indexed format
                string indexedFormat = $"FileContents{index}";
                if (dataObject.GetDataPresent(indexedFormat))
                {
                    content = dataObject.GetData(indexedFormat);
                }
                
                // Method 2: Try with colon separator
                if (content == null)
                {
                    string colonFormat = $"FileContents:{index}";
                    if (dataObject.GetDataPresent(colonFormat))
                    {
                        content = dataObject.GetData(colonFormat);
                    }
                }
                
                // Method 3: Try to get from array format
                if (content == null && dataObject.GetDataPresent(CFSTR_FILECONTENTS))
                {
                    var allContents = dataObject.GetData(CFSTR_FILECONTENTS);
                    if (allContents is object[] contentsArray && index < contentsArray.Length)
                    {
                        content = contentsArray[index];
                    }
                    else if (index == 0) // Single file case
                    {
                        content = allContents;
                    }
                }
                
                if (content != null)
                {
                    if (SaveContent(content, filePath))
                    {
                        return filePath;
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"[WARN] Could not find content for file: {fileName}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Error extracting file content: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extracts a single file when only one attachment is present
        /// </summary>
        private static ExtractionResult ExtractSingleFile(IDataObject dataObject, string format, string targetPath)
        {
            try
            {
                var content = dataObject.GetData(format);
                if (content == null)
                {
                    return ExtractionResult.CreateFailure($"No data found for format: {format}", "Single file extraction");
                }
                
                // Try to get a filename from other formats or use a default
                string fileName = "Attachment.dat";
                
                // Check for text format that might contain filename info
                foreach (string textFormat in new[] { "Text", "UnicodeText" })
                {
                    if (dataObject.GetDataPresent(textFormat))
                    {
                        string text = dataObject.GetData(textFormat) as string;
                        if (!string.IsNullOrEmpty(text))
                        {
                            // Look for what might be a filename
                            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (string line in lines)
                            {
                                if (line.Contains(".") && !line.Contains("/") && !line.Contains("\\"))
                                {
                                    // This might be a filename
                                    fileName = SanitizeFileName(line.Trim());
                                    break;
                                }
                            }
                            break;
                        }
                    }
                }
                
                // Try to detect file type and add appropriate extension
                if (!Path.HasExtension(fileName))
                {
                    string extension = DetectFileExtension(dataObject, 0);
                    if (!string.IsNullOrEmpty(extension))
                    {
                        fileName += extension;
                    }
                }
                
                string filePath = GetUniqueFilePath(Path.Combine(targetPath, fileName));
                
                var result = new ExtractionResult
                {
                    Success = false,
                    TotalFiles = 1,
                    Stage = "Single file extraction",
                    ExtractedFiles = new List<string>(),
                    FilesSkipped = 0,
                    Errors = 0
                };
                
                if (SaveContent(content, filePath))
                {
                    result.ExtractedFiles.Add(filePath);
                    result.Success = true;
                }
                else
                {
                    result.FilesSkipped = 1;
                    result.Errors = 1;
                    result.ErrorMessage = $"Failed to save content for {fileName}";
                }
                
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Error extracting single file: {ex.Message}");
                return ExtractionResult.CreateFailure($"Error extracting single file: {ex.Message}", "Single file extraction");
            }
        }

        /// <summary>
        /// Saves file content to disk handling various content types
        /// </summary>
        private static bool SaveContent(object content, string filePath)
        {
            try
            {
                if (content is MemoryStream memStream)
                {
                    using (var fileStream = File.Create(filePath))
                    {
                        memStream.Seek(0, SeekOrigin.Begin);
                        memStream.CopyTo(fileStream);
                    }
                    return true;
                }
                else if (content is byte[] bytes)
                {
                    File.WriteAllBytes(filePath, bytes);
                    return true;
                }
                else if (content is Stream stream)
                {
                    using (var fileStream = File.Create(filePath))
                    {
                        stream.CopyTo(fileStream);
                    }
                    return true;
                }
                else if (content is string text)
                {
                    File.WriteAllText(filePath, text);
                    return true;
                }
                
                System.Diagnostics.Debug.WriteLine($"[WARN] Unsupported content type: {content.GetType()}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Error saving file content: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets a unique file path by appending a number if the file already exists
        /// </summary>
        private static string GetUniqueFilePath(string originalPath)
        {
            if (!File.Exists(originalPath))
                return originalPath;

            string directory = Path.GetDirectoryName(originalPath) ?? string.Empty;
            string nameWithoutExt = Path.GetFileNameWithoutExtension(originalPath);
            string extension = Path.GetExtension(originalPath);

            int counter = 1;
            string newPath;
            do
            {
                newPath = Path.Combine(directory, $"{nameWithoutExt} ({counter}){extension}");
                counter++;
            }
            while (File.Exists(newPath));

            return newPath;
        }

        /// <summary>
        /// Sanitizes a filename by removing invalid characters
        /// </summary>
        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "Attachment";
                
            // Replace invalid filename characters
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }
            
            // Remove control characters which might cause display issues
            StringBuilder sb = new StringBuilder();
            foreach (char c in fileName)
            {
                if (!char.IsControl(c))
                {
                    sb.Append(c);
                }
            }
            fileName = sb.ToString();
            
            // Ensure filename isn't too long
            if (fileName.Length > 240)
            {
                string extension = Path.GetExtension(fileName);
                fileName = fileName.Substring(0, 240 - extension.Length) + extension;
            }
            
            // Return "Attachment" if we ended up with an empty string
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return "Attachment";
            }
            
            return fileName.Trim();
        }
    }
}