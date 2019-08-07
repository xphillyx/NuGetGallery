// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Web;
using Moq;
using NuGetGallery.Services;
using Xunit;

namespace NuGetGallery.Infrastructure
{
    public class HijackSearchServiceFactoryFacts
    {
        [Fact]
        public void ReturnsNonPreviewWhenFeatureDisabled()
        {
            // Arrange
            _featureFlags
                .Setup(f => f.IsPreviewHijackEnabled())
                .Returns(false);

            // Act
            var result = _target.GetService();

            // Assert
            Assert.Equal(_search.Object, result);
            Assert.NotEqual(_previewSearch.Object, result);

            _telemetry.Verify(
                t => t.TrackHijackTestEvaluated(
                    /*isActive: */ It.IsAny<bool>(),
                    /*testBucket: */ It.IsAny<int>(),
                    /*testPercentage: */ It.IsAny<int>()),
                Times.Never);
        }

        [Fact]
        public void ReturnsNonPreviewAtZeroPercentage()
        {
            // Arrange
            _featureFlags
                .Setup(f => f.IsPreviewHijackEnabled())
                .Returns(true);
            _config
                .Setup(c => c.PreviewHijackPercentage)
                .Returns(0);
            SetupRequest(IpAddressInTestAt50Pct);

            // Act
            var result = _target.GetService();

            // Assert
            Assert.Equal(_search.Object, result);
            Assert.NotEqual(_previewSearch.Object, result);

            _telemetry.Verify(
                t => t.TrackHijackTestEvaluated(
                    /*isActive: */ false,
                    /*testBucket: */ It.IsAny<int>(),
                    /*testPercentage: */ 0),
                Times.Once);
        }

        [Theory]
        [MemberData(nameof(ReturnsPreviewAt100PercentData))]
        public void ReturnsPreviewAt100Percent(string ipAddress, string forwardedIpAddressOrNull)
        {
            // Arrange
            _featureFlags
                .Setup(f => f.IsPreviewHijackEnabled())
                .Returns(true);
            _config
                .Setup(c => c.PreviewHijackPercentage)
                .Returns(100);
            SetupRequest(ipAddress, forwardedIpAddressOrNull);

            // Act
            var result = _target.GetService();

            // Assert
            Assert.Equal(_previewSearch.Object, result);
            Assert.NotEqual(_search.Object, result);

            _telemetry.Verify(
                t => t.TrackHijackTestEvaluated(
                    /*isActive: */ true,
                    /*testBucket: */ It.IsAny<int>(),
                    /*testPercentage: */ 100),
                Times.Once);
        }

        public static IEnumerable<object[]> ReturnsPreviewAt100PercentData()
        {
            object[] ReturnsPreviewAt100Percent(string ipAddress, string forwardedIpAddressOrNull)
            {
                return new object[] { ipAddress, forwardedIpAddressOrNull };
            }

            yield return ReturnsPreviewAt100Percent(
                ipAddress: IpAddressInTestAt50Pct,
                forwardedIpAddressOrNull: null);

            yield return ReturnsPreviewAt100Percent(
                ipAddress: IpAddressNotInTestAt50Pct,
                forwardedIpAddressOrNull: null);

            yield return ReturnsPreviewAt100Percent(
                ipAddress: IpAddressNotInTestAt50Pct,
                forwardedIpAddressOrNull: IpAddressInTestAt50Pct);

            yield return ReturnsPreviewAt100Percent(
                ipAddress: IpAddressInTestAt50Pct,
                forwardedIpAddressOrNull: IpAddressNotInTestAt50Pct);
        }

        [Theory]
        [MemberData(nameof(ReturnsPreviewAt50PercentData))]
        public void ReturnsPreviewAt50Percent(string ipAddress, string forwardedIpAddressOrNull, bool inTest)
        {
            // Arrange
            _featureFlags
                .Setup(f => f.IsPreviewHijackEnabled())
                .Returns(true);
            _config
                .Setup(c => c.PreviewHijackPercentage)
                .Returns(50);
            SetupRequest(ipAddress, forwardedIpAddressOrNull);

            // Act
            var result = _target.GetService();

            // Assert
            if (inTest)
            {
                Assert.Equal(_previewSearch.Object, result);
                Assert.NotEqual(_search.Object, result);

                _telemetry.Verify(
                    t => t.TrackHijackTestEvaluated(
                        /*isActive: */ true,
                        /*testBucket: */ It.IsAny<int>(),
                        /*testPercentage: */ 50),
                    Times.Once);
            }
            else
            {
                Assert.Equal(_search.Object, result);
                Assert.NotEqual(_previewSearch.Object, result);

                _telemetry.Verify(
                    t => t.TrackHijackTestEvaluated(
                        /*isActive: */ false,
                        /*testBucket: */ It.IsAny<int>(),
                        /*testPercentage: */ 50),
                    Times.Once);
            }
        }

        public static IEnumerable<object[]> ReturnsPreviewAt50PercentData()
        {
            object[] ReturnsPreviewAt50Percent(string ipAddress, string forwardedIpAddressOrNull, bool inTest)
            {
                return new object[] { ipAddress, forwardedIpAddressOrNull, inTest };
            }

            yield return ReturnsPreviewAt50Percent(
                ipAddress: IpAddressInTestAt50Pct,
                forwardedIpAddressOrNull: null,
                inTest: true);

            yield return ReturnsPreviewAt50Percent(
                ipAddress: IpAddressNotInTestAt50Pct,
                forwardedIpAddressOrNull: null,
                inTest: false);

            // The forwarded IP address should have priority
            yield return ReturnsPreviewAt50Percent(
                ipAddress: IpAddressNotInTestAt50Pct,
                forwardedIpAddressOrNull: IpAddressInTestAt50Pct,
                inTest: true);

            yield return ReturnsPreviewAt50Percent(
                ipAddress: IpAddressInTestAt50Pct,
                forwardedIpAddressOrNull: IpAddressNotInTestAt50Pct,
                inTest: false);
        }

        private void SetupRequest(string ipAddress, string forwardedIpAddress = null)
        {
            var httpRequest = new Mock<HttpRequestBase>();
            var serverVariables = new NameValueCollection();

            if (forwardedIpAddress != null)
            {
                serverVariables.Add("HTTP_X_FORWARDED_FOR", forwardedIpAddress);
            }

            httpRequest
                .Setup(r => r.UserAgent)
                .Returns("Example/1.0.0");
            httpRequest
                .Setup(r => r.UserHostAddress)
                .Returns(ipAddress);
            httpRequest
                .Setup(r => r.ServerVariables)
                .Returns(serverVariables);

            _httpContext
                .Setup(c => c.Request)
                .Returns(httpRequest.Object);
        }

        private const string IpAddressInTestAt50Pct = "2.0.0.0";
        private const string IpAddressNotInTestAt50Pct = "1.0.0.0";

        private readonly Mock<HttpContextBase> _httpContext;
        private readonly Mock<IFeatureFlagService> _featureFlags;
        private readonly Mock<IABTestConfiguration> _config;
        private readonly Mock<ISearchService> _search;
        private readonly Mock<ISearchService> _previewSearch;
        private readonly Mock<ITelemetryService> _telemetry;

        private readonly HijackSearchServiceFactory _target;

        public HijackSearchServiceFactoryFacts()
        {
            _httpContext = new Mock<HttpContextBase>();
            _featureFlags = new Mock<IFeatureFlagService>();
            _config = new Mock<IABTestConfiguration>();
            _search = new Mock<ISearchService>();
            _previewSearch = new Mock<ISearchService>();
            _telemetry = new Mock<ITelemetryService>();

            var content = new Mock<IContentObjectService>();
            content
                .Setup(c => c.ABTestConfiguration)
                .Returns(_config.Object);

            _target = new HijackSearchServiceFactory(
                _httpContext.Object,
                _featureFlags.Object,
                content.Object,
                _search.Object,
                _previewSearch.Object,
                _telemetry.Object);
        }
    }
}
