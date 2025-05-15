// Add to Utilities namespace
using System;
using System.IO;

namespace ExplorerPro.Utilities
{
    /// <summary>
    /// Utility class for path operations with improved error handling
    /// </summary>
    public static class PathUtils
    {
        /// <summary>
        /// Gets a valid directory path, falling back to default if necessary
        /// </summary>
        /// <param name="path">Original path to validate</param>
        /// <param name="fallback">Optional fallback path</param>
        /// <returns>A valid directory path</returns>
        public static string GetValidDirectory(string path, string fallback = "")
        {
            try
            {
                // Check if the path exists and is a directory
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    return Path.GetFullPath(path);
                }
                
                // If path doesn't exist but fallback is provided, check fallback
                if (!string.IsNullOrEmpty(fallback))
                {
                    if (Directory.Exists(fallback))
                    {
                        return Path.GetFullPath(fallback);
                    }
                }
                
                // If no valid path found, use system defaults in this order
                string[] defaultPaths = {
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    Environment.CurrentDirectory
                };
                
                foreach (string defaultPath in defaultPaths)
                {
                    if (!string.IsNullOrEmpty(defaultPath) && Directory.Exists(defaultPath))
                    {
                        return defaultPath;
                    }
                }
                
                // Last resort - return original path even if invalid
                return string.IsNullOrEmpty(path) ? fallback : path;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error validating directory: {ex.Message}");
                
                // On error, return user profile as ultimate fallback
                try
                {
                    string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    if (Directory.Exists(userProfile))
                    {
                        return userProfile;
                    }
                }
                catch
                {
                    // If even that fails, return the current directory
                    return Environment.CurrentDirectory;
                }
                
                // If all else fails, return the original path
                return string.IsNullOrEmpty(path) ? fallback : path;
            }
        }
        
        /// <summary>
        /// Gets a filename safely, handling exceptions
        /// </summary>
        /// <param name="path">Path to extract filename from</param>
        /// <returns>The filename or path if extraction fails</returns>
        public static string SafeGetFileName(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    return "Unnamed";
                }
                
                string fileName = Path.GetFileName(path);
                
                // Handle root paths like "C:\"
                if (string.IsNullOrEmpty(fileName) || path.EndsWith(":\\"))
                {
                    return path;
                }
                
                return fileName;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting filename: {ex.Message}");
                return path;
            }
        }
        
        /// <summary>
        /// Ensures a directory exists, creating it if needed
        /// </summary>
        /// <param name="path">Directory path to ensure</param>
        /// <returns>True if directory exists or was created, false otherwise</returns>
        public static bool EnsureDirectoryExists(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    return false;
                }
                
                if (Directory.Exists(path))
                {
                    return true;
                }
                
                Directory.CreateDirectory(path);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error ensuring directory exists: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Checks if a path is valid for the file system
        /// </summary>
        /// <param name="path">Path to validate</param>
        /// <returns>True if path is valid, false otherwise</returns>
        public static bool IsValidPath(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    return false;
                }
                
                // Check for invalid characters
                char[] invalidChars = Path.GetInvalidPathChars();
                if (path.IndexOfAny(invalidChars) >= 0)
                {
                    return false;
                }
                
                // Try to get full path (will throw for invalid paths)
                Path.GetFullPath(path);
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Gets a safe path for a file by ensuring parent directory exists
        /// </summary>
        /// <param name="filePath">Path to validate</param>
        /// <param name="createDirectory">Whether to create the directory if missing</param>
        /// <returns>Valid path or null if invalid</returns>
        public static string GetSafeFilePath(string filePath, bool createDirectory = true)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    return null;
                }
                
                // Check path validity
                if (!IsValidPath(filePath))
                {
                    return null;
                }
                
                // Get directory
                string directory = Path.GetDirectoryName(filePath);
                
                // If directory is null or empty, use current directory
                if (string.IsNullOrEmpty(directory))
                {
                    directory = Environment.CurrentDirectory;
                    filePath = Path.Combine(directory, Path.GetFileName(filePath));
                }
                
                // Ensure directory exists if requested
                if (createDirectory)
                {
                    EnsureDirectoryExists(directory);
                }
                
                return filePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting safe file path: {ex.Message}");
                return null;
            }
        }
    }
}