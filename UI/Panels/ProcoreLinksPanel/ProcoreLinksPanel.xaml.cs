using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Newtonsoft.Json;
using System.Windows.Media;

namespace ExplorerPro.UI.Panels.ProcoreLinksPanel
{
    /// <summary>
    /// Interaction logic for ProcoreLinksPanel.xaml
    /// </summary>
    public partial class ProcoreLinksPanel : UserControl
    {
        private string _dataFile = "Data/procore_links.json";
        private Dictionary<string, ProjectData> _projects = new Dictionary<string, ProjectData>();
        private bool _initialized = false;
        private HashSet<string> _expandedItems = new HashSet<string>();

        public ProcoreLinksPanel()
        {
            InitializeComponent();
            
            // Initialize with the default data file path
            Loaded += (s, e) => 
            {
                if (!_initialized)
                {
                    LoadLinks();
                    PopulateTree();
                    _initialized = true;
                }
            };
        }

        #region Models

        /// <summary>
        /// Represents a link with a URL and tags
        /// </summary>
        public class LinkData
        {
            public string Url { get; set; }
            public List<string> Tags { get; set; } = new List<string>();
        }

        /// <summary>
        /// Represents a project with its links and tags
        /// </summary>
        public class ProjectData : Dictionary<string, object>
        {
            public List<string> Tags 
            { 
                get 
                {
                    if (ContainsKey("_tags") && this["_tags"] is List<string> tags)
                        return tags;
                    return new List<string>();
                }
                set
                {
                    this["_tags"] = value;
                }
            }
            
            public ProjectData()
            {
                this["_tags"] = new List<string>();
            }
            
            public LinkData GetLink(string linkName)
            {
                if (!ContainsKey(linkName) || linkName == "_tags")
                    return null;
                
                if (this[linkName] is LinkData linkData)
                    return linkData;
                
                // Handle conversion from dictionary to LinkData object
                if (this[linkName] is Dictionary<string, object> dict)
                {
                    LinkData link = new LinkData 
                    { 
                        Url = dict.ContainsKey("url") ? dict["url"].ToString() : string.Empty
                    };
                    
                    if (dict.ContainsKey("tags") && dict["tags"] is List<object> tagObjects)
                    {
                        link.Tags = tagObjects.Select(t => t.ToString()).ToList();
                    }
                    
                    return link;
                }
                
                // Handle conversion from plain string to LinkData
                if (this[linkName] is string urlString)
                {
                    LinkData link = new LinkData
                    {
                        Url = urlString,
                        Tags = new List<string>()
                    };
                    
                    // Convert to proper LinkData format
                    this[linkName] = link;
                    
                    return link;
                }
                
                return null;
            }
            
            public void SetLink(string linkName, LinkData linkData)
            {
                this[linkName] = linkData;
            }
        }

        #endregion

        #region File Handling

        /// <summary>
        /// Loads projects and links from the JSON file
        /// </summary>
        private void LoadLinks()
        {
            try
            {
                if (File.Exists(_dataFile) && new FileInfo(_dataFile).Length > 0)
                {
                    string json = File.ReadAllText(_dataFile);
                    var data = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, object>>>(json);
                    
                    if (data != null)
                    {
                        _projects.Clear();
                        
                        // Convert the raw dictionaries to our ProjectData objects
                        foreach (var projectPair in data)
                        {
                            var projectData = new ProjectData();
                            
                            // First add the tags
                            if (projectPair.Value.ContainsKey("_tags") && projectPair.Value["_tags"] is Newtonsoft.Json.Linq.JArray tagsArray)
                            {
                                projectData.Tags = tagsArray.ToObject<List<string>>();
                            }
                            else
                            {
                                projectData.Tags = new List<string>();
                            }
                            
                            // Then add all the links
                            foreach (var linkPair in projectPair.Value)
                            {
                                if (linkPair.Key == "_tags")
                                    continue;
                                
                                // Handle different data formats
                                if (linkPair.Value is string urlString)
                                {
                                    // Old format: plain string URL
                                    projectData[linkPair.Key] = new LinkData 
                                    { 
                                        Url = urlString,
                                        Tags = new List<string>()
                                    };
                                }
                                else if (linkPair.Value is Newtonsoft.Json.Linq.JObject linkObj)
                                {
                                    // New format: {"url": "...", "tags": [...]}
                                    var linkData = new LinkData
                                    {
                                        Url = linkObj["url"]?.ToString() ?? string.Empty,
                                        Tags = linkObj["tags"]?.ToObject<List<string>>() ?? new List<string>()
                                    };
                                    projectData[linkPair.Key] = linkData;
                                }
                            }
                            
                            _projects[projectPair.Key] = projectData;
                        }
                    }
                    else
                    {
                        // Json deserialized to null - start with empty projects
                        _projects = new Dictionary<string, ProjectData>();
                    }
                }
                else
                {
                    // File doesn't exist or is empty - start with empty projects
                    _projects = new Dictionary<string, ProjectData>();
                    
                    // Create the directory if it doesn't exist
                    string? directory = Path.GetDirectoryName(_dataFile);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    
                    // Create an empty file with valid JSON
                    File.WriteAllText(_dataFile, "{}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load links: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _projects = new Dictionary<string, ProjectData>();
            }
        }

        /// <summary>
        /// Saves the current projects dictionary to JSON
        /// </summary>
        private void SaveLinks()
        {
            try
            {
                if (_projects.Count > 0)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_dataFile));
                    string json = JsonConvert.SerializeObject(_projects, Formatting.Indented);
                    File.WriteAllText(_dataFile, json);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save links: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region TreeView Management

        /// <summary>
        /// Stores the expansion state of tree items
        /// </summary>
        private void StoreExpandedItems()
        {
            _expandedItems.Clear();
            StoreExpandedItemsRecursive(LinksTreeView.Items);
        }

        private void StoreExpandedItemsRecursive(ItemCollection items)
        {
            foreach (TreeViewItem item in items.OfType<TreeViewItem>())
            {
                if (item.IsExpanded)
                {
                    _expandedItems.Add(item.Header.ToString());
                }
                
                StoreExpandedItemsRecursive(item.Items);
            }
        }

        /// <summary>
        /// Restores the expansion state of tree items
        /// </summary>
        private void RestoreExpandedItems()
        {
            RestoreExpandedItemsRecursive(LinksTreeView.Items);
        }

        private void RestoreExpandedItemsRecursive(ItemCollection items)
        {
            foreach (TreeViewItem item in items.OfType<TreeViewItem>())
            {
                if (_expandedItems.Contains(item.Header.ToString()))
                {
                    item.IsExpanded = true;
                }
                
                RestoreExpandedItemsRecursive(item.Items);
            }
        }

        /// <summary>
        /// Populates the TreeView with project/link items
        /// </summary>
        private void PopulateTree()
        {
            StoreExpandedItems();
            LinksTreeView.Items.Clear();

            foreach (var projectPair in _projects)
            {
                // Create project item
                var projectItem = new TreeViewItem
                {
                    Header = projectPair.Key,
                    Tag = projectPair.Value
                };
                
                // Set tooltip with tags
                if (projectPair.Value.Tags.Any())
                {
                    projectItem.ToolTip = $"Tags: {string.Join(", ", projectPair.Value.Tags)}";
                }
                else
                {
                    projectItem.ToolTip = "No tags";
                }
                
                LinksTreeView.Items.Add(projectItem);
                
                // Add links as child items
                foreach (var linkKey in projectPair.Value.Keys)
                {
                    if (linkKey == "_tags")
                        continue;
                    
                    LinkData linkData = projectPair.Value.GetLink(linkKey);
                    if (linkData == null)
                        continue;
                    
                    var linkItem = new TreeViewItem
                    {
                        Header = linkKey,
                        Tag = linkData
                    };
                    
                    // Set tooltip with tags
                    if (linkData.Tags.Any())
                    {
                        linkItem.ToolTip = $"Tags: {string.Join(", ", linkData.Tags)}";
                    }
                    else
                    {
                        linkItem.ToolTip = "No tags";
                    }
                    
                    projectItem.Items.Add(linkItem);
                }
            }

            RestoreExpandedItems();
        }

        /// <summary>
        /// Filters the tree based on search text
        /// </summary>
        private void FilterTree(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                // Show all items
                foreach (TreeViewItem projectItem in LinksTreeView.Items)
                {
                    projectItem.Visibility = Visibility.Visible;
                    
                    foreach (TreeViewItem linkItem in projectItem.Items)
                    {
                        linkItem.Visibility = Visibility.Visible;
                    }
                    
                    if (projectItem.Items.Count > 0)
                    {
                        projectItem.IsExpanded = true;
                    }
                }
                return;
            }
            
            string query = searchText.ToLower();
            
            foreach (TreeViewItem projectItem in LinksTreeView.Items)
            {
                bool projectVisible = false;
                string projectName = projectItem.Header.ToString();
                ProjectData projectData = projectItem.Tag as ProjectData;
                
                // Check project name
                if (projectName.ToLower().Contains(query))
                {
                    projectVisible = true;
                }
                
                // Check project tags
                if (projectData != null && 
                    projectData.Tags.Any(tag => tag.ToLower().Contains(query)))
                {
                    projectVisible = true;
                }
                
                // Check each link
                bool anyLinkVisible = false;
                foreach (TreeViewItem linkItem in projectItem.Items)
                {
                    string linkName = linkItem.Header.ToString();
                    LinkData linkData = linkItem.Tag as LinkData;
                    
                    bool nameMatch = linkName.ToLower().Contains(query);
                    bool urlMatch = false;
                    bool tagMatch = false;
                    
                    if (linkData != null)
                    {
                        // Check URL
                        urlMatch = linkData.Url.ToLower().Contains(query);
                        
                        // Check link tags
                        tagMatch = linkData.Tags.Any(tag => tag.ToLower().Contains(query));
                    }
                    
                    bool linkVisible = nameMatch || urlMatch || tagMatch;
                    linkItem.Visibility = linkVisible ? Visibility.Visible : Visibility.Collapsed;
                    
                    if (linkVisible)
                    {
                        anyLinkVisible = true;
                    }
                }
                
                // If any link is visible, the project should also be visible
                if (anyLinkVisible)
                {
                    projectVisible = true;
                    projectItem.IsExpanded = true;
                }
                
                projectItem.Visibility = projectVisible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        #endregion

        #region Event Handlers

        private void SearchBar_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterTree(SearchBar.Text);
        }

        private void AddProjectButton_Click(object sender, RoutedEventArgs e)
        {
            AddProject();
        }

        private void AddLinkButton_Click(object sender, RoutedEventArgs e)
        {
            AddLink();
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            RemoveSelected();
        }

        private void LinksTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            TreeViewItem selectedItem = LinksTreeView.SelectedItem as TreeViewItem;
            if (selectedItem == null)
                return;
            
            if (selectedItem.Parent is TreeViewItem) // It's a link
            {
                OpenSelectedLink(selectedItem);
            }
        }

        private void LinksTreeView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            // Context menu is handled in ContextMenu_Opened
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            var contextMenu = sender as ContextMenu;
            if (contextMenu == null)
                return;
            
            contextMenu.Items.Clear();
            
            // Get the TreeViewItem this context menu belongs to
            var treeViewItem = FindAncestor<TreeViewItem>((DependencyObject)contextMenu.PlacementTarget);
            
            if (treeViewItem == null)
            {
                // Clicked in empty area
                var sortAllProjectsItem = new MenuItem { Header = "Sort All Projects" };
                sortAllProjectsItem.Click += (s, args) => SortAllProjectsAndLinks();
                contextMenu.Items.Add(sortAllProjectsItem);
            }
            else if (treeViewItem.Parent is TreeViewItem)
            {
                // Link context menu
                var openLinkItem = new MenuItem { Header = "Open Link" };
                openLinkItem.Click += (s, args) => OpenSelectedLink(treeViewItem);
                
                var renameLinkItem = new MenuItem { Header = "Rename Link" };
                renameLinkItem.Click += (s, args) => RenameLink(treeViewItem);
                
                var copyLinkItem = new MenuItem { Header = "Copy URL" };
                copyLinkItem.Click += (s, args) => CopyLink(treeViewItem);
                
                var addTagItem = new MenuItem { Header = "Add Tag" };
                addTagItem.Click += (s, args) => TagLink(treeViewItem);
                
                var removeTagItem = new MenuItem { Header = "Remove Tag" };
                removeTagItem.Click += (s, args) => RemoveTagFromLink(treeViewItem);
                
                var deleteLinkItem = new MenuItem { Header = "Delete Link" };
                deleteLinkItem.Click += (s, args) => RemoveSelected();
                
                contextMenu.Items.Add(openLinkItem);
                contextMenu.Items.Add(renameLinkItem);
                contextMenu.Items.Add(copyLinkItem);
                contextMenu.Items.Add(addTagItem);
                contextMenu.Items.Add(removeTagItem);
                contextMenu.Items.Add(deleteLinkItem);
            }
            else
            {
                // Project context menu
                var renameProjectItem = new MenuItem { Header = "Rename Project" };
                renameProjectItem.Click += (s, args) => RenameProject(treeViewItem);
                
                var addProjectTagItem = new MenuItem { Header = "Add Tag to Project" };
                addProjectTagItem.Click += (s, args) => TagProject(treeViewItem);
                
                var removeProjectTagItem = new MenuItem { Header = "Remove Tag from Project" };
                removeProjectTagItem.Click += (s, args) => RemoveTagFromProject(treeViewItem);
                
                var addLinkToProjectItem = new MenuItem { Header = "Add Link" };
                addLinkToProjectItem.Click += (s, args) => AddLinkForProject(treeViewItem);
                
                var deleteProjectItem = new MenuItem { Header = "Delete Project" };
                deleteProjectItem.Click += (s, args) => RemoveSelected();
                
                contextMenu.Items.Add(renameProjectItem);
                contextMenu.Items.Add(addProjectTagItem);
                contextMenu.Items.Add(removeProjectTagItem);
                contextMenu.Items.Add(addLinkToProjectItem);
                contextMenu.Items.Add(deleteProjectItem);
            }
        }

        #endregion

        #region Actions

        /// <summary>
        /// Opens the selected link in a web browser
        /// </summary>
        private void OpenSelectedLink(TreeViewItem linkItem)
        {
            if (linkItem == null)
                return;
            
            var linkData = linkItem.Tag as LinkData;
            if (linkData != null && !string.IsNullOrEmpty(linkData.Url))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = linkData.Url,
                    UseShellExecute = true
                });
            }
        }

        /// <summary>
        /// Adds a new project to the tree
        /// </summary>
        private void AddProject()
        {
            var dialog = new InputDialog("Add Project", "Enter project name:");
            if (dialog.ShowDialog() == true)
            {
                string projectName = dialog.ResponseText;
                if (!string.IsNullOrWhiteSpace(projectName) && !_projects.ContainsKey(projectName))
                {
                    _projects[projectName] = new ProjectData();
                    SaveLinks();
                    PopulateTree();
                    
                    // Expand the new project
                    foreach (TreeViewItem item in LinksTreeView.Items)
                    {
                        if (item.Header.ToString() == projectName)
                        {
                            item.IsExpanded = true;
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Adds a link to the selected project
        /// </summary>
        private void AddLink()
        {
            TreeViewItem selectedItem = LinksTreeView.SelectedItem as TreeViewItem;
            if (selectedItem == null)
            {
                MessageBox.Show("Please select a project first.", "Select Project", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            TreeViewItem projectItem;
            if (selectedItem.Parent is TreeViewItem) // It's a link
            {
                projectItem = selectedItem.Parent as TreeViewItem;
            }
            else // It's a project
            {
                projectItem = selectedItem;
            }
            
            string projectName = projectItem.Header.ToString();
            
            // Get link title
            var titleDialog = new InputDialog("Add Link", "Enter link title:");
            if (titleDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(titleDialog.ResponseText))
                return;
            
            string linkTitle = titleDialog.ResponseText;
            
            // Get link URL
            var urlDialog = new InputDialog("Add Link", "Enter URL:");
            if (urlDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(urlDialog.ResponseText))
                return;
            
            string linkUrl = urlDialog.ResponseText;
            
            if (_projects.ContainsKey(projectName))
            {
                _projects[projectName][linkTitle] = new LinkData 
                { 
                    Url = linkUrl,
                    Tags = new List<string>()
                };
                
                SaveLinks();
                PopulateTree();
                
                // Expand the project
                foreach (TreeViewItem item in LinksTreeView.Items)
                {
                    if (item.Header.ToString() == projectName)
                    {
                        item.IsExpanded = true;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Adds a link to the specified project
        /// </summary>
        private void AddLinkForProject(TreeViewItem projectItem)
        {
            if (projectItem == null)
                return;
            
            string projectName = projectItem.Header.ToString();
            if (!_projects.ContainsKey(projectName))
            {
                MessageBox.Show($"Project '{projectName}' not found.", "Invalid Project", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Get link title
            var titleDialog = new InputDialog("Add Link", "Enter link title:");
            if (titleDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(titleDialog.ResponseText))
                return;
            
            string linkTitle = titleDialog.ResponseText;
            
            // Get link URL
            var urlDialog = new InputDialog("Add Link", "Enter URL:");
            if (urlDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(urlDialog.ResponseText))
                return;
            
            string linkUrl = urlDialog.ResponseText;
            
            _projects[projectName][linkTitle] = new LinkData 
            { 
                Url = linkUrl,
                Tags = new List<string>()
            };
            
            SaveLinks();
            PopulateTree();
            
            // Expand the project
            foreach (TreeViewItem item in LinksTreeView.Items)
            {
                if (item.Header.ToString() == projectName)
                {
                    item.IsExpanded = true;
                    break;
                }
            }
        }

        /// <summary>
        /// Removes the selected item from the tree
        /// </summary>
        private void RemoveSelected()
        {
            TreeViewItem selectedItem = LinksTreeView.SelectedItem as TreeViewItem;
            if (selectedItem == null)
            {
                MessageBox.Show("Please select a project or link to remove.", "Select Item", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            if (selectedItem.Parent is TreeViewItem) // It's a link
            {
                TreeViewItem projectItem = selectedItem.Parent as TreeViewItem;
                string projectName = projectItem.Header.ToString();
                string linkTitle = selectedItem.Header.ToString();
                
                if (_projects.ContainsKey(projectName) && _projects[projectName].ContainsKey(linkTitle))
                {
                    _projects[projectName].Remove(linkTitle);
                    SaveLinks();
                    PopulateTree();
                }
            }
            else // It's a project
            {
                string projectName = selectedItem.Header.ToString();
                if (_projects.ContainsKey(projectName))
                {
                    _projects.Remove(projectName);
                    SaveLinks();
                    PopulateTree();
                }
            }
        }

        /// <summary>
        /// Renames the selected project
        /// </summary>
        private void RenameProject(TreeViewItem projectItem)
        {
            if (projectItem == null)
                return;
            
            string oldName = projectItem.Header.ToString();
            
            var dialog = new InputDialog("Rename Project", "Enter new name:", oldName);
            if (dialog.ShowDialog() == true)
            {
                string newName = dialog.ResponseText;
                if (!string.IsNullOrWhiteSpace(newName) && _projects.ContainsKey(oldName))
                {
                    var projectData = _projects[oldName];
                    _projects.Remove(oldName);
                    _projects[newName] = projectData;
                    
                    SaveLinks();
                    PopulateTree();
                }
            }
        }

        /// <summary>
        /// Renames the selected link
        /// </summary>
        private void RenameLink(TreeViewItem linkItem)
        {
            if (linkItem == null || !(linkItem.Parent is TreeViewItem projectItem))
                return;
            
            string projectName = projectItem.Header.ToString();
            string oldName = linkItem.Header.ToString();
            LinkData linkData = linkItem.Tag as LinkData;
            
            if (linkData == null)
                return;
            
            var dialog = new InputDialog("Rename Link", "Enter new name:", oldName);
            if (dialog.ShowDialog() == true)
            {
                string newName = dialog.ResponseText;
                if (!string.IsNullOrWhiteSpace(newName) && 
                    _projects.ContainsKey(projectName) && 
                    _projects[projectName].ContainsKey(oldName))
                {
                    _projects[projectName].Remove(oldName);
                    _projects[projectName][newName] = linkData;
                    
                    SaveLinks();
                    PopulateTree();
                }
            }
        }

        /// <summary>
        /// Copies the link URL to clipboard
        /// </summary>
        private void CopyLink(TreeViewItem linkItem)
        {
            if (linkItem == null)
                return;
            
            var linkData = linkItem.Tag as LinkData;
            if (linkData != null && !string.IsNullOrEmpty(linkData.Url))
            {
                Clipboard.SetText(linkData.Url);
                MessageBox.Show($"Copied: {linkData.Url}", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Adds a tag to the selected link
        /// </summary>
        private void TagLink(TreeViewItem linkItem)
        {
            if (linkItem == null || !(linkItem.Parent is TreeViewItem projectItem))
                return;
            
            string projectName = projectItem.Header.ToString();
            string linkName = linkItem.Header.ToString();
            LinkData linkData = linkItem.Tag as LinkData;
            
            if (linkData == null)
                return;
            
            var dialog = new InputDialog("Add Tag", $"Enter a tag for link '{linkName}':");
            if (dialog.ShowDialog() == true)
            {
                string tag = dialog.ResponseText;
                if (!string.IsNullOrWhiteSpace(tag) && !linkData.Tags.Contains(tag))
                {
                    linkData.Tags.Add(tag);
                    
                    SaveLinks();
                    PopulateTree();
                    
                    MessageBox.Show($"Tag '{tag}' added to link '{linkName}'.", "Tag Added", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else if (linkData.Tags.Contains(tag))
                {
                    MessageBox.Show($"Link '{linkName}' already has tag '{tag}'.", "Tag Exists", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        /// <summary>
        /// Removes a tag from the selected link
        /// </summary>
        private void RemoveTagFromLink(TreeViewItem linkItem)
        {
            if (linkItem == null || !(linkItem.Parent is TreeViewItem projectItem))
                return;
            
            string projectName = projectItem.Header.ToString();
            string linkName = linkItem.Header.ToString();
            LinkData linkData = linkItem.Tag as LinkData;
            
            if (linkData == null || !linkData.Tags.Any())
            {
                MessageBox.Show($"No tags available for link '{linkName}'.", "No Tags", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            var dialog = new TagSelectionDialog("Remove Tag", $"Select a tag to remove from link '{linkName}':", linkData.Tags.ToArray());
            if (dialog.ShowDialog() == true)
            {
                string tag = dialog.SelectedTag;
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    linkData.Tags.Remove(tag);
                    
                    SaveLinks();
                    PopulateTree();
                    
                    MessageBox.Show($"Removed '{tag}' from link '{linkName}'.", "Tag Removed", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        /// <summary>
        /// Adds a tag to the selected project
        /// </summary>
        private void TagProject(TreeViewItem projectItem)
        {
            if (projectItem == null)
                return;
            
            string projectName = projectItem.Header.ToString();
            ProjectData projectData = projectItem.Tag as ProjectData;
            
            if (projectData == null)
                return;
            
            var dialog = new InputDialog("Add Tag to Project", $"Enter a tag for project '{projectName}':");
            if (dialog.ShowDialog() == true)
            {
                string tag = dialog.ResponseText;
                if (!string.IsNullOrWhiteSpace(tag) && !projectData.Tags.Contains(tag))
                {
                    projectData.Tags.Add(tag);
                    
                    SaveLinks();
                    PopulateTree();
                    
                    MessageBox.Show($"Tag '{tag}' added to project '{projectName}'.", "Tag Added", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else if (projectData.Tags.Contains(tag))
                {
                    MessageBox.Show($"Project '{projectName}' already has tag '{tag}'.", "Tag Exists", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        /// <summary>
        /// Removes a tag from the selected project
        /// </summary>
        private void RemoveTagFromProject(TreeViewItem projectItem)
        {
            if (projectItem == null)
                return;
            
            string projectName = projectItem.Header.ToString();
            ProjectData projectData = projectItem.Tag as ProjectData;
            
            if (projectData == null || !projectData.Tags.Any())
            {
                MessageBox.Show($"No tags available for project '{projectName}'.", "No Tags", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            var dialog = new TagSelectionDialog("Remove Project Tag", $"Select a tag to remove from project '{projectName}':", projectData.Tags.ToArray());
            if (dialog.ShowDialog() == true)
            {
                string tag = dialog.SelectedTag;
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    projectData.Tags.Remove(tag);
                    
                    SaveLinks();
                    PopulateTree();
                    
                    MessageBox.Show($"Removed '{tag}' from '{projectName}'.", "Tag Removed", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        /// <summary>
        /// Sorts all projects and their links alphabetically
        /// </summary>
        private void SortAllProjectsAndLinks()
        {
            var sortedProjects = new Dictionary<string, ProjectData>();
            
            // Get projects as list and sort them
            var projectsList = _projects.ToList();
            projectsList.Sort((p1, p2) => 
            {
                // Text-based projects come first, numeric projects last
                bool isP1Numeric = p1.Key.Length > 0 && char.IsDigit(p1.Key[0]);
                bool isP2Numeric = p2.Key.Length > 0 && char.IsDigit(p2.Key[0]);
                
                if (isP1Numeric == isP2Numeric)
                {
                    return string.Compare(p1.Key, p2.Key, StringComparison.OrdinalIgnoreCase);
                }
                
                return isP1Numeric ? 1 : -1;
            });
            
            // Rebuild the dictionary in sorted order, also sorting links
            foreach (var projectPair in projectsList)
            {
                var projectData = projectPair.Value;
                var newProjectData = new ProjectData();
                
                // First add the tags
                newProjectData.Tags = projectData.Tags;
                
                // Get all links
                var linksList = new List<KeyValuePair<string, object>>();
                foreach (var key in projectData.Keys)
                {
                    if (key == "_tags")
                        continue;
                    
                    var linkData = projectData.GetLink(key);
                    if (linkData != null)
                    {
                        linksList.Add(new KeyValuePair<string, object>(key, linkData));
                    }
                }
                
                // Sort links alphabetically
                linksList.Sort((l1, l2) => string.Compare(l1.Key, l2.Key, StringComparison.OrdinalIgnoreCase));
                
                // Add sorted links
                foreach (var linkPair in linksList)
                {
                    newProjectData[linkPair.Key] = linkPair.Value;
                }
                
                sortedProjects[projectPair.Key] = newProjectData;
            }
            
            _projects = sortedProjects;
            SaveLinks();
            PopulateTree();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Finds an ancestor of a specified type
        /// </summary>
        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T)
                {
                    return (T)current;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        #endregion
    }

    /// <summary>
    /// Simple dialog to get text input from the user
    /// </summary>
    public class InputDialog : Window
    {
        private TextBox _textBox;
        public string ResponseText { get; private set; }

        public InputDialog(string title, string prompt, string defaultValue = "")
        {
            Title = title;
            Width = 350;
            Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            
            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            // Prompt
            TextBlock promptBlock = new TextBlock
            {
                Text = prompt,
                Margin = new Thickness(10, 10, 10, 5)
            };
            Grid.SetRow(promptBlock, 0);
            grid.Children.Add(promptBlock);
            
            // TextBox
            _textBox = new TextBox
            {
                Text = defaultValue,
                Margin = new Thickness(10, 5, 10, 10)
            };
            Grid.SetRow(_textBox, 1);
            grid.Children.Add(_textBox);
            
            // Buttons
            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };
            
            Button okButton = new Button
            {
                Content = "OK",
                Width = 75,
                Height = 23,
                Margin = new Thickness(0, 0, 5, 0),
                IsDefault = true
            };
            okButton.Click += (s, e) => 
            {
                ResponseText = _textBox.Text;
                DialogResult = true;
            };
            
            Button cancelButton = new Button
            {
                Content = "Cancel",
                Width = 75,
                Height = 23,
                IsCancel = true
            };
            
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            
            Grid.SetRow(buttonPanel, 2);
            grid.Children.Add(buttonPanel);
            
            Content = grid;
            
            Loaded += (s, e) => 
            {
                _textBox.SelectAll();
                _textBox.Focus();
            };
        }
    }

    /// <summary>
    /// Dialog to select a tag from a list
    /// </summary>
    public class TagSelectionDialog : Window
    {
        private ComboBox _comboBox;
        public string SelectedTag { get; private set; }

        public TagSelectionDialog(string title, string prompt, string[] tags)
        {
            Title = title;
            Width = 350;
            Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            
            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            // Prompt
            TextBlock promptBlock = new TextBlock
            {
                Text = prompt,
                Margin = new Thickness(10, 10, 10, 5)
            };
            Grid.SetRow(promptBlock, 0);
            grid.Children.Add(promptBlock);
            
            // ComboBox
            _comboBox = new ComboBox
            {
                ItemsSource = tags,
                SelectedIndex = 0,
                Margin = new Thickness(10, 5, 10, 10)
            };
            Grid.SetRow(_comboBox, 1);
            grid.Children.Add(_comboBox);
            
            // Buttons
            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };
            
            Button okButton = new Button
            {
                Content = "OK",
                Width = 75,
                Height = 23,
                Margin = new Thickness(0, 0, 5, 0),
                IsDefault = true
            };
            okButton.Click += (s, e) => 
            {
                SelectedTag = _comboBox.SelectedItem as string;
                DialogResult = true;
            };
            
            Button cancelButton = new Button
            {
                Content = "Cancel",
                Width = 75,
                Height = 23,
                IsCancel = true
            };
            
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            
            Grid.SetRow(buttonPanel, 2);
            grid.Children.Add(buttonPanel);
            
            Content = grid;
        }
    }
}