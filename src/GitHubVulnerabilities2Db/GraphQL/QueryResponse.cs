// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace GitHubVulnerabilities2Db.GraphQL
{
    /// <summary>
    /// A GraphQL response object.
    /// </summary>
    /// <typeparam name="TResponse">The type of response data being retrieved.</typeparam>
    /// <typeparam name="TNode">The type of node in the response data.</typeparam>
    public class QueryResponse
    {
        public QueryResponseData Data { get; set; }
    }

    public class QueryResponseData
    {
        /// <summary>
        /// https://developer.github.com/v4/object/securityvulnerabilityconnection/
        /// </summary>
        public ConnectionResponseData<SecurityVulnerability> SecurityVulnerabilities { get; set; }

        /// <summary>
        /// https://developer.github.com/v4/object/securityadvisoryconnection/
        /// </summary>
        public ConnectionResponseData<SecurityAdvisory> SecurityAdvisories { get; set; }
    }

    /// <summary>
    /// Allows accessing <typeparamref name="TNode"/>s returned by GraphQL query.
    /// </summary>
    public class ConnectionResponseData<TNode> where TNode : INode
    {
        public IEnumerable<Edge<TNode>> Edges { get; set; }
        public IEnumerable<TNode> Nodes { get; set; }
    }

    /// <summary>
    /// Wraps a <typeparamref name="TNode"/> with its <see cref="Cursor"/>.
    /// </summary>
    public class Edge<TNode> where TNode : INode
    {
        public string Cursor { get; set; }
        public TNode Node { get; set; }
    }
}
