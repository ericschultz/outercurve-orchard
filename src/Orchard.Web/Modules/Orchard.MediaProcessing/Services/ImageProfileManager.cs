﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using Orchard.ContentManagement;
using Orchard.FileSystems.Media;
using Orchard.Forms.Services;
using Orchard.Logging;
using Orchard.MediaProcessing.Descriptors.Filter;
using Orchard.MediaProcessing.Media;
using Orchard.MediaProcessing.Models;
using Orchard.Tokens;
using Orchard.Utility.Extensions;

namespace Orchard.MediaProcessing.Services {
    public class ImageProfileManager : IImageProfileManager {
        private readonly IStorageProvider _storageProvider;
        private readonly IImageProcessingFileNameProvider _fileNameProvider;
        private readonly IImageProfileService _profileService;
        private readonly IImageProcessingManager _processingManager;
        private readonly IOrchardServices _services;
        private readonly ITokenizer _tokenizer;

        public ImageProfileManager(
            IStorageProvider storageProvider,
            IImageProcessingFileNameProvider fileNameProvider,
            IImageProfileService profileService,
            IImageProcessingManager processingManager,
            IOrchardServices services,
            ITokenizer tokenizer) {
            _storageProvider = storageProvider;
            _fileNameProvider = fileNameProvider;
            _profileService = profileService;
            _processingManager = processingManager;
            _services = services;
            _tokenizer = tokenizer;

            Logger = NullLogger.Instance;
        }

        public ILogger Logger { get; set; }

        public string GetImageProfileUrl(string path, string profileName) {
            return GetImageProfileUrl(path, profileName, null, null);
        }

        public string GetImageProfileUrl(string path, string profileName, ContentItem contentItem) {
            return GetImageProfileUrl(path, profileName, null, contentItem);
        }

        public string GetImageProfileUrl(string path, string profileName, FilterRecord customFilter) {
            return GetImageProfileUrl(path, profileName, customFilter, null);
        }

        public string GetImageProfileUrl(string path, string profileName, FilterRecord customFilter, ContentItem contentItem) {

            // try to load the processed filename from cache
            var filePath = _fileNameProvider.GetFileName(profileName, path);
            bool process = false;

            // if the filename is not cached, process it
            if (string.IsNullOrEmpty(filePath)) {
                Logger.Debug("FilePath is null, processing required, profile {0} for image {1}", profileName, path);

                process = true;
            }

                // the processd file doesn't exist anymore, process it
            else if (!_storageProvider.FileExists(filePath)) {
                Logger.Debug("Processed file no longer exists, processing required, profile {0} for image {1}", profileName, path);

                process = true;
            }

            // if the original file is more recent, process it
            else {
                DateTime pathLastUpdated;
                if (TryGetImageLastUpdated(path, out pathLastUpdated)) {
                    var filePathLastUpdated = _storageProvider.GetFile(filePath).GetLastUpdated();

                    if (pathLastUpdated > filePathLastUpdated) {
                        Logger.Debug("Original file more recent, processing required, profile {0} for image {1}", profileName, path);

                        process = true;
                    }
                }
            }

            // todo: regenerate the file if the profile is newer, by deleting the associated filename cache entries.
            if (process) {
                Logger.Debug("Processing profile {0} for image {1}", profileName, path);

                ImageProfilePart profilePart;

                if (customFilter == null) {
                    profilePart = _profileService.GetImageProfileByName(profileName);
                    if (profilePart == null)
                        return String.Empty;
                }
                else {
                    profilePart = _services.ContentManager.New<ImageProfilePart>("ImageProfile");
                    profilePart.Name = profileName;
                    profilePart.Filters.Add(customFilter);
                }

                using (var image = GetImage(path)) {

                    var filterContext = new FilterContext { Media = image, FilePath = _storageProvider.Combine("_Profiles", _storageProvider.Combine(profileName, CreateDefaultFileName(path))) };

                    var tokens = new Dictionary<string, object>();
                    // if a content item is provided, use it while tokenizing
                    if (contentItem != null) {
                        tokens.Add("Content", contentItem);
                    }

                    foreach (var filter in profilePart.Filters.OrderBy(f => f.Position)) {
                        var descriptor = _processingManager.DescribeFilters().SelectMany(x => x.Descriptors).FirstOrDefault(x => x.Category == filter.Category && x.Type == filter.Type);
                        if (descriptor == null)
                            continue;

                        var tokenized = _tokenizer.Replace(filter.State, tokens);
                        filterContext.State = FormParametersHelper.ToDynamic(tokenized);
                        descriptor.Filter(filterContext);
                    }

                    _fileNameProvider.UpdateFileName(profileName, path, filterContext.FilePath);

                    if (!filterContext.Saved) {
                        _storageProvider.TryCreateFolder(_storageProvider.Combine("_Profiles", profilePart.Name));
                        var newFile = _storageProvider.OpenOrCreate(filterContext.FilePath);
                        using (var imageStream = newFile.OpenWrite()) {
                            using (var sw = new BinaryWriter(imageStream)) {
                                if (filterContext.Media.CanSeek) {
                                    filterContext.Media.Seek(0, SeekOrigin.Begin);
                                }
                                using (var sr = new BinaryReader(filterContext.Media)) {
                                    int count;
                                    var buffer = new byte[8192];
                                    while ((count = sr.Read(buffer, 0, buffer.Length)) != 0) {
                                        sw.Write(buffer, 0, count);
                                    }
                                }
                            }
                        }
                    }

                    filterContext.Media.Dispose();
                    filePath = filterContext.FilePath;
                }
            }

            // generate a timestamped url to force client caches to update if the file has changed
            var publicUrl = _storageProvider.GetPublicUrl(filePath);
            var timestamp = _storageProvider.GetFile(filePath).GetLastUpdated().Ticks;
            return publicUrl + "?v=" + timestamp.ToString(CultureInfo.InvariantCulture);
        }

        // TODO: Update this method once the storage provider has been updated
        private Stream GetImage(string path) {
            var storagePath = _storageProvider.GetStoragePath(path);
            if (storagePath != null) {
                try {
                    var file = _storageProvider.GetFile(storagePath);
                    return file.OpenRead();
                }
                catch {
                    Logger.Error("path:" + path + " storagePath:" + storagePath);
                }
            }

            // http://blob.storage-provider.net/my-image.jpg
            if (Uri.IsWellFormedUriString(path, UriKind.Absolute)) {
                return new WebClient().OpenRead(new Uri(path));
            }

            // ~/Media/Default/images/my-image.jpg
            if (VirtualPathUtility.IsAppRelative(path)) {
                var request = _services.WorkContext.HttpContext.Request;
                return new WebClient().OpenRead(new Uri(request.Url, VirtualPathUtility.ToAbsolute(path)));
            }

            return null;
        }

        private bool TryGetImageLastUpdated(string path, out DateTime lastUpdated) {
            var storagePath = _storageProvider.GetStoragePath(path);
            if (storagePath != null) {
                var file = _storageProvider.GetFile(storagePath);
                lastUpdated = file.GetLastUpdated();
                return true;
            }

            lastUpdated = DateTime.MinValue;
            return false;
        }

        private static readonly char[] _disallowed = @"/:?#\[\]@!$&'()*+,.;=\s\""\<\>\\\|%".ToCharArray();

        private static string CreateDefaultFileName(string path) {
            var extention = Path.GetExtension(path);
            var newPath = Path.ChangeExtension(path, "");
            newPath = newPath.TrimEnd('.').RemoveDiacritics();
            var normalized = newPath.ToCharArray();
            for (var i = 0; i < normalized.Length; i++) {
                if (Array.IndexOf(_disallowed, normalized[i]) >= 0) {
                    normalized[i] = '_';
                }
            }

            return new string(normalized) + extention;
        }
    }
}
