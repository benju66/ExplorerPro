using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.ComponentModel;
using ExplorerPro.Models;

namespace ExplorerPro.UI.Dialogs
{
    /// <summary>
    /// Dialog for displaying and editing file properties.
    /// </summary>
    public partial class FilePropertiesDialog : Window
    {
        private readonly string _filePath;
        private readonly MetadataManager _metadataManager;
        private readonly FileInfo _fileInfo;
        private readonly DirectoryInfo _dirInfo;
        private bool _isFile;

        /// <summary>
        /// Initialize a new File Properties Dialog.
        /// </summary>
        /// <param name="filePath">Path to the file or directory</param>
        /// <param name="metadataManager">Metadata manager instance</param>
        public FilePropertiesDialog(string filePath, MetadataManager metadataManager)
        {
            InitializeComponent();
            
            _filePath = filePath;
            _metadataManager = metadataManager;
            
            // Determine if it's a file or directory
            _isFile = File.Exists(filePath);
            
            if (_isFile)
            {
                _fileInfo = new FileInfo(filePath);
                _dirInfo = null;
                Title = "File Properties";
            }
            else if (Directory.Exists(filePath))
            {
                _fileInfo = null;
                _dirInfo = new DirectoryInfo(filePath);
                Title = "Folder Properties";
            }
            else
            {
                MessageBox.Show("File or folder does not exist.", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }
            
            // Load metadata and properties
            LoadProperties();
            
            // Set up events
            this.Loaded += FilePropertiesDialog_Loaded;
        }
        
        private void FilePropertiesDialog_Loaded(object sender, RoutedEventArgs e)
        {
            // Additional initialization when loaded
        }
        
        private void LoadProperties()
        {
            try
            {
                // Basic properties
                if (_isFile)
                {
                    SetFileProperties();
                }
                else
                {
                    SetFolderProperties();
                }
                
                // Custom metadata from metadata manager
                LoadCustomMetadata();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading properties: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void SetFileProperties()
        {
            if (_fileInfo == null) return;
            
            // Set file name and path
            fileNameTextBlock.Text = _fileInfo.Name;
            filePathTextBlock.Text = _fileInfo.DirectoryName;
            
            // Set file size
            string fileSize = FormatFileSize(_fileInfo.Length);
            fileSizeTextBlock.Text = fileSize;
            
            // Set file dates
            createdDateTextBlock.Text = _fileInfo.CreationTime.ToString();
            modifiedDateTextBlock.Text = _fileInfo.LastWriteTime.ToString();
            accessedDateTextBlock.Text = _fileInfo.LastAccessTime.ToString();
            
            // Set file attributes
            fileAttributesTextBlock.Text = _fileInfo.Attributes.ToString();
            
            // Set file type
            fileTypeTextBlock.Text = GetFileType(_fileInfo.Extension);
        }
        
        private void SetFolderProperties()
        {
            if (_dirInfo == null) return;
            
            // Set folder name and path
            fileNameTextBlock.Text = _dirInfo.Name;
            filePathTextBlock.Text = _dirInfo.Parent?.FullName;
            
            // Set folder size (may take time for large folders)
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += (s, e) =>
            {
                e.Result = GetDirectorySize(_dirInfo);
            };
            worker.RunWorkerCompleted += (s, e) =>
            {
                if (e.Error != null)
                {
                    fileSizeTextBlock.Text = "Error calculating size";
                }
                else
                {
                    fileSizeTextBlock.Text = FormatFileSize((long)e.Result);
                }
            };
            worker.RunWorkerAsync();
            
            // Set folder dates
            createdDateTextBlock.Text = _dirInfo.CreationTime.ToString();
            modifiedDateTextBlock.Text = _dirInfo.LastWriteTime.ToString();
            accessedDateTextBlock.Text = _dirInfo.LastAccessTime.ToString();
            
            // Set folder attributes
            fileAttributesTextBlock.Text = _dirInfo.Attributes.ToString();
            
            // Set folder type
            fileTypeTextBlock.Text = "File Folder";
        }
        
        private void LoadCustomMetadata()
        {
            // Load tags
            var tags = _metadataManager.GetTags(_filePath);
            if (tags != null && tags.Count > 0)
            {
                tagsTextBlock.Text = string.Join(", ", tags);
            }
            else
            {
                tagsTextBlock.Text = "No tags";
            }
            
            // Load custom color if any
            string colorHex = _metadataManager.GetItemColor(_filePath);
            if (!string.IsNullOrEmpty(colorHex))
            {
                try
                {
                    Color color = (Color)ColorConverter.ConvertFromString(colorHex);
                    colorIndicator.Fill = new SolidColorBrush(color);
                }
                catch
                {
                    colorIndicator.Fill = new SolidColorBrush(Colors.Gray);
                }
            }
            else
            {
                colorIndicator.Fill = new SolidColorBrush(Colors.Gray);
            }
        }
        
        private string GetFileType(string extension)
        {
            if (string.IsNullOrEmpty(extension))
                return "File";
                
            switch (extension.ToLower())
            {
                case ".txt":
                    return "Text Document";
                case ".docx":
                    return "Microsoft Word Document";
                case ".xlsx":
                    return "Microsoft Excel Spreadsheet";
                case ".pdf":
                    return "PDF Document";
                case ".jpg":
                case ".jpeg":
                    return "JPEG Image";
                case ".png":
                    return "PNG Image";
                case ".gif":
                    return "GIF Image";
                case ".mp3":
                    return "MP3 Audio";
                case ".mp4":
                    return "MP4 Video";
                default:
                    return $"{extension.TrimStart('.')} File";
            }
        }
        
        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int suffixIndex = 0;
            double size = bytes;
            
            while (size >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                size /= 1024;
                suffixIndex++;
            }
            
            return $"{size:0.##} {suffixes[suffixIndex]} ({bytes:N0} bytes)";
        }
        
        private long GetDirectorySize(DirectoryInfo dir)
        {
            long size = 0;
            
            // Add file sizes
            foreach (FileInfo file in dir.GetFiles())
            {
                size += file.Length;
            }
            
            // Add subdirectory sizes
            foreach (DirectoryInfo subdir in dir.GetDirectories())
            {
                size += GetDirectorySize(subdir);
            }
            
            return size;
        }
        
        private void AddTagButton_Click(object sender, RoutedEventArgs e)
        {
            string tag = tagTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(tag))
            {
                _metadataManager.AddTag(_filePath, tag);
                tagTextBox.Clear();
                LoadCustomMetadata();
            }
        }
        
        private void ChangeColorButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.ColorDialog colorDialog = new System.Windows.Forms.ColorDialog();
            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Color color = Color.FromArgb(
                    colorDialog.Color.A,
                    colorDialog.Color.R,
                    colorDialog.Color.G,
                    colorDialog.Color.B
                );
                
                string colorHex = color.ToString();
                _metadataManager.SetItemColor(_filePath, colorHex);
                colorIndicator.Fill = new SolidColorBrush(color);
            }
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}