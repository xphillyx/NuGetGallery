// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace GitHubVulnerabilities2Db.Collector
{
    public interface INodeCollector
    {
        /// <summary>
        /// Queries for any new or updated nodes using a cursor, processes them, and then updates the cursor.
        /// </summary>
        Task Process(CancellationToken token);
    }
}
