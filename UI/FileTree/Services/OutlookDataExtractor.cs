// UI/FileTree/Services/OutlookDataExtractor.cs (UPDATED with better COM handling)
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace ExplorerPro.UI.FileTree.Services
{
    /// <summary>
    /// Utility class for extracting files from Outlook drag operations with improved COM handling
    /// </summary>
    public static class OutlookDataExtractor
    {
        // Outlook data format constants
        private const string CFSTR_FILEDESCRIPTOR = "FileGroupDescriptor";
        private const string CFSTR_FILECONTENTS = "FileContents";

        /// <summary>
        /// Extracts files from Outlook data object using a simplified approach
        /// </summary>
        /// <param name="dataObject">The data object containing Outlook data</param>
        /// <param name="targetPath">Target directory to save files</param>
        /// <returns>True if extraction was successful</returns>
        public static bool ExtractOutlookFiles(IDataObject dataObject, string targetPath)
        {
            try
            {
                // Try the simple approach first - check for direct file content
                if (TryExtractSimpleFormat(dataObject, targetPath))
                {
                    return true;
                }

                // Try the complex FileGroupDescriptor approach
                return TryExtractComplexFormat(dataObject, targetPath);
            }
            catch (COMException comEx)
            {
                System.Diagnostics.Debug.WriteLine($"COM error extracting Outlook files: {comEx.Message} (HRESULT: 0x{comEx.HResult:X})");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error extracting Outlook files: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Tries to extract files using simple data formats
        /// </summary>
        private static bool TryExtractSimpleFormat(IDataObject dataObject, string targetPath)
        {
            try
            {
                // Check for simple file content formats
                string[] simpleFormats = {
                    "FileContents",
                    "Preferred DropEffect",
                    "application/octet-stream"
                };

                foreach (string format in simpleFormats)
                {
                    if (dataObject.GetDataPresent(format))
                    {
                        System.Diagnostics.Debug.WriteLine($"Found simple format: {format}");
                        
                        var data = dataObject.GetData(format);
                        if (data != null)
                        {
                            return SaveSimpleContent(data, targetPath);
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in simple extraction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Saves simple content data to a file
        /// </summary>
        private static bool SaveSimpleContent(object data, string targetPath)
        {
            try
            {
                string fileName = $"OutlookAttachment_{DateTime.Now:yyyyMMdd_HHmmss}";
                
                if (data is MemoryStream stream)
                {
                    // Try to determine file extension from content
                    stream.Seek(0, SeekOrigin.Begin);
                    byte[] header = new byte[4];
                    stream.Read(header, 0, 4);
                    stream.Seek(0, SeekOrigin.Begin);
                    
                    string extension = GetFileExtensionFromHeader(header);
                    fileName += extension;
                    
                    string filePath = GetUniqueFilePath(Path.Combine(targetPath, fileName));
                    
                    using (var fileStream = File.Create(filePath))
                    {
                        stream.CopyTo(fileStream);
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"Saved simple content as: {fileName}");
                    return true;
                }
                else if (data is byte[] bytes)
                {
                    string extension = GetFileExtensionFromHeader(bytes);
                    fileName += extension;
                    
                    string filePath = GetUniqueFilePath(Path.Combine(targetPath, fileName));
                    File.WriteAllBytes(filePath, bytes);
                    
                    System.Diagnostics.Debug.WriteLine($"Saved byte content as: {fileName}");
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving simple content: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Tries to extract files using the complex FileGroupDescriptor format
        /// </summary>
        private static bool TryExtractComplexFormat(IDataObject dataObject, string targetPath)
        {
            try
            {
                // Check if we have the required formats
                if (!dataObject.GetDataPresent(CFSTR_FILEDESCRIPTOR))
                {
                    System.Diagnostics.Debug.WriteLine("FileGroupDescriptor not present");
                    return false;
                }

                // Get descriptor data
                var descriptorData = dataObject.GetData(CFSTR_FILEDESCRIPTOR);
                if (descriptorData == null)
                {
                    System.Diagnostics.Debug.WriteLine("Could not get FileGroupDescriptor data");
                    return false;
                }

                // Try to read as memory stream first
                MemoryStream descriptorStream = null;
                
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
                    System.Diagnostics.Debug.WriteLine($"Unexpected descriptor data type: {descriptorData.GetType()}");
                    return false;
                }

                return ReadFileDescriptors(descriptorStream, dataObject, targetPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in complex extraction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reads file descriptors and extracts files
        /// </summary>
        private static bool ReadFileDescriptors(MemoryStream descriptorStream, IDataObject dataObject, string targetPath)
        {
            try
            {
                descriptorStream.Seek(0, SeekOrigin.Begin);
                using (var reader = new BinaryReader(descriptorStream))
                {
                    // Read file count
                    if (descriptorStream.Length < 4)
                    {
                        System.Diagnostics.Debug.WriteLine("Descriptor stream too short");
                        return false;
                    }
                    
                    uint fileCount = reader.ReadUInt32();
                    System.Diagnostics.Debug.WriteLine($"Found {fileCount} files in descriptor");
                    
                    if (fileCount == 0 || fileCount > 100) // Sanity check
                    {
                        System.Diagnostics.Debug.WriteLine($"Invalid file count: {fileCount}");
                        return false;
                    }

                    bool anySuccess = false;
                    for (int i = 0; i < fileCount; i++)
                    {
                        try
                        {
                            string fileName = ReadFileName(reader);
                            if (!string.IsNullOrEmpty(fileName))
                            {
                                System.Diagnostics.Debug.WriteLine($"Processing file {i}: {fileName}");
                                
                                // Try to get file content
                                if (TryGetFileContent(dataObject, i, fileName, targetPath))
                                {
                                    anySuccess = true;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error processing file {i}: {ex.Message}");
                            // Continue with next file
                        }
                    }

                    return anySuccess;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading file descriptors: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reads a filename from the descriptor (simplified approach)
        /// </summary>
        private static string ReadFileName(BinaryReader reader)
        {
            try
            {
                // Skip most of the descriptor fields and just get to the filename
                // The filename is at offset 76 in the FILEDESCRIPTOR structure
                long currentPos = reader.BaseStream.Position;
                
                // Skip to filename (assuming we're already past the file count)
                // FILEDESCRIPTOR is about 86 bytes, filename starts at offset 76
                reader.BaseStream.Seek(currentPos + 72, SeekOrigin.Begin);
                
                // Read filename (260 Unicode characters maximum)
                byte[] nameBytes = reader.ReadBytes(520);
                string fileName = Encoding.Unicode.GetString(nameBytes).TrimEnd('\0');
                
                return fileName;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading filename: {ex.Message}");
                return $"OutlookFile_{DateTime.Now.Ticks}";
            }
        }

        /// <summary>
        /// Tries to get file content for a specific file
        /// </summary>
        private static bool TryGetFileContent(IDataObject dataObject, int index, string fileName, string targetPath)
        {
            try
            {
                // Clean the filename
                foreach (char invalidChar in Path.GetInvalidFileNameChars())
                {
                    fileName = fileName.Replace(invalidChar, '_');
                }

                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = $"OutlookAttachment_{index}_{DateTime.Now:yyyyMMdd_HHmmss}";
                }

                // Try multiple approaches to get the content
                object content = null;
                
                // Method 1: Try indexed format
                if (dataObject.GetDataPresent($"FileContents{index}"))
                {
                    content = dataObject.GetData($"FileContents{index}");
                }
                
                // Method 2: Try with colon
                if (content == null && dataObject.GetDataPresent($"FileContents:{index}"))
                {
                    content = dataObject.GetData($"FileContents:{index}");
                }
                
                // Method 3: Try to get all contents as array
                if (content == null && dataObject.GetDataPresent(CFSTR_FILECONTENTS))
                {
                    var allContents = dataObject.GetData(CFSTR_FILECONTENTS);
                    if (allContents is object[] contentsArray && index < contentsArray.Length)
                    {
                        content = contentsArray[index];
                    }
                }

                if (content != null)
                {
                    return SaveFileContent(content, fileName, targetPath);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Could not get content for file {index}: {fileName}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting file content for {fileName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Saves file content to disk
        /// </summary>
        private static bool SaveFileContent(object content, string fileName, string targetPath)
        {
            try
            {
                string filePath = GetUniqueFilePath(Path.Combine(targetPath, fileName));

                if (content is MemoryStream contentStream)
                {
                    using (var fileStream = File.Create(filePath))
                    {
                        contentStream.Seek(0, SeekOrigin.Begin);
                        contentStream.CopyTo(fileStream);
                    }
                }
                else if (content is byte[] contentBytes)
                {
                    File.WriteAllBytes(filePath, contentBytes);
                }
                else if (content is Stream stream)
                {
                    using (var fileStream = File.Create(filePath))
                    {
                        stream.Seek(0, SeekOrigin.Begin);
                        stream.CopyTo(fileStream);
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Unsupported content type: {content.GetType()}");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"Successfully saved: {fileName}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving file {fileName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets file extension from file header bytes
        /// </summary>
        private static string GetFileExtensionFromHeader(byte[] header)
        {
            if (header == null || header.Length < 4)
                return ".dat";

            // Check for common file signatures
            if (header.Length >= 4)
            {
                // PDF signature
                if (header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46)
                    return ".pdf";
                
                // PNG signature
                if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
                    return ".png";
                
                // JPEG signature
                if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
                    return ".jpg";
                
                // ZIP signature (also used by Office documents)
                if (header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04)
                    return ".zip";
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
                string newName = $"{nameWithoutExt}_{counter}{extension}";
                newPath = Path.Combine(directory, newName);
                counter++;
            }
            while (File.Exists(newPath));

            return newPath;
        }
    }
}