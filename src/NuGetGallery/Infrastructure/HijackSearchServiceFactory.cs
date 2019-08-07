// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace NuGetGallery
{
    public class HijackSearchServiceFactory : IHijackSearchServiceFactory, IDisposable
    {
        private readonly HttpContextBase _httpContext;
        private readonly IFeatureFlagService _featureFlags;
        private readonly IContentObjectService _contentObjectService;
        private readonly ISearchService _search;
        private readonly ISearchService _previewSearch;
        private readonly ITelemetryService _telemetryService;

        private readonly SHA256 _hasher;

        public HijackSearchServiceFactory(
            HttpContextBase httpContext,
            IFeatureFlagService featureFlags,
            IContentObjectService contentObjectService,
            ISearchService search,
            ISearchService previewSearch,
            ITelemetryService telemetryService)
        {
            _httpContext = httpContext ?? throw new ArgumentNullException(nameof(httpContext));
            _featureFlags = featureFlags ?? throw new ArgumentNullException(nameof(featureFlags));
            _contentObjectService = contentObjectService ?? throw new ArgumentNullException(nameof(contentObjectService));
            _search = search ?? throw new ArgumentNullException(nameof(search));
            _previewSearch = previewSearch ?? throw new ArgumentNullException(nameof(previewSearch));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));

            _hasher = SHA256.Create();
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

            _telemetryService.TrackHijackTestEvaluated(
                isActive,
                testBucket,
                testPercentage);

            return isActive ? _previewSearch : _search;
        }

        public void Dispose() => _hasher.Dispose();

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

            var hashedBytes = _hasher.ComputeHash(Encoding.ASCII.GetBytes(clientData));
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