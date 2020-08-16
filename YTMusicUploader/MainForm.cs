﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using JBToolkit.WinForms;
using MetroFramework;
using MetroFramework.Forms;
using JBToolkit.Helpers;
using YTMusicUploader.Providers;
using YTMusicUploader.Providers.Models;
using YTMusicUploader.Providers.Repos;
using System.Threading;

namespace YTMusicUploader
{
    public partial class MainForm : OptimisedMetroForm
    {
        public SettingsRepo SettingsRepo { get; set; } = new SettingsRepo();
        public WatchFolderRepo WatchFolderRepo { get; set; } = new WatchFolderRepo();
        public MusicFileRepo MusicFileRepo { get; set; } = new MusicFileRepo();
        public Settings Settings { get; set; }
        public List<WatchFolder> WatchFolders { get; set; }


        public MainForm() : base(formResizable: false,
                                 controlTagsAsTooltips: false)
        {
            CulterHelper.GloballySetCultureToGB();
            //CheckForIllegalCrossThreadCalls = false;            

            InitializeComponent();
            SuspendDrawing(this);

            PerformPrechecks();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            OnLoad(e);
            ResumeDrawing(this);
        }

        private void PerformPrechecks()
        {
            // TODO: Try catch and handle here

            new Thread((ThreadStart)delegate
            {
                DataAccess.CheckAndCopyDatabaseFile();                
                WatchFolders = WatchFolderRepo.Load();
                Settings = SettingsRepo.Load();
                
                SetStartWithWindows(Settings.StartWithWindows);
                BindWatchFoldersList();

                if (!Requests.CheckAndCopyApiFiles())
                {
                    MetroMessageBox.Show(
                       this,
                       @"Python (minimum version: 3.5) is required for this tool to work. You can download from here: https://www.python.org/downloads/",
                       "Dependency Required",
                       MessageBoxButtons.OK,
                       MessageBoxIcon.Asterisk,
                       120);
                }
                else
                {
                    if (!string.IsNullOrEmpty(Settings.AuthenticationCookie))
                        SetConnectedToYouTubeMusic(Requests.IsAuthenticated());
                    else
                        SetConnectedToYouTubeMusic(false);
                }

            }).Start();
        }        
    }
}
