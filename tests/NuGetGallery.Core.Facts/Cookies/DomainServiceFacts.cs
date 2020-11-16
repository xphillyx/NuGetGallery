// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace NuGetGallery.Cookies
{
    public class DomainServiceFacts
    {
        public class TheTryGetDomainMethod
        {
            [Fact]
            public void TryGetDomainWithNullHttpContext()
            {
                // Arrange
                var domainService = new DomainService(Mock.Of<ILogger>());

                // Act & Assert
                Assert.False(domainService.TryGetDomain(httpContext: null, out string domain));
                Assert.Null(domain);
            }

            [Fact]
            public void TryGetDomainWithNullHttpRequest()
            {
                // Arrange
                var domainService = new DomainService(Mock.Of<ILogger>());

                // Act & Assert
                Assert.False(domainService.TryGetDomain(httpContext: Mock.Of<HttpContextBase>(), out string domain));
                Assert.Null(domain);
            }

            [Fact]
            public void TryGetDomainWithNullUrl()
            {
                // Arrange
                var domainService = new DomainService(Mock.Of<ILogger>());

                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(c => c.Request).Returns(Mock.Of<HttpRequestBase>());

                // Act & Assert
                Assert.False(domainService.TryGetDomain(httpContext.Object, out string domain));
                Assert.Null(domain);
            }

            [Theory]
            [InlineData("http://anydomain", "anydomain")]
            [InlineData("https://anydomain", "anydomain")]
            [InlineData("http://anydomain/", "anydomain")]
            [InlineData("https://anydomain/", "anydomain")]
            [InlineData("http://anydomain:80", "anydomain")]
            [InlineData("https://anydomain:443", "anydomain")]
            [InlineData("http://anydomain:80/", "anydomain")]
            [InlineData("https://anydomain:443/", "anydomain")]
            [InlineData("http://anydomain:80/anypath", "anydomain")]
            [InlineData("https://anydomain:443/anypath", "anydomain")]
            [InlineData("http://anydomain.test", "anydomain.test")]
            [InlineData("https://anydomain.test", "anydomain.test")]
            [InlineData("http://anydomain.test/", "anydomain.test")]
            [InlineData("https://anydomain.test/", "anydomain.test")]
            [InlineData("http://anydomain.test:80/", "anydomain.test")]
            [InlineData("https://anydomain.test:443/", "anydomain.test")]
            [InlineData("http://subdomain.anydomain.test", "subdomain.anydomain.test")]
            [InlineData("https://subdomain.anydomain.test", "subdomain.anydomain.test")]
            [InlineData("http://subdomain.anydomain.test/", "subdomain.anydomain.test")]
            [InlineData("https://subdomain.anydomain.test/", "subdomain.anydomain.test")]
            [InlineData("http://subdomain.anydomain.test:80/", "subdomain.anydomain.test")]
            [InlineData("https://subdomain.anydomain.test:443/", "subdomain.anydomain.test")]
            [InlineData("http://subdomain.anydomain.test:80/anypath", "subdomain.anydomain.test")]
            [InlineData("https://subdomain.anydomain.test:443/anypath", "subdomain.anydomain.test")]
            [InlineData("http://1.2.3.4:80/anypath", "1.2.3.4")]
            [InlineData("http://[1234:1234:1234:1234:1234:1234:1234:1234]:1234/anypath", "[1234:1234:1234:1234:1234:1234:1234:1234]")]
            public void TryGetDomain(string uri, string expectedDomain)
            {
                // Arrange
                var domainService = new DomainService(Mock.Of<ILogger>());

                var httpContext = new Mock<HttpContextBase>();
                var httpRequest = new Mock<HttpRequestBase>();
                httpRequest.Setup(r => r.Url).Returns(new Uri(uri));
                httpContext.Setup(c => c.Request).Returns(httpRequest.Object);

                // Act & Assert
                Assert.True(domainService.TryGetDomain(httpContext.Object, out string domain));
                Assert.Equal(expectedDomain, domain);
            }
        }

        public class TheTryGetRootDomainMethod
        {
            [Fact]
            public void TryGetRootDomainWithNullHttpContext()
            {
                // Arrange
                var domainService = new DomainService(Mock.Of<ILogger>());

                // Act & Assert
                Assert.False(domainService.TryGetRootDomain(httpContext: null, out string rootDomain));
                Assert.Null(rootDomain);
            }

            [Fact]
            public void TryGetRootDomainWithNullHttpRequest()
            {
                // Arrange
                var domainService = new DomainService(Mock.Of<ILogger>());

                // Act & Assert
                Assert.False(domainService.TryGetRootDomain(httpContext: Mock.Of<HttpContextBase>(), out string rootDomain));
                Assert.Null(rootDomain);
            }

            [Fact]
            public void TryGetRootDomainWithNullUrl()
            {
                // Arrange
                var domainService = new DomainService(Mock.Of<ILogger>());

                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(c => c.Request).Returns(Mock.Of<HttpRequestBase>());

                // Act & Assert
                Assert.False(domainService.TryGetRootDomain(httpContext.Object, out string rootDomain));
                Assert.Null(rootDomain);
            }

            [Theory]
            [InlineData("http://1.2.3.4")]
            [InlineData("https://1.2.3.4")]
            [InlineData("http://1.2.3.4/")]
            [InlineData("https://1.2.3.4/")]
            [InlineData("http://1.2.3.4:80")]
            [InlineData("https://1.2.3.4:443")]
            [InlineData("http://1.2.3.4:80/")]
            [InlineData("https://1.2.3.4:443/")]
            [InlineData("http://1.2.3.4:80/anypath")]
            [InlineData("https://1.2.3.4:443/anypath")]
            [InlineData("http://[1234:1234:1234:1234:1234:1234:1234:1234]")]
            [InlineData("https://[1234:1234:1234:1234:1234:1234:1234:1234]")]
            [InlineData("http://[1234:1234:1234:1234:1234:1234:1234:1234]/")]
            [InlineData("https://[1234:1234:1234:1234:1234:1234:1234:1234]/")]
            [InlineData("http://[1234:1234:1234:1234:1234:1234:1234:1234]:80")]
            [InlineData("https://[1234:1234:1234:1234:1234:1234:1234:1234]:443")]
            [InlineData("http://[1234:1234:1234:1234:1234:1234:1234:1234]:80/")]
            [InlineData("https://[1234:1234:1234:1234:1234:1234:1234:1234]:443/")]
            [InlineData("http://[1234:1234:1234:1234:1234:1234:1234:1234]:80/anypath")]
            [InlineData("https://[1234:1234:1234:1234:1234:1234:1234:1234]:443/anypath")]
            public void TryGetRootDomainWithIpAddress(string uri)
            {
                // Arrange
                var domainService = new DomainService(Mock.Of<ILogger>());

                var httpContext = new Mock<HttpContextBase>();
                var httpRequest = new Mock<HttpRequestBase>();
                httpRequest.Setup(r => r.Url).Returns(new Uri(uri));
                httpContext.Setup(c => c.Request).Returns(httpRequest.Object);

                // Act & Assert
                Assert.False(domainService.TryGetRootDomain(httpContext.Object, out string rootDomain));
                Assert.Null(rootDomain);
            }

            [Theory]
            [InlineData("http://anydomain", "anydomain")]
            [InlineData("https://anydomain", "anydomain")]
            [InlineData("http://anydomain/", "anydomain")]
            [InlineData("https://anydomain/", "anydomain")]
            [InlineData("http://anydomain:80", "anydomain")]
            [InlineData("https://anydomain:443", "anydomain")]
            [InlineData("http://anydomain:80/", "anydomain")]
            [InlineData("https://anydomain:443/", "anydomain")]
            [InlineData("http://anydomain:80/anypath", "anydomain")]
            [InlineData("https://anydomain:443/anypath", "anydomain")]
            [InlineData("http://anydomain.test", "anydomain.test")]
            [InlineData("https://anydomain.test", "anydomain.test")]
            [InlineData("http://anydomain.test/", "anydomain.test")]
            [InlineData("https://anydomain.test/", "anydomain.test")]
            [InlineData("http://anydomain.test:80/", "anydomain.test")]
            [InlineData("https://anydomain.test:443/", "anydomain.test")]
            [InlineData("http://subdomain.anydomain.test", "anydomain.test")]
            [InlineData("https://subdomain.anydomain.test", "anydomain.test")]
            [InlineData("http://subdomain.anydomain.test/", "anydomain.test")]
            [InlineData("https://subdomain.anydomain.test/", "anydomain.test")]
            [InlineData("http://subdomain.anydomain.test:80/", "anydomain.test")]
            [InlineData("https://subdomain.anydomain.test:443/", "anydomain.test")]
            [InlineData("http://subdomain.anydomain.test:80/anypath", "anydomain.test")]
            [InlineData("https://subdomain.anydomain.test:443/anypath", "anydomain.test")]
            public void TryGetRootDomain(string uri, string expectedRootDomain)
            {
                // Arrange
                var domainService = new DomainService(Mock.Of<ILogger>());

                var httpContext = new Mock<HttpContextBase>();
                var httpRequest = new Mock<HttpRequestBase>();
                httpRequest.Setup(r => r.Url).Returns(new Uri(uri));
                httpContext.Setup(c => c.Request).Returns(httpRequest.Object);

                // Act & Assert
                Assert.True(domainService.TryGetRootDomain(httpContext.Object, out string rootDomain));
                Assert.Equal(expectedRootDomain, rootDomain);
            }
        }
    }
}