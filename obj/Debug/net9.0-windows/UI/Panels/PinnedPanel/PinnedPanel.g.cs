﻿#pragma checksum "..\..\..\..\..\..\UI\Panels\PinnedPanel\PinnedPanel.xaml" "{ff1816ec-aa5e-4d10-87f7-6f4963833460}" "99F11A25950BA4F10837203945423B5D66B5EC32"
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using ExplorerPro.UI.Panels.PinnedPanel;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;


namespace ExplorerPro.UI.Panels.PinnedPanel {
    
    
    /// <summary>
    /// PinnedPanel
    /// </summary>
    public partial class PinnedPanel : System.Windows.Controls.DockPanel, System.Windows.Markup.IComponentConnector, System.Windows.Markup.IStyleConnector {
        
        
        #line 13 "..\..\..\..\..\..\UI\Panels\PinnedPanel\PinnedPanel.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.TreeView pinnedTree;
        
        #line default
        #line hidden
        
        
        #line 36 "..\..\..\..\..\..\UI\Panels\PinnedPanel\PinnedPanel.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.ContextMenu treeContextMenu;
        
        #line default
        #line hidden
        
        private bool _contentLoaded;
        
        /// <summary>
        /// InitializeComponent
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "9.0.4.0")]
        public void InitializeComponent() {
            if (_contentLoaded) {
                return;
            }
            _contentLoaded = true;
            System.Uri resourceLocater = new System.Uri("/ExplorerPro;component/ui/panels/pinnedpanel/pinnedpanel.xaml", System.UriKind.Relative);
            
            #line 1 "..\..\..\..\..\..\UI\Panels\PinnedPanel\PinnedPanel.xaml"
            System.Windows.Application.LoadComponent(this, resourceLocater);
            
            #line default
            #line hidden
        }
        
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "9.0.4.0")]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1800:DoNotCastUnnecessarily")]
        void System.Windows.Markup.IComponentConnector.Connect(int connectionId, object target) {
            switch (connectionId)
            {
            case 1:
            this.pinnedTree = ((System.Windows.Controls.TreeView)(target));
            
            #line 15 "..\..\..\..\..\..\UI\Panels\PinnedPanel\PinnedPanel.xaml"
            this.pinnedTree.MouseMove += new System.Windows.Input.MouseEventHandler(this.PinnedTree_MouseMove);
            
            #line default
            #line hidden
            
            #line 16 "..\..\..\..\..\..\UI\Panels\PinnedPanel\PinnedPanel.xaml"
            this.pinnedTree.PreviewMouseLeftButtonDown += new System.Windows.Input.MouseButtonEventHandler(this.PinnedTree_PreviewMouseLeftButtonDown);
            
            #line default
            #line hidden
            
            #line 17 "..\..\..\..\..\..\UI\Panels\PinnedPanel\PinnedPanel.xaml"
            this.pinnedTree.PreviewMouseLeftButtonUp += new System.Windows.Input.MouseButtonEventHandler(this.PinnedTree_PreviewMouseLeftButtonUp);
            
            #line default
            #line hidden
            
            #line 18 "..\..\..\..\..\..\UI\Panels\PinnedPanel\PinnedPanel.xaml"
            this.pinnedTree.MouseDoubleClick += new System.Windows.Input.MouseButtonEventHandler(this.PinnedTree_MouseDoubleClick);
            
            #line default
            #line hidden
            
            #line 19 "..\..\..\..\..\..\UI\Panels\PinnedPanel\PinnedPanel.xaml"
            this.pinnedTree.ContextMenuOpening += new System.Windows.Controls.ContextMenuEventHandler(this.PinnedTree_ContextMenuOpening);
            
            #line default
            #line hidden
            
            #line 21 "..\..\..\..\..\..\UI\Panels\PinnedPanel\PinnedPanel.xaml"
            this.pinnedTree.DragEnter += new System.Windows.DragEventHandler(this.PinnedTree_DragEnter);
            
            #line default
            #line hidden
            
            #line 22 "..\..\..\..\..\..\UI\Panels\PinnedPanel\PinnedPanel.xaml"
            this.pinnedTree.DragOver += new System.Windows.DragEventHandler(this.PinnedTree_DragOver);
            
            #line default
            #line hidden
            
            #line 23 "..\..\..\..\..\..\UI\Panels\PinnedPanel\PinnedPanel.xaml"
            this.pinnedTree.Drop += new System.Windows.DragEventHandler(this.PinnedTree_Drop);
            
            #line default
            #line hidden
            
            #line 24 "..\..\..\..\..\..\UI\Panels\PinnedPanel\PinnedPanel.xaml"
            this.pinnedTree.DragLeave += new System.Windows.DragEventHandler(this.PinnedTree_DragLeave);
            
            #line default
            #line hidden
            return;
            case 3:
            this.treeContextMenu = ((System.Windows.Controls.ContextMenu)(target));
            return;
            }
            this._contentLoaded = true;
        }
        
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "9.0.4.0")]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1800:DoNotCastUnnecessarily")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        void System.Windows.Markup.IStyleConnector.Connect(int connectionId, object target) {
            System.Windows.EventSetter eventSetter;
            switch (connectionId)
            {
            case 2:
            eventSetter = new System.Windows.EventSetter();
            eventSetter.Event = System.Windows.Controls.TreeViewItem.ExpandedEvent;
            
            #line 28 "..\..\..\..\..\..\UI\Panels\PinnedPanel\PinnedPanel.xaml"
            eventSetter.Handler = new System.Windows.RoutedEventHandler(this.TreeViewItem_Expanded);
            
            #line default
            #line hidden
            ((System.Windows.Style)(target)).Setters.Add(eventSetter);
            eventSetter = new System.Windows.EventSetter();
            eventSetter.Event = System.Windows.Controls.TreeViewItem.CollapsedEvent;
            
            #line 29 "..\..\..\..\..\..\UI\Panels\PinnedPanel\PinnedPanel.xaml"
            eventSetter.Handler = new System.Windows.RoutedEventHandler(this.TreeViewItem_Collapsed);
            
            #line default
            #line hidden
            ((System.Windows.Style)(target)).Setters.Add(eventSetter);
            eventSetter = new System.Windows.EventSetter();
            eventSetter.Event = System.Windows.Controls.TreeViewItem.SelectedEvent;
            
            #line 30 "..\..\..\..\..\..\UI\Panels\PinnedPanel\PinnedPanel.xaml"
            eventSetter.Handler = new System.Windows.RoutedEventHandler(this.TreeViewItem_Selected);
            
            #line default
            #line hidden
            ((System.Windows.Style)(target)).Setters.Add(eventSetter);
            break;
            }
        }
    }
}

