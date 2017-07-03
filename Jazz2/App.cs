﻿using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Duality;
using Duality.Backend;
using Jazz2.Game;

namespace Jazz2
{
    public static class App
    {
        private static Controller controller;

        public static string AssemblyTitle
        {
            get
            {
                object[] attributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
                if (attributes.Length > 0) {
                    AssemblyTitleAttribute titleAttribute = (AssemblyTitleAttribute)attributes[0];
                    if (!string.IsNullOrEmpty(titleAttribute.Title)) {
                        return titleAttribute.Title;
                    }
                }
                return Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location);
            }
        }

        public static string AssemblyVersion
        {
            get
            {
                Version v = Assembly.GetEntryAssembly().GetName().Version;
                return v.Major.ToString(CultureInfo.InvariantCulture) + "." + v.Minor.ToString(CultureInfo.InvariantCulture) + (v.Build != 0 ? "." + v.Build.ToString(CultureInfo.InvariantCulture) : "");
            }
        }

        [STAThread]
        private static void Main(string[] args)
        {
            // 3 Mar, 2017 - https://github.com/AdamsLair/duality/commit/7f4d38c982956d53ee3c72512fc20fd8d19278fe

            DualityApp.Init(DualityApp.ExecutionContext.Game, new DefaultAssemblyLoader(), args);

            using (INativeWindow window = DualityApp.OpenWindow(new WindowOptions {
                Title = AssemblyTitle,
                RefreshMode = (args.Contains("/mv") ? RefreshMode.ManualSync : RefreshMode.VSync),
                Size = LevelRenderSetup.TargetSize
            })) {
                controller = new Controller(window);
                controller.ShowMainMenu();
                window.Run();
            }

            DualityApp.Terminate();
        }
    }
}