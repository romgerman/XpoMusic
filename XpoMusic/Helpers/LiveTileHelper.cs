﻿using XpoMusic.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using Windows.UI.StartScreen;
using Windows.UI.Xaml;

namespace XpoMusic.Helpers
{
    public static class LiveTileHelper
    {
        public enum LiveTileDesign
        {
            Disabled = 0,
            AlbumAndArtistArt = 1,
            AlbumArtOnly = 2,
            ArtistArtOnly = 3,
        }

        internal static LiveTileDesign[] GetLiveTileDesigns()
        {
            return new[] {
                LiveTileDesign.AlbumAndArtistArt,
                LiveTileDesign.AlbumArtOnly,
                LiveTileDesign.ArtistArtOnly,
                LiveTileDesign.Disabled,
            };
        }

        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private static TileUpdater tileUpdater;
        private static int? lastTileUpdateHash = null;

        public static void InitLiveTileUpdates()
        {
            tileUpdater = TileUpdateManager.CreateTileUpdaterForApplication();

            ClearLiveTile(); // Clear live tile on startup

            PlayStatusTracker.LastPlayStatus.Updated += LastPlayStatus_Updated;
            Application.Current.Suspending += Current_Suspending;
        }

        private static void Current_Suspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            ClearLiveTile();
            logger.Info("Cleared live tile on Suspending event.");
        }

        private static void LastPlayStatus_Updated(object sender, EventArgs e)
        {
            try
            {
                //if (!PlayStatusTracker.LastPlayStatus.IsPlaying)
                //{
                //    ClearLiveTile();
                //    return;
                //}

                UpdateLiveTile();
            }
            catch (Exception ex)
            {
                logger.Warn("LastPlayStatus_Updated error: " + ex.ToString());
            }
        }

        public static async void UpdateLiveTile()
        {
            try
            {
                if (LocalConfiguration.LiveTileDesign == LiveTileDesign.Disabled)
                {
                    ClearLiveTile();
                    return;
                }

                var artistPhoto = await SongImageProvider.GetArtistArt(PlayStatusTracker.LastPlayStatus.ArtistId);
                var albumPhoto = await SongImageProvider.GetAlbumArt(PlayStatusTracker.LastPlayStatus.AlbumId);

                var template = GetLiveTileTemplate(LocalConfiguration.LiveTileDesign)
                    .Replace("{albumName}", WebUtility.HtmlEncode(PlayStatusTracker.LastPlayStatus.AlbumName))
                    .Replace("{artistName}", WebUtility.HtmlEncode(PlayStatusTracker.LastPlayStatus.ArtistName))
                    .Replace("{songName}", WebUtility.HtmlEncode(PlayStatusTracker.LastPlayStatus.SongName))
                    .Replace("{artistPhoto}", WebUtility.HtmlEncode(artistPhoto))
                    .Replace("{albumPhoto}", WebUtility.HtmlEncode(albumPhoto));

                UpdateTileWithTemplate(template);

                logger.Info("Live tile updated.");
            }
            catch (Exception ex)
            {
                logger.Warn("UpdateLiveTile failed: " + ex.ToString());
            }
        }

        private static string GetLiveTileTemplate(LiveTileDesign design)
        {
            return File.ReadAllText($"Assets/TileTemplates/LiveTile{design}.xml");
        }

        private static void UpdateTileWithTemplate(string template)
        {
            if (lastTileUpdateHash.HasValue && lastTileUpdateHash.Value.GetHashCode() == template.GetHashCode())
                return; // Do not update tile again with same data.
            lastTileUpdateHash = template.GetHashCode();

            var xml = new XmlDocument();
            xml.LoadXml(template);

            var notification = new TileNotification(xml);

            tileUpdater.Update(notification);
        }

        public static void ClearLiveTile()
        {
            lastTileUpdateHash = null;
            tileUpdater.Clear();
        }

        internal static async Task<bool> IsPinnedToStart()
        {
            AppListEntry entry = (await Package.Current.GetAppListEntriesAsync())[0];
            bool isPinned = await StartScreenManager.GetDefault().ContainsAppListEntryAsync(entry);

            return isPinned;
        }

        internal static async Task<bool> CanPinToStart()
        {
            AppListEntry entry = (await Package.Current.GetAppListEntriesAsync())[0];
            bool isSupported = StartScreenManager.GetDefault().SupportsAppListEntry(entry);

            return isSupported;
        }

        internal static async Task PinToStart()
        {
            AppListEntry entry = (await Package.Current.GetAppListEntriesAsync())[0];
            bool isPinned = await StartScreenManager.GetDefault().RequestAddAppListEntryAsync(entry);
        }
    }
}
