// UI/FileTree/Services/OutlookDataExtractor.cs (UPDATED to preserve original names)
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
    /// Utility class for extracting files from Outlook drag operations with improved original name preservation
    /// </summary>
    public static class OutlookDataExtractor
    {
        // Outlook data format constants
        private const string CFSTR_FILEDESCRIPTOR = "FileGroupDescriptor";
        private const string CFSTR_FILECONTENTS = "FileContents";

        /// <summary>
        /// Represents an extracted attachment with its original context
        /// </summary>
        public class AttachmentInfo
        {
            public string OriginalFileName { get; set; } = string.Empty;
            public byte[] Content { get; set; } = new byte[0];
            public long Size { get; set; }
            public DateTime LastModified { get; set; } = DateTime.Now;
        }

        /// <summary>
        /// Result of extraction operation
        /// </summary>
        public class ExtractionResult
        {
            public bool Success { get; set; }
            public int FilesExtracted { get; set; }
            public int FilesSkipped { get; set; }
            public List<string> Errors { get; set; } = new List<string>();
            public List<string> ExtractedFiles { get; set; } = new List<string>();
        }

        /// <summary>
        /// Extracts files from Outlook data object preserving original names
        /// </summary>
        /// <param name="dataObject">The data object containing Outlook data</param>
        /// <param name="targetPath">Target directory to save files</param>
        /// <returns>True if extraction was successful</returns>
        public static bool ExtractOutlookFiles(IDataObject dataObject, string targetPath)
        {
            try
            {
                var result = ExtractOutlookFilesWithDetails(dataObject, targetPath);
                return result.Success;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error extracting Outlook files: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Async version of Outlook file extraction with progress reporting
        /// </summary>
        public static async Task<ExtractionResult> ExtractOutlookFilesAsync(
            IDataObject dataObject, 
            string targetPath, 
            IProgress<string> progress = null, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                progress?.Report("Analyzing Outlook data...");
                
                var result = await Task.Run(() => ExtractOutlookFilesWithDetails(dataObject, targetPath, progress, cancellationToken), cancellationToken);
                
                progress?.Report(result.Success ? "Extraction completed successfully" : "Extraction failed");
                return result;
            }
            catch (OperationCanceledException)
            {
                progress?.Report("Extraction cancelled");
                return new ExtractionResult { Success = false };
            }
            catch (Exception ex)
            {
                progress?.Report($"Error: {ex.Message}");
                return new ExtractionResult { Success = false, Errors = { ex.Message } };
            }
        }

        /// <summary>
        /// Extracts files with detailed results
        /// </summary>
        private static ExtractionResult ExtractOutlookFilesWithDetails(
            IDataObject dataObject, 
            string targetPath, 
            IProgress<string> progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new ExtractionResult();

            try
            {
                // Try different extraction approaches
                if (TryExtractComplexFormat(dataObject, targetPath, result, progress, cancellationToken))
                {
                    result.Success = result.FilesExtracted > 0;
                    return result;
                }

                if (TryExtractSimpleFormat(dataObject, targetPath, result, progress))
                {
                    result.Success = result.FilesExtracted > 0;
                    return result;
                }

                result.Errors.Add("No recognizable Outlook data formats found");
                return result;
            }
            catch (COMException comEx)
            {
                result.Errors.Add($"COM error: {comEx.Message} (HRESULT: 0x{comEx.HResult:X})");
                return result;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"General error: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Tries to extract files using simple data formats
        /// </summary>
        private static bool TryExtractSimpleFormat(IDataObject dataObject, string targetPath, ExtractionResult result, IProgress<string> progress)
        {
            try
            {
                string[] simpleFormats = { "FileContents", "application/octet-stream" };

                foreach (string format in simpleFormats)
                {
                    if (dataObject.GetDataPresent(format))
                    {
                        progress?.Report($"Found simple format: {format}");
                        
                        var data = dataObject.GetData(format);
                        if (data != null && SaveSimpleContent(data, targetPath, result))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error in simple extraction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Saves simple content data to a file
        /// </summary>
        private static bool SaveSimpleContent(object data, string targetPath, ExtractionResult result)
        {
            try
            {
                string fileName = $"OutlookAttachment_{DateTime.Now:yyyyMMdd_HHmmss}";
                byte[] content = null;
                
                if (data is MemoryStream stream)
                {
                    content = stream.ToArray();
                }
                else if (data is byte[] bytes)
                {
                    content = bytes;
                }
                else
                {
                    return false;
                }

                // Try to determine file extension from content
                string extension = GetFileExtensionFromContent(content);
                fileName += extension;
                
                string filePath = GetUniqueFilePath(Path.Combine(targetPath, fileName));
                File.WriteAllBytes(filePath, content);
                
                result.ExtractedFiles.Add(filePath);
                result.FilesExtracted++;
                
                return true;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error saving simple content: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Tries to extract files using the complex FileGroupDescriptor format
        /// </summary>
        private static bool TryExtractComplexFormat(
            IDataObject dataObject, 
            string targetPath, 
            ExtractionResult result, 
            IProgress<string> progress,
            CancellationToken cancellationToken)
        {
            try
            {
                if (!dataObject.GetDataPresent(CFSTR_FILEDESCRIPTOR))
                {
                    return false;
                }

                var descriptorData = dataObject.GetData(CFSTR_FILEDESCRIPTOR);
                if (descriptorData == null)
                {
                    result.Errors.Add("Could not get FileGroupDescriptor data");
                    return false;
                }

                MemoryStream descriptorStream = GetMemoryStreamFromData(descriptorData);
                if (descriptorStream == null)
                {
                    result.Errors.Add($"Unexpected descriptor data type: {descriptorData.GetType()}");
                    return false;
                }

                return ReadFileDescriptors(descriptorStream, dataObject, targetPath, result, progress, cancellationToken);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error in complex extraction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Converts data to MemoryStream
        /// </summary>
        private static MemoryStream GetMemoryStreamFromData(object data)
        {
            if (data is MemoryStream memStream)
            {
                return memStream;
            }
            else if (data is byte[] dataBytes)
            {
                return new MemoryStream(dataBytes);
            }
            return null;
        }

        /// <summary>
        /// Reads file descriptors and extracts files preserving original names
        /// </summary>
        private static bool ReadFileDescriptors(
            MemoryStream descriptorStream, 
            IDataObject dataObject, 
            string targetPath, 
            ExtractionResult result,
            IProgress<string> progress,
            CancellationToken cancellationToken)
        {
            try
            {
                descriptorStream.Seek(0, SeekOrigin.Begin);
                
                var attachments = ParseFileDescriptors(descriptorStream);
                if (attachments.Count == 0)
                {
                    result.Errors.Add("No file descriptors found");
                    return false;
                }

                progress?.Report($"Found {attachments.Count} attachment(s). Extracting...");

                for (int i = 0; i < attachments.Count; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var attachment = attachments[i];
                    
                    // Use original filename if available, otherwise generate one
                    string fileName = !string.IsNullOrEmpty(attachment.OriginalFileName) 
                        ? attachment.OriginalFileName 
                        : $"Attachment_{i}_{DateTime.Now:yyyyMMdd_HHmmss}";
                    
                    progress?.Report($"Extracting {fileName} ({i + 1}/{attachments.Count})...");
                    
                    try
                    {
                        if (TryGetFileContentByIndex(dataObject, i, attachment, fileName, targetPath, result))
                        {
                            // Success handled in TryGetFileContentByIndex
                        }
                        else
                        {
                            result.FilesSkipped++;
                            result.Errors.Add($"Could not extract content for: {fileName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.FilesSkipped++;
                        result.Errors.Add($"Error extracting {fileName}: {ex.Message}");
                    }
                }

                return result.FilesExtracted > 0;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error reading file descriptors: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Parses the FileGroupDescriptor to extract attachment information
        /// </summary>
        private static List<AttachmentInfo> ParseFileDescriptors(MemoryStream descriptorStream)
        {
            var attachments = new List<AttachmentInfo>();
            
            try
            {
                using (var reader = new BinaryReader(descriptorStream))
                {
                    if (descriptorStream.Length < 4)
                        return attachments;
                    
                    uint fileCount = reader.ReadUInt32();
                    if (fileCount == 0 || fileCount > 1000) // Sanity check
                        return attachments;

                    for (int i = 0; i < fileCount; i++)
                    {
                        try
                        {
                            var attachment = ReadSingleFileDescriptor(reader);
                            if (attachment != null)
                            {
                                attachments.Add(attachment);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error reading descriptor {i}: {ex.Message}");
                            // Continue with next descriptor
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing descriptors: {ex.Message}");
            }

            return attachments;
        }

        /// <summary>
        /// Reads a single file descriptor from the stream
        /// </summary>
        private static AttachmentInfo ReadSingleFileDescriptor(BinaryReader reader)
        {
            try
            {
                long startPosition = reader.BaseStream.Position;
                
                // FILEDESCRIPTOR structure:
                // DWORD dwFlags
                // CLSID clsid (16 bytes)
                // SIZEL sizel (8 bytes) 
                // POINTL pointl (8 bytes)
                // DWORD dwFileAttributes
                // FILETIME ftCreationTime (8 bytes)
                // FILETIME ftLastAccessTime (8 bytes)
                // FILETIME ftLastWriteTime (8 bytes)
                // DWORD nFileSizeHigh
                // DWORD nFileSizeLow
                // TCHAR cFileName[MAX_PATH] (520 bytes for Unicode)

                // Skip to file size (offset 72)
                reader.BaseStream.Seek(startPosition + 72, SeekOrigin.Begin);
                
                uint fileSizeHigh = reader.ReadUInt32();
                uint fileSizeLow = reader.ReadUInt32();
                long fileSize = ((long)fileSizeHigh << 32) | fileSizeLow;
                
                // Read filename (520 bytes Unicode)
                byte[] nameBytes = reader.ReadBytes(520);
                string fileName = Encoding.Unicode.GetString(nameBytes).TrimEnd('\0');
                
                // Clean the filename for filesystem use
                fileName = CleanFileName(fileName);
                
                return new AttachmentInfo
                {
                    OriginalFileName = fileName,
                    Size = fileSize
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading single descriptor: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Cleans a filename for filesystem use while preserving the original name as much as possible
        /// </summary>
        private static string CleanFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return string.Empty;
            
            // Replace invalid filename characters with underscore
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(invalidChar, '_');
            }
            
            // Trim whitespace and periods from the end
            fileName = fileName.Trim(' ', '.');
            
            // Ensure the filename isn't empty
            if (string.IsNullOrWhiteSpace(fileName))
                return "attachment";
            
            return fileName;
        }

        /// <summary>
        /// Tries to get file content for a specific attachment by index
        /// </summary>
        private static bool TryGetFileContentByIndex(
            IDataObject dataObject, 
            int index, 
            AttachmentInfo attachment,
            string fileName,
            string targetPath, 
            ExtractionResult result)
        {
            try
            {
                object content = null;
                
                // Try multiple approaches to get the content
                string[] contentFormats = {
                    $"FileContents{index}",
                    $"FileContents:{index}",
                    CFSTR_FILECONTENTS
                };

                foreach (string format in contentFormats)
                {
                    try
                    {
                        if (dataObject.GetDataPresent(format))
                        {
                            content = dataObject.GetData(format);
                            if (content != null)
                            {
                                // If we got an array and this is the FileContents format, get the specific index
                                if (format == CFSTR_FILECONTENTS && content is object[] contentsArray && index < contentsArray.Length)
                                {
                                    content = contentsArray[index];
                                }
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error trying format {format}: {ex.Message}");
                        continue;
                    }
                }

                if (content != null)
                {
                    return SaveAttachmentContent(content, fileName, targetPath, result);
                }
                
                return false;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error getting content for {fileName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Saves attachment content to disk preserving the original filename
        /// </summary>
        private static bool SaveAttachmentContent(object content, string fileName, string targetPath, ExtractionResult result)
        {
            try
            {
                string filePath = GetUniqueFilePath(Path.Combine(targetPath, fileName));
                byte[] fileData = null;

                if (content is MemoryStream contentStream)
                {
                    fileData = contentStream.ToArray();
                }
                else if (content is byte[] contentBytes)
                {
                    fileData = contentBytes;
                }
                else if (content is Stream stream)
                {
                    using (var ms = new MemoryStream())
                    {
                        stream.Seek(0, SeekOrigin.Begin);
                        stream.CopyTo(ms);
                        fileData = ms.ToArray();
                    }
                }
                else
                {
                    result.Errors.Add($"Unsupported content type for {fileName}: {content.GetType()}");
                    return false;
                }

                // Validate that we have content
                if (fileData == null || fileData.Length == 0)
                {
                    result.Errors.Add($"No content data for {fileName}");
                    return false;
                }

                File.WriteAllBytes(filePath, fileData);
                
                result.ExtractedFiles.Add(filePath);
                result.FilesExtracted++;
                
                return true;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error saving {fileName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets file extension from content bytes (fallback when original name has no extension)
        /// </summary>
        private static string GetFileExtensionFromContent(byte[] content)
        {
            if (content == null || content.Length < 4)
                return ".dat";

            // Check for common file signatures
            var signatures = new Dictionary<byte[], string>
            {
                { new byte[] { 0x25, 0x50, 0x44, 0x46 }, ".pdf" },
                { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, ".png" },
                { new byte[] { 0xFF, 0xD8, 0xFF }, ".jpg" },
                { new byte[] { 0x47, 0x49, 0x46, 0x38 }, ".gif" },
                { new byte[] { 0x42, 0x4D }, ".bmp" },
                { new byte[] { 0x50, 0x4B, 0x03, 0x04 }, ".zip" },
                { new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }, ".doc" },
                { new byte[] { 0x4D, 0x5A }, ".exe" },
                { new byte[] { 0x7B, 0x5C, 0x72, 0x74, 0x66 }, ".rtf" },
                { new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x00 }, ".rar" },
                { new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C }, ".7z" }
            };

            foreach (var signature in signatures)
            {
                if (content.Length >= signature.Key.Length && 
                    content.Take(signature.Key.Length).SequenceEqual(signature.Key))
                {
                    return signature.Value;
                }
            }

            return ".dat";
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
                string newName = $"{nameWithoutExt} ({counter}){extension}";
                newPath = Path.Combine(directory, newName);
                counter++;
            }
            while (File.Exists(newPath));

            return newPath;
        }
    }
}