﻿using JBToolkit.FuzzyLogic;
using JBToolkit.StreamHelpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using YTMusicUploader.Business;
using YTMusicUploader.Providers.DataModels;
using YTMusicUploader.Providers.RequestModels;

namespace YTMusicUploader.Providers
{
    /// <summary>
    /// HttpWebRequest POST request to send to YouTube to determine if the song about to be uploaded already exists
    /// It may request a little attention depending on the results, seen as though we're trying to match a song based
    /// on artist, album and track name, which may be slightly different in the uploading music file's meta tags. Currently
    /// it uses a Levenshtein distance fuzzy logic implementation to check similarity between strings and is considered
    /// a match if it's above 0.75.
    /// 
    /// Thanks to: sigma67: 
    ///     https://ytmusicapi.readthedocs.io/en/latest/ 
    ///     https://github.com/sigma67/ytmusicapi
    /// </summary>
    public partial class Requests
    {
        public enum UploadCheckResult
        {
            NotPresent,
            Present_FromCache,
            Present_NewRequest
        }

        public static Thread UploadCheckPreloaderThread { get; set; } = null;
        public static Thread UploadCheckPreloaderSleepThread { get; set; } = null;

        /// <summary>
        /// Object to store pre fetched already uploaded check results
        /// </summary>
        public static class UploadCheckCheckCache
        {
            public static List<MusicFileCacheObject> CachedObjects { get; set; } = new List<MusicFileCacheObject>();
            public static bool Sleep { get; set; } = false;
            public static bool CleanUp { get; set; } = false;
            public static HashSet<string> CachedObjectHash { get; set; } = new HashSet<string>();

            public class MusicFileCacheObject
            {
                public string MusicFilePath { get; set; }
                public string MbId { get; set; }
                public bool Result { get; set; }
            }
        }

        /// <summary>
        /// Starts a new thread which loops through MusicFiles to check if they've already been uploaded to YouTube Music ahead of
        /// time and stores them in a cache to speed up the upload checker.
        /// </summary>
        /// <param name="musicFilesList">Path to music file to be uploaded</param>
        /// <param name="cookieValue">Cookie from a previous YouTube Music sign in via this application (stored in the database)</param>
        /// <param name="musicDataFetcher">You can pass an existing MusicDataFetcher object, or one will be created if left blank</param>
        public static void StartPrefetchingUploadedFilesCheck(
            List<MusicFile> musicFilesList,
            string cookieValue,
            MusicDataFetcher musicDataFetcher = null)
        {
            UploadCheckCheckCache.CleanUp = false;
            UploadCheckCheckCache.CachedObjects.Clear();
            UploadCheckCheckCache.CachedObjectHash.Clear();

            try
            {
                if (UploadCheckPreloaderThread != null)
                    UploadCheckPreloaderThread.Abort();
            }
            catch { }

            UploadCheckPreloaderThread = new Thread((ThreadStart)delegate
            {
                while (!UploadCheckCheckCache.CleanUp)
                {
                    UploadCheckCheckCache.Sleep = true;
                    Thread.Sleep(100);
                    UploadCheckCheckCache.Sleep = false;
                    Thread.Sleep(1000);
                }

                UploadCheckCheckCache.CachedObjects.Clear();
                UploadCheckCheckCache.CachedObjectHash.Clear();
            });
            UploadCheckPreloaderThread.Start();

            UploadCheckPreloaderThread = new Thread((ThreadStart)delegate
            {
                musicFilesList.AsParallel().ForAllInApproximateOrder(cacheObject =>
                {
                    if (!UploadCheckCheckCache.CleanUp)
                    {
                        while (UploadCheckCheckCache.Sleep)
                            Thread.Sleep(2);

                        UploadCheckCheckCache.CachedObjects.Add(new UploadCheckCheckCache.MusicFileCacheObject
                        {
                            MusicFilePath = cacheObject.Path,
                            MbId = !string.IsNullOrEmpty(cacheObject.MbId)
                                        ? cacheObject.MbId
                                        : musicDataFetcher.GetTrackMbId(cacheObject.Path).Result,
                            Result = IsSongUploaded(cacheObject.Path, cookieValue, musicDataFetcher).Result == UploadCheckResult.Present_NewRequest
                        });
                        UploadCheckCheckCache.CachedObjectHash.Add(cacheObject.Path);
                    }
                    else
                    {
                        try
                        {
                            return;
                        }
                        catch { }
                    }
                }, 4 /* Max degrees of parallelism */ );
            });

            UploadCheckPreloaderThread.Start();
        }

        /// <summary>
        /// HttpWebRequest POST request to send to YouTube to determine if the song about to be uploaded already exists
        /// It may request a little attention depending on the results, seen as though we're trying to match a song based
        /// on artist, album and track name, which may be slightly different in the uploading music file's meta tags. Currently
        /// it uses a Levenshtein distance fuzzy logic implementation to check similarity between strings and is considered
        /// a match if it's above 0.75.
        /// </summary>
        /// <param name="musicFilePath">Path to music file to be uploaded</param>
        /// <param name="cookieValue">Cookie from a previous YouTube Music sign in via this application (stored in the database)</param>
        /// <param name="musicDataFetcher">You can pass an existing MusicDataFetcher object, or one will be created if left blank</param>
        /// <returns>True if song is found, false otherwise</returns>
        public static async Task<UploadCheckResult> IsSongUploaded(
            string musicFilePath,
            string cookieValue,
            MusicDataFetcher musicDataFetcher = null)
        {
            if (UploadCheckCheckCache.CachedObjectHash.Contains(musicFilePath))
            {
                return await Task.FromResult(
                                    UploadCheckCheckCache.CachedObjects
                                                         .Where(m => m.MusicFilePath == musicFilePath)
                                                         .FirstOrDefault()
                                                         .Result
                                                                ? UploadCheckResult.Present_FromCache
                                                                : UploadCheckResult.NotPresent);
            }
            else
            {
                if (musicDataFetcher == null)
                    musicDataFetcher = new MusicDataFetcher();

                var musicFileMetaData = musicDataFetcher.GetMusicFileMetaData(musicFilePath);
                if (musicFileMetaData == null ||
                    musicFileMetaData.Artist == null ||
                    musicFileMetaData.Album == null ||
                    musicFileMetaData.Track == null)
                {
                    return await Task.FromResult(UploadCheckResult.NotPresent);
                }

                string artist = musicFileMetaData.Artist;
                string album = musicFileMetaData.Album;
                string track = musicFileMetaData.Track;

                try
                {
                    return await IsSongUploadedMulitpleAlbumNameVariations(artist, album, track, cookieValue)
                                    ? UploadCheckResult.Present_NewRequest
                                    : UploadCheckResult.NotPresent;
                }
                catch
                {
                    return await Task.FromResult(UploadCheckResult.NotPresent);
                }
            }
        }

        /// <summary>
        /// HttpWebRequest POST request to send to YouTube to determine if the song about to be uploaded already exists
        /// It may request a little attention depending on the results, seen as though we're trying to match a song based
        /// on artist, album and track name, which may be slightly different in the uploading music file's meta tags. Currently
        /// it uses a Levenshtein distance fuzzy logic implementation to check similarity between strings and is considered
        /// a match if it's above 0.75.
        /// </summary>
        /// <param name="artist">Artist name from music file meta tag</param>
        /// <param name="album">Album name from music file meta tag</param>
        /// <param name="track">Track or song name from music file meta tag</param>
        /// <param name="cookieValue">Cookie from a previous YouTube Music sign in via this application (stored in the database)</param>
        /// <returns>True if song is found, false otherwise</returns>
        public static async Task<bool> IsSongUploadedMulitpleAlbumNameVariations(
            string artist,
            string album,
            string track,
            string cookieValue)
        {
            bool result = await IsSongUploaded(artist, album, track, cookieValue);

            if (result)
                return result;

            album = album.Substring(album.IndexOf("-") + 1, album.Length - 1 - album.IndexOf("-")).Trim();
            result = await IsSongUploaded(artist, album, track, cookieValue);

            if (result)
                return result;

            album = Regex.Replace(album, @"(?<=\[)(.*?)(?=\])", "").Replace("[]", "").Replace("  ", " ").Trim();
            result = await IsSongUploaded(artist, album, track, cookieValue);

            if (result)
                return result;

            try
            {
                if (!string.IsNullOrEmpty(track) && track.Substring(0, 2).IsNumeric())
                {
                    track = track.Substring(2).Trim();
                    if (track.StartsWith("_") || track.StartsWith("-") || track.StartsWith("."))
                        track = track.Substring(1).Trim();
                }
            }
            catch { }

            result = await IsSongUploaded(artist, album, track, cookieValue);
            if (result)
                return result;

            track = Regex.Replace(track, @"(\d)+-(\d)+", "").Trim();
            return await IsSongUploaded(artist, album, track, cookieValue);
        }

        /// <summary>
        /// HttpWebRequest POST request to send to YouTube to determine if the song about to be uploaded already exists
        /// It may request a little attention depending on the results, seen as though we're trying to match a song based
        /// on artist, album and track name, which may be slightly different in the uploading music file's meta tags. Currently
        /// it uses a Levenshtein distance fuzzy logic implementation to check similarity between strings and is considered
        /// a match if it's above 0.75.
        /// </summary>
        /// <param name="artist">Artist name from music file meta tag</param>
        /// <param name="album">Album name from music file meta tag</param>
        /// <param name="track">Track or song name from music file meta tag</param>
        /// <param name="cookieValue">Cookie from a previous YouTube Music sign in via this application (stored in the database)</param>
        /// <returns>True if song is found, false otherwise</returns>
        public static async Task<bool> IsSongUploaded(
            string artist,
            string album,
            string track,
            string cookieValue)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(Global.YouTubeBaseUrl + "search" + Global.YouTubeMusicParams);
                request = AddStandardHeaders(request, cookieValue);

                request.ContentType = "application/json; charset=UTF-8";
                request.Headers["X-Goog-AuthUser"] = "0";
                request.Headers["x-origin"] = "https://music.youtube.com";
                request.Headers["X-Goog-Visitor-Id"] = Global.GoogleVisitorId;
                request.Headers["Authorization"] = GetAuthorisation(GetSAPISIDFromCookie(cookieValue));

                var context = JsonConvert.DeserializeObject<SearchContext>(
                                                SafeFileStream.ReadAllText(
                                                                    Path.Combine(
                                                                            Global.WorkingDirectory,
                                                                            @"AppData\search_uploads_context.json")));

                context.query = string.Format("{0} {1} {2}", artist, album, track);
                byte[] postBytes = GetPostBytes(JsonConvert.SerializeObject(context));
                request.ContentLength = postBytes.Length;

                using (var requestStream = request.GetRequestStream())
                {
                    requestStream.Write(postBytes, 0, postBytes.Length);
                    requestStream.Close();
                }

                postBytes = null;
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    string result;
                    using (var brotli = new Brotli.BrotliStream(
                                                        response.GetResponseStream(),
                                                        System.IO.Compression.CompressionMode.Decompress,
                                                        true))
                    {
                        var streamReader = new StreamReader(brotli);
                        result = streamReader.ReadToEnd();
                    }

                    JObject jo = JObject.Parse(result);
                    List<JToken> runs = jo.Descendants()
                                          .Where(t => t.Type == JTokenType.Property && ((JProperty)t).Name == "runs")
                                          .Select(p => ((JProperty)p).Value).ToList();

                    float matchSuccess = Global.YouTubeUploadedSimilarityPercentageForMatch;
                    float artistSimilarity = 0.0f;
                    float albumSimilartity = 0.0f;
                    float trackSimilarity = 0.0f;

                    foreach (JToken run in runs)
                    {
                        if (run.ToString().Contains("text"))
                        {
                            var runArray = run.ToObject<SearchResult.Run[]>();
                            if (runArray.Length > 0)
                            {
                                if (runArray[0].text.ToLower().Contains("no results found"))
                                    return await Task.FromResult(true);
                                else
                                {
                                    Parallel.ForEach(runArray, (runElement) =>
                                    {
                                        if (runElement.text == null)
                                            runElement.text = string.Empty;

                                        float _artistSimilarity = 0.0f;
                                        float _albumSimilartity = 0.0f;
                                        float _trackSimilarity = 0.0f;

                                        Parallel.For(0, 3, (i, state) =>
                                        {
                                            switch (i)
                                            {
                                                case 0:
                                                    if (artistSimilarity < matchSuccess)
                                                        _artistSimilarity = Levenshtein.Similarity(runElement.text, artist);
                                                    break;
                                                case 1:
                                                    if (_albumSimilartity < matchSuccess)
                                                        _albumSimilartity = Levenshtein.Similarity(runElement.text, album);
                                                    break;
                                                case 2:
                                                    if (_trackSimilarity < matchSuccess)
                                                        _trackSimilarity = Levenshtein.Similarity(runElement.text, track);
                                                    break;
                                                default:
                                                    break;
                                            }
                                        });

                                        if (artistSimilarity < _artistSimilarity)
                                            artistSimilarity = _artistSimilarity;

                                        if (albumSimilartity < _albumSimilartity)
                                            albumSimilartity = _albumSimilartity;

                                        if (trackSimilarity < _trackSimilarity)
                                            trackSimilarity = _trackSimilarity;
                                    });
                                }
                            }
                        }
                    }

                    if (artistSimilarity >= matchSuccess &&
                        albumSimilartity >= matchSuccess &&
                        trackSimilarity >= matchSuccess)
                    {
                        return await Task.FromResult(true);
                    }

                    return await Task.FromResult(false);
                }
            }
            catch (Exception)
            {
                return await Task.FromResult(true);
            }
        }
    }
}
