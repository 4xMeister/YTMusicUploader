﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace YTMusicUploader.MusicBrainz.API.Resources {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "16.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Constants {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Constants() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("YTMusicUploader.Providers.MusicBrainz.Resources.Constants", typeof(Constants).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to -area-beginarea-endarea-arid-artist-artistaccent-alias-begin-comment-country-end-ended-gender-ipi-sortname-tag-type-.
        /// </summary>
        internal static string ArtistQueryParams {
            get {
                return ResourceManager.GetString("ArtistQueryParams", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to +official+promotion+bootleg+pseudo-release+.
        /// </summary>
        internal static string BrowseStatus {
            get {
                return ResourceManager.GetString("BrowseStatus", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to +nat+album+single+ep+compilation+soundtrack+spokenword+interview+audiobook+live+remix+other+.
        /// </summary>
        internal static string BrowseType {
            get {
                return ResourceManager.GetString("BrowseType", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to -arid-artist-artistname-creditname-comment-country-date-dur-format-isrc-number-position-primarytype-puid-qdur-recording-recordingaccent-reid-release-rgid--rid-secondarytype-status-tid-tnum-tracks-tracksrelease-tag-type-video-.
        /// </summary>
        internal static string RecordingQueryParams {
            get {
                return ResourceManager.GetString("RecordingQueryParams", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to -arid-artist-artistname-comment-creditname-primarytype-rgid-releasegroup-releasegroupaccent-releases-release-reid-secondarytype-status-tag-type-.
        /// </summary>
        internal static string ReleaseGroupQueryParams {
            get {
                return ResourceManager.GetString("ReleaseGroupQueryParams", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to -arid-artist-artistname-asin-barcode-catno-comment-country-creditname-date-discids-discidsmedium-format-laid-label-lang-mediums-primarytype-puid-quality-reid-release-releaseaccent-rgid-script-secondarytype-status-tag-tracks-tracksmedium-type-.
        /// </summary>
        internal static string ReleaseQueryParams {
            get {
                return ResourceManager.GetString("ReleaseQueryParams", resourceCulture);
            }
        }
    }
}
