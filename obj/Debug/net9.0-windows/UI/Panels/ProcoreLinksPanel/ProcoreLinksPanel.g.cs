﻿#pragma checksum "..\..\..\..\..\..\UI\Panels\ProcoreLinksPanel\ProcoreLinksPanel.xaml" "{ff1816ec-aa5e-4d10-87f7-6f4963833460}" "B74D1E9CB34A32B88415C9EDA8084301338650A8"
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using ExplorerPro.UI.Panels.ProcoreLinksPanel;
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


namespace ExplorerPro.UI.Panels.ProcoreLinksPanel {
    
    
    /// <summary>
    /// ProcoreLinksPanel
    /// </summary>
    public partial class ProcoreLinksPanel : System.Windows.Controls.UserControl, System.Windows.Markup.IComponentConnector {
        
        
        #line 17 "..\..\..\..\..\..\UI\Panels\ProcoreLinksPanel\ProcoreLinksPanel.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.TextBox SearchBar;
        
        #line default
        #line hidden
        
        
        #line 44 "..\..\..\..\..\..\UI\Panels\ProcoreLinksPanel\ProcoreLinksPanel.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Button AddProjectButton;
        
        #line default
        #line hidden
        
        
        #line 52 "..\..\..\..\..\..\UI\Panels\ProcoreLinksPanel\ProcoreLinksPanel.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Button AddLinkButton;
        
        #line default
        #line hidden
        
        
        #line 60 "..\..\..\..\..\..\UI\Panels\ProcoreLinksPanel\ProcoreLinksPanel.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Button RemoveButton;
        
        #line default
        #line hidden
        
        
        #line 71 "..\..\..\..\..\..\UI\Panels\ProcoreLinksPanel\ProcoreLinksPanel.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.TreeView LinksTreeView;
        
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
            System.Uri resourceLocater = new System.Uri("/ExplorerPro;component/ui/panels/procorelinkspanel/procorelinkspanel.xaml", System.UriKind.Relative);
            
            #line 1 "..\..\..\..\..\..\UI\Panels\ProcoreLinksPanel\ProcoreLinksPanel.xaml"
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
            this.SearchBar = ((System.Windows.Controls.TextBox)(target));
            
            #line 20 "..\..\..\..\..\..\UI\Panels\ProcoreLinksPanel\ProcoreLinksPanel.xaml"
            this.SearchBar.TextChanged += new System.Windows.Controls.TextChangedEventHandler(this.SearchBar_TextChanged);
            
            #line default
            #line hidden
            return;
            case 2:
            this.AddProjectButton = ((System.Windows.Controls.Button)(target));
            
            #line 47 "..\..\..\..\..\..\UI\Panels\ProcoreLinksPanel\ProcoreLinksPanel.xaml"
            this.AddProjectButton.Click += new System.Windows.RoutedEventHandler(this.AddProjectButton_Click);
            
            #line default
            #line hidden
            return;
            case 3:
            this.AddLinkButton = ((System.Windows.Controls.Button)(target));
            
            #line 55 "..\..\..\..\..\..\UI\Panels\ProcoreLinksPanel\ProcoreLinksPanel.xaml"
            this.AddLinkButton.Click += new System.Windows.RoutedEventHandler(this.AddLinkButton_Click);
            
            #line default
            #line hidden
            return;
            case 4:
            this.RemoveButton = ((System.Windows.Controls.Button)(target));
            
            #line 63 "..\..\..\..\..\..\UI\Panels\ProcoreLinksPanel\ProcoreLinksPanel.xaml"
            this.RemoveButton.Click += new System.Windows.RoutedEventHandler(this.RemoveButton_Click);
            
            #line default
            #line hidden
            return;
            case 5:
            this.LinksTreeView = ((System.Windows.Controls.TreeView)(target));
            
            #line 73 "..\..\..\..\..\..\UI\Panels\ProcoreLinksPanel\ProcoreLinksPanel.xaml"
            this.LinksTreeView.MouseDoubleClick += new System.Windows.Input.MouseButtonEventHandler(this.LinksTreeView_MouseDoubleClick);
            
            #line default
            #line hidden
            
            #line 74 "..\..\..\..\..\..\UI\Panels\ProcoreLinksPanel\ProcoreLinksPanel.xaml"
            this.LinksTreeView.ContextMenuOpening += new System.Windows.Controls.ContextMenuEventHandler(this.LinksTreeView_ContextMenuOpening);
            
            #line default
            #line hidden
            return;
            case 6:
            
            #line 78 "..\..\..\..\..\..\UI\Panels\ProcoreLinksPanel\ProcoreLinksPanel.xaml"
            ((System.Windows.Controls.ContextMenu)(target)).Opened += new System.Windows.RoutedEventHandler(this.ContextMenu_Opened);
            
            #line default
            #line hidden
            return;
            }
            this._contentLoaded = true;
        }
    }
}

