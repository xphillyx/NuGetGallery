// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Web;

namespace NuGetGallery.Cookies
{
    public class DomainService : IDomainService
    {
        public bool TryGetDomain(HttpContextBase httpContext, out string domain)
        {
            domain = null;
            if (httpContext == null)
            {
                return false;
            }

            var request = httpContext.Request;
            if (request == null || request.Url == null || request.Url.Host == null)
            {
                return false;
            }

            domain = request.Url.Host;

            return true;
        }

        public bool TryGetRootDomain(HttpContextBase httpContext, out string rootDomain)
        {
            rootDomain = null;
            if (httpContext == null)
            {
                return false;
            }

            if (!TryGetDomain(httpContext, out string domain) || domain == null)
            {
                return false;
            }

            if (IPAddress.TryParse(domain, out IPAddress iPAddress))
            {
                return false;
            }

            var index1 = domain.LastIndexOf('.');
            if (index1 < 0)
            {
                rootDomain = domain;
                return true;
            }

            var index2 = domain.LastIndexOf('.', index1 - 1);
            if (index2 < 0)
            {
                rootDomain = domain;
                return true;
            }

            rootDomain = domain.Substring(index2 + 1);
            return true;
        }
    }
}