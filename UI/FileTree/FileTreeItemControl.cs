// FileTreeItemControl.cs - A custom control for efficient column rendering
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.ComponentModel;

namespace ExplorerPro.UI.FileTree
{
    /// <summary>
    /// Custom control for rendering file tree items with synchronized columns
    /// </summary>
    public class FileTreeItemControl : ContentControl, INotifyPropertyChanged
    {
        #region Dependency Properties

        public static readonly DependencyProperty ItemProperty =
            DependencyProperty.Register("Item", typeof(FileTreeItem), typeof(FileTreeItemControl),
                new PropertyMetadata(null, OnItemChanged));

        public static readonly DependencyProperty NameColumnWidthProperty =
            DependencyProperty.Register("NameColumnWidth", typeof(double), typeof(FileTreeItemControl),
                new PropertyMetadata(250.0, OnColumnWidthChanged));

        public static readonly DependencyProperty SizeColumnWidthProperty =
            DependencyProperty.Register("SizeColumnWidth", typeof(double), typeof(FileTreeItemControl),
                new PropertyMetadata(100.0, OnColumnWidthChanged));

        public static readonly DependencyProperty TypeColumnWidthProperty =
            DependencyProperty.Register("TypeColumnWidth", typeof(double), typeof(FileTreeItemControl),
                new PropertyMetadata(120.0, OnColumnWidthChanged));

        public static readonly DependencyProperty DateColumnWidthProperty =
            DependencyProperty.Register("DateColumnWidth", typeof(double), typeof(FileTreeItemControl),
                new PropertyMetadata(150.0, OnColumnWidthChanged));

        #endregion

        #region Properties

        public FileTreeItem Item
        {
            get => (FileTreeItem)GetValue(ItemProperty);
            set => SetValue(ItemProperty, value);
        }

        public double NameColumnWidth
        {
            get => (double)GetValue(NameColumnWidthProperty);
            set => SetValue(NameColumnWidthProperty, value);
        }

        public double SizeColumnWidth
        {
            get => (double)GetValue(SizeColumnWidthProperty);
            set => SetValue(SizeColumnWidthProperty, value);
        }

        public double TypeColumnWidth
        {
            get => (double)GetValue(TypeColumnWidthProperty);
            set => SetValue(TypeColumnWidthProperty, value);
        }

        public double DateColumnWidth
        {
            get => (double)GetValue(DateColumnWidthProperty);
            set => SetValue(DateColumnWidthProperty, value);
        }

        #endregion

        #region Fields

        private Grid _layoutGrid;
        private Canvas _treeLines;
        private ToggleButton _expander;
        private Image _icon;
        private TextBlock _nameText;
        private TextBlock _sizeText;
        private TextBlock _typeText;
        private TextBlock _dateText;

        // Column registry for width synchronization
        private static readonly Dictionary<string, List<WeakReference>> _columnRegistry = 
            new Dictionary<string, List<WeakReference>>();
        private static readonly object _registryLock = new object();

        #endregion

        #region Constructor

        static FileTreeItemControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FileTreeItemControl),
                new FrameworkPropertyMetadata(typeof(FileTreeItemControl)));
        }

        public FileTreeItemControl()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        #endregion

        #region Template Management

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // Create the visual structure programmatically for better performance
            CreateVisualStructure();
        }

        private void CreateVisualStructure()
        {
            _layoutGrid = new Grid();
            
            // Define columns matching the header
            _layoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(NameColumnWidth) });
            _layoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            _layoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SizeColumnWidth) });
            _layoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            _layoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(TypeColumnWidth) });
            _layoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            _layoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(DateColumnWidth) });
            _layoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Create name column content
            var namePanel = new Grid();
            
            // Tree lines
            _treeLines = new Canvas
            {
                Background = Brushes.Transparent,
                Width = 40,
                Height = 20,
                HorizontalAlignment = HorizontalAlignment.Left,
                ClipToBounds = false
            };
            namePanel.Children.Add(_treeLines);

            // Name content stack panel
            var nameStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 0) // Margin will be set based on level
            };

            // Expander
            _expander = new ToggleButton
            {
                Style = Application.Current.FindResource("Windows11ExpanderStyle") as Style,
                VerticalAlignment = VerticalAlignment.Center
            };
            nameStack.Children.Add(_expander);

            // Icon
            _icon = new Image
            {
                Width = 16,
                Height = 16,
                Margin = new Thickness(2, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            RenderOptions.SetBitmapScalingMode(_icon, BitmapScalingMode.HighQuality);
            nameStack.Children.Add(_icon);

            // Name text
            _nameText = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            nameStack.Children.Add(_nameText);

            namePanel.Children.Add(nameStack);
            Grid.SetColumn(namePanel, 0);
            _layoutGrid.Children.Add(namePanel);

            // Size text
            _sizeText = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(5, 0, 5, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(_sizeText, 2);
            _layoutGrid.Children.Add(_sizeText);

            // Type text
            _typeText = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Left,
                Margin = new Thickness(5, 0, 5, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(_typeText, 4);
            _layoutGrid.Children.Add(_typeText);

            // Date text
            _dateText = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Left,
                Margin = new Thickness(5, 0, 5, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(_dateText, 6);
            _layoutGrid.Children.Add(_dateText);

            Content = _layoutGrid;
        }

        #endregion

        #region Column Width Synchronization

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RegisterForColumnUpdates();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            UnregisterFromColumnUpdates();
        }

        private void RegisterForColumnUpdates()
        {
            lock (_registryLock)
            {
                RegisterColumn("Name", this);
                RegisterColumn("Size", this);
                RegisterColumn("Type", this);
                RegisterColumn("Date", this);
            }
        }

        private void UnregisterFromColumnUpdates()
        {
            lock (_registryLock)
            {
                UnregisterColumn("Name", this);
                UnregisterColumn("Size", this);
                UnregisterColumn("Type", this);
                UnregisterColumn("Date", this);
            }
        }

        private static void RegisterColumn(string columnName, FileTreeItemControl control)
        {
            if (!_columnRegistry.ContainsKey(columnName))
            {
                _columnRegistry[columnName] = new List<WeakReference>();
            }

            // Clean up dead references
            _columnRegistry[columnName].RemoveAll(wr => !wr.IsAlive);
            
            // Add new reference
            _columnRegistry[columnName].Add(new WeakReference(control));
        }

        private static void UnregisterColumn(string columnName, FileTreeItemControl control)
        {
            if (_columnRegistry.ContainsKey(columnName))
            {
                _columnRegistry[columnName].RemoveAll(wr => !wr.IsAlive || wr.Target == control);
            }
        }

        public static void UpdateColumnWidth(string columnName, double newWidth)
        {
            lock (_registryLock)
            {
                if (!_columnRegistry.ContainsKey(columnName))
                    return;

                // Clean up dead references
                _columnRegistry[columnName].RemoveAll(wr => !wr.IsAlive);

                // Update all live controls
                foreach (var weakRef in _columnRegistry[columnName])
                {
                    if (weakRef.Target is FileTreeItemControl control)
                    {
                        control.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                        {
                            control.UpdateColumnWidthInternal(columnName, newWidth);
                        }));
                    }
                }
            }
        }

        private void UpdateColumnWidthInternal(string columnName, double newWidth)
        {
            if (_layoutGrid == null) return;

            int columnIndex = GetColumnIndex(columnName);
            if (columnIndex >= 0 && columnIndex < _layoutGrid.ColumnDefinitions.Count)
            {
                _layoutGrid.ColumnDefinitions[columnIndex].Width = new GridLength(newWidth);
            }
        }

        private int GetColumnIndex(string columnName)
        {
            switch (columnName)
            {
                case "Name": return 0;
                case "Size": return 2;
                case "Type": return 4;
                case "Date": case "DateModified": return 6;
                default: return -1;
            }
        }

        #endregion

        #region Property Changed Handlers

        private static void OnItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FileTreeItemControl control)
            {
                control.UpdateVisuals();
            }
        }

        private static void OnColumnWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FileTreeItemControl control)
            {
                control.UpdateColumnWidths();
            }
        }

        private void UpdateVisuals()
        {
            if (Item == null) return;

            // Update all visual elements
            UpdateTreeLines();
            UpdateExpander();
            UpdateIcon();
            UpdateTexts();
        }

        private void UpdateTreeLines()
        {
            // Implementation for tree lines based on Item.Level
        }

        private void UpdateExpander()
        {
            if (_expander != null)
            {
                _expander.IsChecked = Item?.IsExpanded ?? false;
                _expander.Visibility = (Item?.IsDirectory == true && Item?.HasChildren == true) 
                    ? Visibility.Visible : Visibility.Hidden;
            }
        }

        private void UpdateIcon()
        {
            if (_icon != null && Item != null)
            {
                _icon.Source = Item.Icon;
            }
        }

        private void UpdateTexts()
        {
            if (Item == null) return;

            if (_nameText != null)
            {
                _nameText.Text = Item.Name;
                _nameText.Foreground = Item.Foreground;
                _nameText.FontWeight = Item.FontWeight;
                _nameText.ToolTip = Item.Path;
            }

            if (_sizeText != null)
            {
                _sizeText.Text = Item.Size;
                _sizeText.Foreground = Item.Foreground;
                _sizeText.FontWeight = Item.FontWeight;
            }

            if (_typeText != null)
            {
                _typeText.Text = Item.Type;
                _typeText.Foreground = Item.Foreground;
                _typeText.FontWeight = Item.FontWeight;
            }

            if (_dateText != null)
            {
                _dateText.Text = Item.LastModifiedStr;
                _dateText.Foreground = Item.Foreground;
                _dateText.FontWeight = Item.FontWeight;
            }
        }

        private void UpdateColumnWidths()
        {
            if (_layoutGrid == null) return;

            _layoutGrid.ColumnDefinitions[0].Width = new GridLength(NameColumnWidth);
            _layoutGrid.ColumnDefinitions[2].Width = new GridLength(SizeColumnWidth);
            _layoutGrid.ColumnDefinitions[4].Width = new GridLength(TypeColumnWidth);
            _layoutGrid.ColumnDefinitions[6].Width = new GridLength(DateColumnWidth);
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}