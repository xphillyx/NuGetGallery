// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using GitHubVulnerabilities2Db.Collector;
using GitHubVulnerabilities2Db.Configuration;
using GitHubVulnerabilities2Db.Gallery;
using GitHubVulnerabilities2Db.GraphQL;
using GitHubVulnerabilities2Db.Ingest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using NuGet.Services.Cursor;
using NuGetGallery;
using NuGetGallery.Auditing;
using NuGetGallery.Security;

namespace GitHubVulnerabilities2Db
{
    public class Job : JsonConfigurationJob, IDisposable
    {
        private readonly HttpClient _client = new HttpClient();

        public override async Task Run()
        {
            var collectors = _serviceProvider.GetRequiredService<IEnumerable<INodeCollector>>();
            using (var tokenSource = new CancellationTokenSource())
            {
                foreach (var collector in collectors)
                {
                    await collector.Process(tokenSource.Token);
                }
            }
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            ConfigureInitializationSection<InitializationConfiguration>(services, configurationRoot);
        }

        public void Dispose()
        {
            _client.Dispose();
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
            containerBuilder
                .RegisterAdapter<IOptionsSnapshot<InitializationConfiguration>, InitializationConfiguration>(c => c.Value);

            ConfigureQueryServices(containerBuilder);
            ConfigureIngestionServices(containerBuilder);
            ConfigureCollectorServices(containerBuilder);
        }

        protected void ConfigureIngestionServices(ContainerBuilder containerBuilder)
        {
            ConfigureGalleryServices(containerBuilder);

            containerBuilder
                .RegisterType<PackageVulnerabilityService>()
                .As<IPackageVulnerabilityService>();

            containerBuilder
                .RegisterType<VulnerabilityIngestor>()
                .As<INodeIngestor<SecurityAdvisory>>()
                .As<INodeIngestor<SecurityVulnerability>>();
        }

        protected void ConfigureGalleryServices(ContainerBuilder containerBuilder)
        {
            containerBuilder
                .Register(ctx =>
                {
                    var connection = CreateSqlConnection<GalleryDbConfiguration>();
                    return new EntitiesContext(connection, false);
                })
                .As<IEntitiesContext>();

            containerBuilder
                .RegisterGeneric(typeof(EntityRepository<>))
                .As(typeof(IEntityRepository<>));

            containerBuilder
                .RegisterType<ThrowingAuditingService>()
                .As<IAuditingService>();

            containerBuilder
                .RegisterType<ThrowingTelemetryService>()
                .As<ITelemetryService>();

            containerBuilder
                .RegisterType<ThrowingSecurityPolicyService>()
                .As<ISecurityPolicyService>();

            containerBuilder
                .RegisterType<PackageService>()
                .As<IPackageService>();

            containerBuilder
                .RegisterType<ThrowingIndexingService>()
                .As<IIndexingService>();

            containerBuilder
                .RegisterType<PackageUpdateService>()
                .As<IPackageUpdateService>();
        }

        protected void ConfigureQueryServices(ContainerBuilder containerBuilder)
        {
            containerBuilder
                .RegisterInstance(_client)
                .As<HttpClient>();

            containerBuilder
                .RegisterType<QueryService>()
                .As<IQueryService>();

            containerBuilder
                .RegisterType<AdvisoryCollectorQueryService>()
                .As<INodeCollectorQueryService<SecurityAdvisory>>();

            containerBuilder
                .RegisterType<VulnerabilityCollectorQueryService>()
                .As<INodeCollectorQueryService<SecurityVulnerability>>();
        }

        private const string AdvisoryCursorKey = "AdvisoryCursorKey";
        private const string VulnerabilityCursorKey = "VulnerabilityCursorKey";
        protected void ConfigureCollectorServices(ContainerBuilder containerBuilder)
        {
            containerBuilder
                .Register(ctx =>
                {
                    var config = ctx.Resolve<IOptionsSnapshot<InitializationConfiguration>>().Value;
                    return CloudStorageAccount.Parse(config.StorageConnectionString);
                })
                .As<CloudStorageAccount>();

            RegisterCollector<SecurityAdvisory>(
                containerBuilder,
                config => config.AdvisoryCursorBlobName,
                AdvisoryCursorKey);

            RegisterCollector<SecurityVulnerability>(
                containerBuilder,
                config => config.VulnerabilitiesCursorBlobName,
                VulnerabilityCursorKey);
        }

        private void RegisterCollector<TNode>(
            ContainerBuilder containerBuilder,
            Func<InitializationConfiguration, string> getBlobName,
            string key) where TNode : INode
        {
            containerBuilder
                .Register(ctx => CreateCursor(ctx, getBlobName))
                .Keyed<ReadWriteCursor<string>>(key);

            containerBuilder
                .RegisterType<NodeCollector<TNode>>()
                .WithParameter(
                    (parameter, ctx) => parameter.ParameterType == typeof(ReadWriteCursor<string>),
                    (parameter, ctx) => ctx.ResolveKeyed<ReadWriteCursor<string>>(key))
                .As<INodeCollector>();
        }

        private DurableStringCursor CreateCursor(IComponentContext ctx, Func<InitializationConfiguration, string> getBlobName)
        {
            var config = ctx.Resolve<IOptionsSnapshot<InitializationConfiguration>>().Value;
            var storageAccount = ctx.Resolve<CloudStorageAccount>();
            var blob = storageAccount
                .CreateCloudBlobClient()
                .GetContainerReference(config.CursorContainerName)
                .GetBlockBlobReference(getBlobName(config));

            return new DurableStringCursor(blob);
        }
    }
}
