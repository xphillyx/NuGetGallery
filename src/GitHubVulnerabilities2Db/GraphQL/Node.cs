// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace GitHubVulnerabilities2Db.GraphQL
{
    /// <summary>
    /// Interface for types returned by the GraphQL API.
    /// </summary>
    public interface INode
    {
    }

    /// <summary>
    /// https://developer.github.com/v4/object/securityvulnerability/
    /// </summary>
    public class SecurityVulnerability : INode
    {
        public SecurityVulnerabilityPackage Package { get; set; }
        public string VulnerableVersionRange { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public SecurityAdvisory Advisory { get; set; }
    }

    /// <summary>
    /// https://developer.github.com/v4/object/securityadvisorypackage/
    /// </summary>
    public class SecurityVulnerabilityPackage
    {
        public string Name { get; set; }
    }

    /// <summary>
    /// https://developer.github.com/v4/object/securityadvisory/
    /// </summary>
    public class SecurityAdvisory : INode
    {
        public int DatabaseId { get; set; }
        public string Description { get; set; }
        public DateTimeOffset? WithdrawnAt { get; set; }
    }
}
