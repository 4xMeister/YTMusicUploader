﻿using System;
using System.IO;
using System.Net;
using YTMusicUploader.Business;

namespace YTMusicUploader.Providers
{
    /// <summary>
    /// YouTube Music API Request Methods
    /// 
    /// Thanks to: sigma67: 
    ///     https://ytmusicapi.readthedocs.io/en/latest/ 
    ///     https://github.com/sigma67/ytmusicapi
    /// </summary>
    public partial class Requests
    {
        /// <summary>
        /// HttpWebRequest POST request to send to YouTube to upload a music file.
        /// </summary>
        /// <param name="mainForm">Instance of the main form to utilise the public methods of and update status'</param>
        /// <param name="cookieValue">Cookie from a previous YouTube Music sign in via this application (stored in the database)</param>
        /// <param name="filePath">Full path to file we're uploading</param>
        /// <param name="maxUploadSpeed">Throttle database bandwidth speed (bytes per second)</param>
        /// <param name="error">OUTPUT error string</param>
        /// <returns>True if the upload is successful, false otherwise</returns>
        public static bool UploadTrack(
            MainForm mainForm,
            string cookieValue,
            string filePath,
            int maxUploadSpeed,
            out string error)
        {
            error = null;

            try
            {
                if (!File.Exists(filePath))
                {
                    error = "File no longer exists. Will refresh on next scan.";
                    return false;
                }

                var startUploadRequest = (HttpWebRequest)WebRequest.Create(Global.YTMusicUploadUrl);
                startUploadRequest = AddStandardHeaders(startUploadRequest, cookieValue);

                startUploadRequest.ContentType = "application/x-www-form-urlencoded;charset=utf-8";
                startUploadRequest.Headers["X-Goog-Upload-Command"] = "start";
                startUploadRequest.Headers["X-Goog-Upload-Header-Content-Length"] = new FileInfo(filePath).Length.ToString();
                startUploadRequest.Headers["X-Goog-Upload-Protocol"] = "resumable";

                byte[] postBytes = GetPostBytes("filename=" + Path.GetFileName(filePath));
                startUploadRequest.ContentLength = postBytes.Length;

                using (var requestStream = startUploadRequest.GetRequestStream())
                {
                    requestStream.Write(postBytes, 0, postBytes.Length);
                    requestStream.Close();
                }

                postBytes = null;
                using (var initialResponse = (HttpWebResponse)startUploadRequest.GetResponse())
                {
                    string uploadUrl = initialResponse.Headers["X-Goog-Upload-URL"];
                    var uploadRequest = (HttpWebRequest)WebRequest.Create(uploadUrl);
                    uploadRequest = AddStandardHeaders(uploadRequest, cookieValue);

                    uploadRequest.ContentType = "application/x-www-form-urlencoded;charset=utf-8";
                    uploadRequest.Headers["X-Goog-Upload-Command"] = "upload, finalize";
                    uploadRequest.Headers["X-Goog-Upload-Offset"] = "0";

                    byte[] songBytes = File.ReadAllBytes(filePath);
                    uploadRequest.ContentLength = songBytes.Length;

                    using (var uploadStream = uploadRequest.GetRequestStream())
                    {
                        using (var throttledStream = new ThrottledStream(
                                                            new MemoryStream(songBytes),
                                                            mainForm,
                                                            songBytes.Length,
                                                            maxUploadSpeed == 0 || maxUploadSpeed == -1
                                                                ? int.MaxValue
                                                                : maxUploadSpeed))
                        {
                            throttledStream.CopyTo(uploadStream);
                            uploadStream.Close();
                        }
                    }

                    songBytes = null;
                    using (var responseUploaded = (HttpWebResponse)uploadRequest.GetResponse())
                    {
                        if (responseUploaded.StatusCode == HttpStatusCode.OK)
                            return true;
                        else
                        {
                            if (responseUploaded.StatusCode != HttpStatusCode.Conflict) // Already uploaded
                            {
                                error = responseUploaded.StatusCode.ToString();
                                return false;
                            }

                            return true;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (!e.Message.Contains("409")) // Already uploaded
                {
                    error = e.Message;
                    return false;
                }

                return true;
            }
        }
    }
}
