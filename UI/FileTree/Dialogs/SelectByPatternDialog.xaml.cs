// UI/FileTree/Dialogs/SelectByPatternDialog.xaml.cs
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace ExplorerPro.UI.FileTree.Dialogs
{
    /// <summary>
    /// Interaction logic for SelectByPatternDialog.xaml
    /// </summary>
    public partial class SelectByPatternDialog : Window, INotifyPropertyChanged
    {
        #region Fields
        
        private string _pattern = "*.pdf";
        private bool _addToSelection;
        private bool _includeSubfolders = true;
        private ICommand _selectCommand;
        
        #endregion
        
        #region Properties
        
        /// <summary>
        /// Gets or sets the pattern string
        /// </summary>
        public string Pattern
        {
            get => _pattern;
            set
            {
                if (_pattern != value)
                {
                    _pattern = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// Gets or sets whether to add to current selection
        /// </summary>
        public bool AddToSelection
        {
            get => _addToSelection;
            set
            {
                if (_addToSelection != value)
                {
                    _addToSelection = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// Gets or sets whether to include subfolders
        /// </summary>
        public bool IncludeSubfolders
        {
            get => _includeSubfolders;
            set
            {
                if (_includeSubfolders != value)
                {
                    _includeSubfolders = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// Gets the select command
        /// </summary>
        public ICommand SelectCommand
        {
            get
            {
                if (_selectCommand == null)
                {
                    _selectCommand = new RelayCommand(OnSelect, CanSelect);
                }
                return _selectCommand;
            }
        }
        
        #endregion
        
        #region Constructor
        
        public SelectByPatternDialog(Window owner, string lastPattern = null)
        {
            InitializeComponent();
            Owner = owner;
            DataContext = this;
            
            if (!string.IsNullOrEmpty(lastPattern))
            {
                Pattern = lastPattern;
            }
            
            // Focus and select all text
            Loaded += (s, e) =>
            {
                PatternTextBox.Focus();
                PatternTextBox.SelectAll();
            };
        }
        
        #endregion
        
        #region Methods
        
        private bool CanSelect()
        {
            return !string.IsNullOrWhiteSpace(Pattern);
        }
        
        private void OnSelect()
        {
            if (CanSelect())
            {
                DialogResult = true;
                Close();
            }
        }
        
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        
        #endregion
        
        #region INotifyPropertyChanged
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        #endregion
        
        #region RelayCommand
        
        private class RelayCommand : ICommand
        {
            private readonly Action _execute;
            private readonly Func<bool> _canExecute;
            
            public RelayCommand(Action execute, Func<bool> canExecute = null)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }
            
            public event EventHandler CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }
            
            public bool CanExecute(object parameter)
            {
                return _canExecute?.Invoke() ?? true;
            }
            
            public void Execute(object parameter)
            {
                _execute();
            }
        }
        
        #endregion
    }
}