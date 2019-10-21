// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GitHubVulnerabilities2Db.GraphQL;
using NuGet.Services.Cursor;

namespace GitHubVulnerabilities2Db.Collector
{
    /// <summary>
    /// Wrapper around <see cref="IQueryService"/> to make it easier to query for nodes using a cursor.
    /// </summary>
    public interface INodeCollectorQueryService<TNode> where TNode : INode
    {
        Task<IReadOnlyList<Edge<TNode>>> GetSince(ReadCursor<string> cursor, CancellationToken token);
    }
}
