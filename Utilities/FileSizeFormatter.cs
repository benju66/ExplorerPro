using System;

namespace ExplorerPro.Utilities
{
    /// <summary>
    /// Provides methods for formatting file sizes in human-readable formats
    /// </summary>
    public static class FileSizeFormatter
    {
        /// <summary>
        /// Size units for file size formatting
        /// </summary>
        private static readonly string[] SizeUnits = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };

        /// <summary>
        /// Formats a file size in bytes to a human-readable string
        /// </summary>
        /// <param name="bytes">File size in bytes</param>
        /// <param name="decimalPlaces">Number of decimal places to show</param>
        /// <returns>Formatted file size string (e.g., "1.5 MB")</returns>
        public static string FormatSize(long bytes, int decimalPlaces = 1)
        {
            if (bytes <= 0)
                return "0 B";

            // Use 1024 for binary bytes (KiB, MiB) or 1000 for decimal (KB, MB)
            const double thresh = 1024;
            
            // Calculate the appropriate size unit
            double size = bytes;
            int unitIndex = 0;
            
            while (size >= thresh && unitIndex < SizeUnits.Length - 1)
            {
                size /= thresh;
                unitIndex++;
            }

            // Round to the specified decimal places
            string format = unitIndex == 0 
                ? "0" // No decimal places for bytes
                : "0." + new string('0', decimalPlaces);
            
            return $"{size.ToString(format)} {SizeUnits[unitIndex]}";
        }

        /// <summary>
        /// Formats a file size to a detailed string including exact byte count
        /// </summary>
        /// <param name="bytes">File size in bytes</param>
        /// <returns>Detailed file size string (e.g., "1.5 MB (1,572,864 bytes)")</returns>
        public static string FormatSizeDetailed(long bytes)
        {
            string formattedSize = FormatSize(bytes);
            string formattedBytes = bytes.ToString("N0");
            return $"{formattedSize} ({formattedBytes} bytes)";
        }

        /// <summary>
        /// Formats a file size with appropriate units based on the size
        /// Using specific rules (e.g., showing KB for small files)
        /// </summary>
        /// <param name="bytes">File size in bytes</param>
        /// <returns>Formatted file size string using custom rules</returns>
        public static string FormatSizeWithRules(long bytes)
        {
            // For very small files, just show bytes
            if (bytes < 1024)
                return $"{bytes} B";
            
            // For files between 1 KB and 1 MB, show KB
            if (bytes < 1024 * 1024)
            {
                double kb = bytes / 1024.0;
                return $"{kb:0.#} KB";
            }
            
            // For files between 1 MB and 1 GB, show MB
            if (bytes < 1024 * 1024 * 1024)
            {
                double mb = bytes / (1024.0 * 1024.0);
                return $"{mb:0.##} MB";
            }
            
            // For larger files, show GB
            double gb = bytes / (1024.0 * 1024.0 * 1024.0);
            return $"{gb:0.##} GB";
        }

        /// <summary>
        /// Converts a formatted file size string back to bytes
        /// </summary>
        /// <param name="formattedSize">Formatted file size string (e.g., "1.5 MB")</param>
        /// <returns>File size in bytes, or -1 if parsing fails</returns>
        public static long ParseFormattedSize(string formattedSize)
        {
            try
            {
                string[] parts = formattedSize.Trim().Split(' ');
                if (parts.Length != 2)
                    return -1;
                
                if (!double.TryParse(parts[0], out double size))
                    return -1;
                
                string unit = parts[1].ToUpperInvariant();
                
                // Find the unit index
                int unitIndex = Array.IndexOf(SizeUnits, unit);
                if (unitIndex < 0)
                    return -1;
                
                // Calculate the bytes
                double bytes = size * Math.Pow(1024, unitIndex);
                return (long)bytes;
            }
            catch
            {
                return -1;
            }
        }
    }
}