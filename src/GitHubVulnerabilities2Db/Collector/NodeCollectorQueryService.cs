// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GitHubVulnerabilities2Db.GraphQL;
using NuGet.Services.Cursor;

namespace GitHubVulnerabilities2Db.Collector
{
    public abstract class NodeCollectorQueryService<TNode> : INodeCollectorQueryService<TNode> where TNode : INode
    {
        public NodeCollectorQueryService(IQueryService queryService)
        {
            _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        }

        private readonly IQueryService _queryService;

        public async Task<IReadOnlyList<Edge<TNode>>> GetSince(ReadCursor<string> cursor, CancellationToken token)
        {
            await cursor.Load(token);
            var response = await _queryService.QueryAsync(CreateQuery(cursor.Value), token);
            return GetConnectionData(response?.Data)?.Edges?.ToList();
        }

        protected abstract ConnectionResponseData<TNode> GetConnectionData(QueryResponseData data);
        protected abstract string CreateQuery(string cursorValue);
    }

    public class AdvisoryCollectorQueryService : NodeCollectorQueryService<SecurityAdvisory>
    {
        public AdvisoryCollectorQueryService(IQueryService queryService) : base(queryService)
        {
        }

        protected override string CreateQuery(string cursorValue)
        {
            return @"
{
  securityAdvisories(first: 100, orderBy: {field: UPDATED_AT, direction: ASC}" + (string.IsNullOrEmpty(cursorValue) ? "" : $", after: \"{cursorValue}\"") + @") {
    edges {
      cursor
      node {
        databaseId
        description
        identifiers {
          type
          value
        }
        origin
        publishedAt
        references {
          url
        }
        severity
        updatedAt
        withdrawnAt
      }
    }
  }
}";
        }

        protected override ConnectionResponseData<SecurityAdvisory> GetConnectionData(QueryResponseData data) => data?.SecurityAdvisories;
    }

    public class VulnerabilityCollectorQueryService : NodeCollectorQueryService<SecurityVulnerability>
    {
        public VulnerabilityCollectorQueryService(IQueryService queryService) : base(queryService)
        {
        }

        protected override string CreateQuery(string cursorValue)
        {
            return @"
{
  securityVulnerabilities(first: 100, ecosystem: NUGET, orderBy: {field: UPDATED_AT, direction: ASC}" + (string.IsNullOrEmpty(cursorValue) ? "" : $", after: \"{cursorValue}\"") + @") {
    edges {
      cursor
      node {
        package {
          name
        }
        vulnerableVersionRange
        firstPatchedVersion {
          identifier
        }
        severity
        updatedAt
        advisory {
          databaseId
          description
          identifiers {
            type
            value
          }
          origin
          publishedAt
          references {
            url
          }
          severity
          updatedAt
          withdrawnAt
        }
      }
    }
  }
}";
        }

        protected override ConnectionResponseData<SecurityVulnerability> GetConnectionData(QueryResponseData data) => data?.SecurityVulnerabilities;
    }
}
