// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GitHubVulnerabilities2Db.GraphQL;
using NuGet.Services.Cursor;

namespace GitHubVulnerabilities2Db.Collector
{
    public class AdvisoryCollectorQueryService : IAdvisoryCollectorQueryService
    {
        private const int First = 100;

        public AdvisoryCollectorQueryService(IQueryService queryService)
        {
            _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        }

        private readonly IQueryService _queryService;

        public async Task<IReadOnlyList<SecurityAdvisory>> GetAdvisoriesSinceAsync(ReadCursor<DateTimeOffset> cursor, CancellationToken token)
        {
            await cursor.Load(token);
            var firstResponse = await _queryService.QueryAsync(CreateQuery(updatedSince: cursor.Value), token);
            var lastAdvisoryEdges = firstResponse?.Data?.SecurityAdvisories?.Edges?.ToList();
            var advisories = lastAdvisoryEdges.Select(e => e.Node).ToList();
            while (lastAdvisoryEdges.Any())
            {
                var response = await _queryService.QueryAsync(CreateQuery(cursor: lastAdvisoryEdges.Last().Cursor), token);
                lastAdvisoryEdges = response?.Data?.SecurityAdvisories?.Edges?.ToList();
                advisories.AddRange(lastAdvisoryEdges.Select(e => e.Node));
            }

            return await Task.WhenAll(advisories.Select(a => FetchAllVulnerabilities(a, token)));
        }

        private async Task<SecurityAdvisory> FetchAllVulnerabilities(SecurityAdvisory advisory, CancellationToken token)
        {
            // If the last time we fetched this advisory, it returned the maximum amount of vulnerabilities, query again to fetch the next batch.
            var lastAdvisory = advisory;
            while (lastAdvisory.Vulnerabilities.Edges.Count() == First)
            {
                var additional = await _queryService.QueryAsync(CreateAdditionalQuery(advisory), token);
                var nextAdvisory = additional.Data.SecurityAdvisory;
                lastAdvisory = nextAdvisory;
                advisory = MergeAdvisories(advisory, nextAdvisory);
            }

            // We have seen some duplicate ranges (same ID and version range) returned by the API before, so make sure to dedupe the ranges.
            var comparer = new VulnerabilityForSameAdvisoryComparer();
            if (advisory.Vulnerabilities?.Edges != null)
            {
                advisory.Vulnerabilities.Edges = advisory.Vulnerabilities.Edges.Distinct(comparer);
            }

            if (advisory.Vulnerabilities?.Nodes != null)
            {
                advisory.Vulnerabilities.Nodes = advisory.Vulnerabilities.Nodes.Distinct(comparer);
            }

            return advisory;
        }

        private string CreateQuery(DateTimeOffset? updatedSince = null, string cursor = null)
        {
            return @"
{
  securityAdvisories(first: " + First + ", orderBy: {field: UPDATED_AT, direction: ASC}" + 
            (!updatedSince.HasValue || updatedSince == DateTimeOffset.MinValue ? "" : $", updatedSince: \"{updatedSince.Value.ToString("O")}\"") + 
            (string.IsNullOrWhiteSpace(cursor) ? "" : $", after: \"{cursor}\"") + @") {
    edges {
      cursor
      node {
        databaseId
        ghsaId
        severity
        updatedAt
        references {
          url
        }
        " + CreateVulnerabilitiesConnectionQuery(null) + @"
      }
    }
  }
}";
        }

        private string CreateVulnerabilitiesConnectionQuery(string edgeCursor)
        {
            return @"vulnerabilities(first: 100, ecosystem: NUGET, orderBy: {field: UPDATED_AT, direction: ASC}" + (string.IsNullOrEmpty(edgeCursor) ? "" : $", after: \"{edgeCursor}\"") + @") {
        edges {
          cursor
          node {
            package {
              name
            }
            vulnerableVersionRange
            updatedAt
          }
        }
      }";
        }

        private string CreateAdditionalQuery(SecurityAdvisory advisory)
        {
            return @"
{
  securityAdvisory(ghsaId: " + advisory.GhsaId + @") {
    references {
      url
    }
    severity
    updatedAt
    identifiers {
      type
      value
    }
    " + CreateVulnerabilitiesConnectionQuery(advisory.Vulnerabilities.Edges.Last().Cursor) + @"
  }
}";
        }

        private SecurityAdvisory MergeAdvisories(SecurityAdvisory advisory, SecurityAdvisory nextAdvisory)
        {
            // We want to keep the next advisory's data, but prepend the existing vulnerabilities that were returned in previous queries.
            nextAdvisory.Vulnerabilities.Nodes = advisory.Vulnerabilities.Nodes.Concat(nextAdvisory.Vulnerabilities.Nodes);
            nextAdvisory.Vulnerabilities.Edges = advisory.Vulnerabilities.Edges.Concat(nextAdvisory.Vulnerabilities.Edges);
            // We are not querying the advisories feed so we do not want to advance the advisory cursor past the initial advisory.
            nextAdvisory.UpdatedAt = advisory.UpdatedAt;
            return nextAdvisory;
        }

        private class VulnerabilityForSameAdvisoryComparer : IEqualityComparer<SecurityVulnerability>, IEqualityComparer<Edge<SecurityVulnerability>>
        {
            public bool Equals(SecurityVulnerability x, SecurityVulnerability y)
            {
                return x?.Package?.Name == y?.Package?.Name
                    && x?.VulnerableVersionRange == y?.VulnerableVersionRange;
            }

            public bool Equals(Edge<SecurityVulnerability> x, Edge<SecurityVulnerability> y)
            {
                return Equals(x?.Node, y?.Node);
            }

            public int GetHashCode(SecurityVulnerability obj)
            {
                return Tuple
                    .Create(
                        obj?.Package?.Name, 
                        obj?.VulnerableVersionRange)
                    .GetHashCode();
            }

            public int GetHashCode(Edge<SecurityVulnerability> obj)
            {
                return GetHashCode(obj?.Node);
            }
        }
    }
}
