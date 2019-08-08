// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;

namespace NuGetGallery
{
    public class HijackSearchServiceFactory : IHijackSearchServiceFactory
    {
        /// <summary>
        /// The hasher that maps clients to test buckets. There is a single hasher per thread
        /// to avoid performance issues from creating <see cref="SHA256"/> objects.
        /// </summary>
        [ThreadStatic]
        private static readonly SHA256 Hasher = SHA256.Create();

        private readonly HttpContextBase _httpContext;
        private readonly IFeatureFlagService _featureFlags;
        private readonly IContentObjectService _contentObjectService;
        private readonly ISearchService _search;
        private readonly ISearchService _previewSearch;
        private readonly ILogger<HijackSearchServiceFactory> _logger;

        public HijackSearchServiceFactory(
            HttpContextBase httpContext,
            IFeatureFlagService featureFlags,
            IContentObjectService contentObjectService,
            ISearchService search,
            ISearchService previewSearch,
            ILogger<HijackSearchServiceFactory> logger = null)
        {
            _httpContext = httpContext ?? throw new ArgumentNullException(nameof(httpContext));
            _featureFlags = featureFlags ?? throw new ArgumentNullException(nameof(featureFlags));
            _contentObjectService = contentObjectService ?? throw new ArgumentNullException(nameof(contentObjectService));
            _search = search ?? throw new ArgumentNullException(nameof(search));
            _previewSearch = previewSearch ?? throw new ArgumentNullException(nameof(previewSearch));
            _logger = logger;
        }

        public ISearchService GetService()
        {
            if (!_featureFlags.IsPreviewHijackEnabled())
            {
                return _search;
            }

            var testBucket = GetClientBucket();
            var testPercentage = _contentObjectService.ABTestConfiguration.PreviewHijackPercentage;
            var isActive = testBucket <= testPercentage;

            // THIS IS FOR TESTING ONLY, DO NOT MERGE THIS IN!
            _logger?.LogInformation(
                "Evaluated hijack search test, bucket:{TestBucket} test percentage:{TestPercentage}, " +
                "active:{IsActive}, real ip:{RealIp} forwarded ip:{ForwardedIp} user ip:{UserIp} user agent:{UserAgent}",
                testBucket,
                testPercentage,
                isActive,
                GetClientIpAddress(),
                _httpContext.Request.ServerVariables["HTTP_X_FORWARDED_FOR"],
                _httpContext.Request.UserHostAddress,
                _httpContext.Request.UserAgent);

            return isActive ? _previewSearch : _search;
        }

        private int GetClientBucket()
        {
            // Use the client's IP address and user agent (if present) to generate a string
            // that is constant for this client. This string will be hashed using SHA-256,
            // and the hash's first 8 bytes will be used to bucket the client from 1-100 inclusive.
            var clientData = GetClientIpAddress();
            if (_httpContext.Request.UserAgent != null)
            {
                clientData += "," + _httpContext.Request.UserAgent;
            }

            var hashedBytes = Hasher.ComputeHash(Encoding.ASCII.GetBytes(clientData));
            var value = BitConverter.ToUInt64(hashedBytes, startIndex: 0);

            return (int)(value % 100) + 1;
        }

        private string GetClientIpAddress()
        {
            // If the Gallery is behind a proxy server, the IP address will be in the
            // "HTTP_X_FORWARDED_FOR" HTTP header. Favor that over the request's IP host address.
            var ipAddress = _httpContext.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
            if (!string.IsNullOrEmpty(ipAddress))
            {
                return ipAddress;
            }

            return _httpContext.Request.UserHostAddress;
        }
    }
}