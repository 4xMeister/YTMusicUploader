﻿using Dapper;
using JBToolkit.Threads;
using JBToolkit.Windows;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using YTMusicUploader.Providers.DataModels;
using static YTMusicUploader.MainForm;

namespace YTMusicUploader.Business
{
    /// <summary>
    /// Responsive for scanning library music files and adding and managing discovered files to the database.
    /// </summary>
    public class FileScanner
    {
        private MainForm MainForm { get; set; }
        public List<FileData> NewFiles { get; set; } = new List<FileData>();
        public HashSet<string> NewFilesHash { get; set; } = new HashSet<string>();
        public HashSet<string> DiscoveredFilesHash { get; set; } = new HashSet<string>();
        public List<MusicFile> MusicFilesToDelete { get; set; } = new List<MusicFile>();
        public List<MusicFile> CurrentMusicFiles { get; set; }
        public HashSet<string> CurrentMusicFilesHash { get; set; } = new HashSet<string>();
        public bool Stopped { get; set; } = false;

        public FileScanner(MainForm mainForm)
        {
            MainForm = mainForm;
        }

        /// <summary>
        /// Executes the scan
        /// </summary>
        public void Process()
        {
            Stopped = false;
            SetStatus();

            if (MainForm.WatchFolders.Count == 0)
            {
                MainForm.MusicFileRepo.DeleteAll().Wait();
                MainForm.SetDiscoveredFilesLabel("0");
                MainForm.SetIssuesLabel("0");
                MainForm.SetUploadedLabel("0");
            }

            CurrentMusicFiles = MainForm.MusicFileRepo.LoadAll().Result;
            foreach (var musicFile in CurrentMusicFiles)
                CurrentMusicFilesHash.Add(musicFile.Path);

            //
            // Get files to add - Cross reference with the DB
            //
            foreach (var watchFolder in MainForm.WatchFolders)
            {
                if (MainFormAborting())
                    return;

                if (Directory.Exists(watchFolder.Path))
                {
                    foreach (var file in FastDirectoryEnumerator.EnumerateFiles(watchFolder.Path,
                                                                                "*.*",
                                                                                SearchOption.AllDirectories))
                    {
                        try
                        {
                            if (MainFormAborting())
                                return;

                            if (!CurrentMusicFilesHash.Contains(file.Path))
                            {
                                if (Path.GetExtension(file.Name.ToLower()).In(Global.SupportedFiles))
                                {
                                    NewFiles.Add(file);
                                    NewFilesHash.Add(file.Path);
                                }
                            }

                            if (MainFormAborting())
                                return;

                            if (Path.GetExtension(file.Name.ToLower()).In(Global.SupportedFiles))
                                DiscoveredFilesHash.Add(file.Path);
                        }
                        catch (Exception e)
                        {
                            Logger.Log(
                                e,
                                "FileScanner.Process - Error reading file (possibly removed): " +
                                file.Path,
                                Log.LogTypeEnum.Error,
                                false);
                        }
                    }
                }
                else
                {
                    if (MainFormAborting())
                        return;

                    SetStatus("Error: Watch folder directory does not exists: " + watchFolder.Path.EllipsisPath(100), "Directory ");
                    ThreadHelper.SafeSleep(5000);
                }
            }

            //
            // Get files to delete - Cross reference with the DB
            //
            foreach (var musicFile in CurrentMusicFiles)
            {
                if (MainFormAborting())
                    return;

                if (!DiscoveredFilesHash.Contains(musicFile.Path))
                    MusicFilesToDelete.Add(musicFile);
            }

            using (var conn = new SQLiteConnection("Data Source=" + Global.DbLocation + ";cache=shared"))
            {
                SetStatus();
                conn.Open();

                try
                {
                    int count = 0;
                    foreach (var file in NewFiles)
                    {
                        if (MainFormAborting(conn))
                            return;

                        count++;
                        if (count % 100 == 0)
                            MainForm.SetDiscoveredFilesLabel(count.ToString());

                        SetStatus();
                        AddToDB(conn, new MusicFile(file.Path));
                    }

                    if (MainFormAborting(conn))
                        return;

                    count = 0;
                    foreach (var musicFile in MusicFilesToDelete)
                    {
                        if (MainFormAborting(conn))
                            return;

                        count++;
                        RemoveFromDB(conn, musicFile.Path);
                    };

                    MainForm.SetDiscoveredFilesLabel(MainForm.MusicFileRepo.CountAll().Result.ToString());
                }
                catch { }

                conn.Close();
            }

            SetStatus(MainForm.ConnectedToYTMusic ? "Ready" : "Waiting for YouTube Music connection", "Waiting for YouTube Music connection");
            Stopped = true;
        }

        /// <summary>
        /// Updates the 'Discovered Files' count on the main form. Ideally used when updating the form 
        /// while an upload process is taking place
        /// </summary>
        public void RecountLibraryFiles()
        {
            try
            {
                int count = 0;
                foreach (var watchFolder in MainForm.WatchFolders)
                {
                    foreach (var file in FastDirectoryEnumerator.EnumerateFiles(watchFolder.Path,
                                                                                "*.*",
                                                                                SearchOption.AllDirectories))
                    {
                        if (Path.GetExtension(file.Name.ToLower()).In(Global.SupportedFiles))
                            count++;
                    }
                }

                MainForm.SetDiscoveredFilesLabel(count.ToString());
            }
            catch (Exception e)
            {
                Logger.Log(e, "RecountLibraryFiles", Log.LogTypeEnum.Warning);
            }
        }

        private void SetStatus(string statusText = null, string systemTrayIconText = null)
        {
            if (string.IsNullOrEmpty(statusText))
            {
                if (MainForm.ManagingYTMusicStatus != ManagingYTMusicStatusEnum.Showing)
                    MainForm.SetUploadingMessage("Idle", null, null, true);

                if (MainForm.WatchFolders.Count > 0)
                    MainForm.SetStatusMessage("Looking for new files...", "Looking for new files");
                else
                    MainForm.SetStatusMessage("Updating database...", "Updating database");
            }
            else
                MainForm.SetStatusMessage(statusText);

            if (!string.IsNullOrEmpty(systemTrayIconText))
                MainForm.SetSystemTrayIconText(systemTrayIconText);
        }

        private void AddToDB(SQLiteConnection conn, MusicFile musicFile)
        {
            // Not using the standard Repos namespace so we don't have to keep creating and
            // opening new connections

            try
            {
                int? id = conn.ExecuteScalar<int?>(
                        @"SELECT Id  
                          FROM MusicFiles
                          WHERE Path = @Path
                          LIMIT 1",
                        new { musicFile.Path });

                if (id == null || id == 0)
                {
                    conn.Execute(
                              @"INSERT 
                                    INTO MusicFiles (
			                                Path, 
                                            Hash,
			                                LastUpload, 
			                                Error,
			                                ErrorReason
                                            )
                                    SELECT @Path,
                                           @Hash,
	                                       @LastUpload,
	                                       @Error,
	                                       @ErrorReason",
                              musicFile);
                }
                else
                {
                    conn.Execute(
                       @"UPDATE MusicFiles
                            SET Removed = 0 
                         WHERE Id = @Id",
                       new { id });
                }
            }
            catch (Exception e)
            {
                Console.Out.WriteLine(e.Message);
            }
        }

        private void RemoveFromDB(SQLiteConnection conn, string path)
        {
            // Not using the standard Repos namespace so we don't have to keep creating and
            // opening new connections

            try
            {
                conn.Execute(
                          @"UPDATE MusicFiles
                               SET Removed = 1
                            WHERE Path = @Path",
                          new { path });
            }
            catch { }
        }

        private bool MainFormAborting(SQLiteConnection conn = null)
        {
            if (MainForm.Aborting)
            {
                if (conn != null)
                {
                    try
                    {
                        conn.Close();
                    }
                    catch { }
                }

                Stopped = true;
                MainForm.SetUploadingMessage("Restarting", "Restarting", null, true);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resets the properties, such as discovered file, current music files and hashes
        /// </summary>
        public void Reset()
        {
            NewFiles = new List<FileData>();
            NewFilesHash = new HashSet<string>();
            DiscoveredFilesHash = new HashSet<string>();
            MusicFilesToDelete = new List<MusicFile>();
            CurrentMusicFilesHash = new HashSet<string>();
        }
    }
}
