﻿using System;
using System.Windows;

namespace BlockStation
{
    [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "4.0.0.0")]
    public partial class App : System.Windows.Application
    {
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public void InitializeComponent()
        {
            this.StartupUri = new System.Uri("gui\\ServerSelector.xaml", System.UriKind.Relative);

        }
    
        //-----------------------------------------------------------------------------------------------
        public static string AppVersion = "0.6";
        public static string AppVersionText = "Alpha";
        public static string BuildVersion = "20151011";
        //-----------------------------------------------------------------------------------------------

        [System.STAThreadAttribute()]
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public static void Main()
        {
            try
            {
                BlockStation.App app = new BlockStation.App();
                app.InitializeComponent();
                app.Run();
            }
            catch(Exception e)
            {
                MessageBox.Show("Ein schwerwiegender Fehler ist aufgetreten.\n\nDetails:\n" + e);
            }

        }
    }
}