using System.Collections.Generic;
using Newtonsoft.Json;

namespace ExplorerPro.Models
{
    /// <summary>
    /// Provides application-wide configuration constants and default settings.
    /// </summary>
    public static class ConfigManager
    {
        /// <summary>
        /// Default theme for the application.
        /// </summary>
        public const string DefaultTheme = "light";

        /// <summary>
        /// List of supported file extensions.
        /// </summary>
        public static readonly IReadOnlyList<string> SupportedExtensions = new List<string>
        {
            ".pdf", ".docx", ".xlsx", ".png", ".jpg", ".jpeg", ".txt", ".py", ".json", ".xls", ".doc", ".zip", ".exe",
            ".ico", ".bmp", ".gif", ".tiff", ".svg", ".csv", ".ini", ".log", ".html", ".css", ".js", ".md", ".pyc", ".mpp"
        };

        /// <summary>
        /// Application name.
        /// </summary>
        public const string AppName = "Enhanced File Explorer";

        /// <summary>
        /// Application version.
        /// </summary>
        public const string Version = "1.0.0";

        /// <summary>
        /// Path to the default template folder.
        /// </summary>
        public const string DefaultTemplateFolder = "assets/templates";

        /// <summary>
        /// Path to the default icons folder.
        /// </summary>
        public const string DefaultIconsFolder = "assets/icons";

        /// <summary>
        /// Path to the default theme folder.
        /// </summary>
        public const string DefaultThemeFolder = "assets/themes";

        /// <summary>
        /// Maximum file size in megabytes for preview.
        /// </summary>
        public const int MaxFilePreviewSizeMB = 10;

        /// <summary>
        /// Default settings for the application.
        /// </summary>
        public static readonly string DefaultSettingsJson = JsonConvert.SerializeObject(new
        {
            theme = DefaultTheme,
            last_opened_directory = "",
            show_hidden_files = false
        }, Formatting.Indented);

        /// <summary>
        /// Gets the default settings as a dynamic object.
        /// </summary>
        /// <returns>Default settings as a dynamic object.</returns>
        public static dynamic GetDefaultSettings()
        {
            return JsonConvert.DeserializeObject(DefaultSettingsJson);
        }

        /// <summary>
        /// Checks if a file extension is supported.
        /// </summary>
        /// <param name="extension">The file extension to check (including the dot).</param>
        /// <returns>True if the extension is supported, false otherwise.</returns>
        public static bool IsExtensionSupported(string extension)
        {
            if (string.IsNullOrEmpty(extension))
                return false;

            extension = extension.ToLower();
            foreach (var supportedExt in SupportedExtensions)
            {
                if (extension == supportedExt)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the maximum file size in bytes for preview.
        /// </summary>
        public static long MaxFilePreviewSizeBytes => MaxFilePreviewSizeMB * 1024 * 1024;
    }
}