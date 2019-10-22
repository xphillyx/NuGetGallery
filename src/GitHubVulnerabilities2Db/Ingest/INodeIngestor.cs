// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GitHubVulnerabilities2Db.GraphQL;

namespace GitHubVulnerabilities2Db.Ingest
{
    /// <summary>
    /// Processes new or updated nodes.
    /// </summary>
    public interface INodeIngestor<TNode> where TNode : INode
    {
        Task Ingest(IReadOnlyList<TNode> items);
    }
}
