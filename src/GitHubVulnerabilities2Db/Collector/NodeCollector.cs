// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GitHubVulnerabilities2Db.GraphQL;
using GitHubVulnerabilities2Db.Ingest;
using NuGet.Services.Cursor;

namespace GitHubVulnerabilities2Db.Collector
{
    public class NodeCollector<TNode> : INodeCollector where TNode : INode
    {
        public NodeCollector(
            ReadWriteCursor<string> cursor,
            INodeCollectorQueryService<TNode> queryService,
            INodeIngestor<TNode> ingestor)
        {
            _cursor = cursor;
            _queryService = queryService;
            _ingestor = ingestor;
        }

        private readonly ReadWriteCursor<string> _cursor;
        private readonly INodeCollectorQueryService<TNode> _queryService;
        private readonly INodeIngestor<TNode> _ingestor;

        public async Task Process(CancellationToken token)
        {
            var items = await _queryService.GetSince(_cursor, token);
            if (items != null && items.Any())
            {
                var latestCursor = items.Last().Cursor;
                await _ingestor.Ingest(
                    items.Select(v => v.Node).ToList(),
                    token);

                _cursor.Value = latestCursor;
                await _cursor.Save(token);
            }
        }
    }
}
