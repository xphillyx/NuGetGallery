// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web;

namespace NuGetGallery.Cookies
{
    /// <summary>
    /// Domain service, used to get domains given HttpContext.
    /// </summary>
    public interface IDomainService
    {
        /// <summary>
        /// The function is used to get the domain given HttpContext;
        /// </summary>
        /// <param name="httpContext">The httpContext.</param>
        /// <param name="domain">The domain obtained from HttpContext.</param>
        bool TryGetDomain(HttpContextBase httpContext, out string domain);

        /// <summary>
        /// The function is used to get the root domain given HttpContext;
        /// </summary>
        /// <param name="httpContext">The httpContext.</param>
        /// <param name="rootDomain">The root domain obtained from HttpContext.</param>
        bool TryGetRootDomain(HttpContextBase httpContext, out string rootDomain);
    }
}