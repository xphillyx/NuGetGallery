// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Autofac;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Knapcode.MiniZip;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGetGallery;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using GalleryTools.Utils;
using NuGet.Services.Sql;

namespace GalleryTools.Commands
{
    public abstract class BackfillCommand<TMetadata>
    {
        protected abstract string MetadataFileName { get; }

        protected virtual string ErrorsFileName => "errors.txt";

        protected virtual string CursorFileName => "cursor.txt";

        protected virtual string MonitoringCursorFileName => "monitoring_cursor.txt";

        protected virtual int CollectBatchSize => 10;

        protected virtual int UpdateBatchSize => 100;

        protected virtual int LimitTo => 0;

        protected virtual MetadataSourceType SourceType => MetadataSourceType.NuspecOnly;

        protected virtual Expression<Func<Package, object>> QueryIncludes => null;

        protected IPackageService _packageService;

        private static int DegreeOfParallelism => 16;

        private static readonly SemaphoreSlim collectSemaphore = new SemaphoreSlim(1);
        private static readonly SemaphoreSlim updateSemaphore = new SemaphoreSlim(1);

        public static void Configure<TCommand>(CommandLineApplication config) where TCommand : BackfillCommand<TMetadata>, new()
        {
            config.Description = "Backfill metadata for packages in the gallery";

            var lastCreateTimeOption = config.Option("-l | --lastcreatetime", "The latest creation time of packages we should check", CommandOptionType.SingleValue);
            var collectData = config.Option("-c | --collect", "Collect metadata and save it in a file", CommandOptionType.NoValue);
            var updateDB = config.Option("-u | --update", "Update the database with collected metadata", CommandOptionType.NoValue);
            var fileName = config.Option("-f | --file", "The file to use", CommandOptionType.SingleValue);
            var serviceDiscoveryUri = config.Option("-s | --servicediscoveryuri", "The ServiceDiscoveryUri.", CommandOptionType.SingleValue);

            config.HelpOption("-? | -h | --help");

            config.OnExecute(async () =>
            {
                var builder = new ContainerBuilder();
                builder.RegisterAssemblyModules(typeof(DefaultDependenciesModule).Assembly);
                var container = builder.Build();

                var sqlConnectionFactory = container.Resolve<ISqlConnectionFactory>();
                var sqlConnection = await sqlConnectionFactory.CreateAsync();
                var serviceDiscoveryUriValue = new Uri(serviceDiscoveryUri.Value());

                var command = new TCommand();
                command._packageService = container.Resolve<IPackageService>();

                var metadataFileName = fileName.HasValue() ? fileName.Value() : command.MetadataFileName;
                
                if (collectData.HasValue())
                {
                    var lastCreateTime = DateTime.MaxValue;

                    if (lastCreateTimeOption.HasValue())
                    {
                        var lastCreateTimeString = lastCreateTimeOption.Value();

                        if (!DateTime.TryParse(lastCreateTimeString, out lastCreateTime))
                        {
                            Console.WriteLine($"Last create time is not valid. Got: {lastCreateTimeString}");
                            return 1;
                        }
                    }

                    await command.Collect(sqlConnection, serviceDiscoveryUriValue, lastCreateTime, metadataFileName);
                }

                if (updateDB.HasValue())
                {
                    await command.Update(sqlConnection, metadataFileName);
                }

                return 0;
            });
        }

        public async Task Collect(SqlConnection connection, Uri serviceDiscoveryUri, DateTime? lastCreateTime, string fileName)
        {
            using (var context = new EntitiesContext(connection, readOnly: true))
            using (var cursor = new FileCursor(CursorFileName))
            using (var logger = new Logger(ErrorsFileName))
            {
                context.SetCommandTimeout(300); // large query

                var startTime = await cursor.Read();

                logger.Log($"Starting metadata collection - Cursor time: {startTime:u}");

                var repository = new EntityRepository<Package>(context);

                var packages = repository.GetAll().Include(p => p.PackageRegistration);
                if (QueryIncludes != null)
                {
                    packages = packages.Include(QueryIncludes);
                }

                packages = packages
                    .Where(p => p.Created < lastCreateTime && p.Created > startTime)
                    .Where(p => p.PackageStatusKey == PackageStatus.Available)
                    .OrderBy(p => p.Created);
                if (LimitTo > 0)
                {
                    packages = packages.Take(LimitTo);
                }

                var flatContainerUri = await GetFlatContainerUri(serviceDiscoveryUri);

                using (var csv = CreateCsvWriter(fileName))
                using (var http = new HttpClient())
                {
                    // We want these downloads ignored by stats pipelines - this user agent is automatically skipped.
                    // See https://github.com/NuGet/NuGet.Jobs/blob/262da48ed05d0366613bbf1c54f47879aad96dcd/src/Stats.ImportAzureCdnStatistics/StatisticsParser.cs#L41
                    http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", 
                        "Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; Trident/5.0; AppInsights)   Backfill Job: NuGet.Gallery GalleryTools");

                    var counter = 0;
                    var lastCreatedDate = default(DateTime?);

                    var packageBag = new ConcurrentBag<Package>(packages);
                    await Repeat(
                        async () =>
                        {
                            await Task.Yield();
                            while (packageBag.TryTake(out var package))
                            {
                                var id = package.PackageRegistration.Id;
                                var version = package.NormalizedVersion;
                                var idLowered = id.ToLowerInvariant();
                                var versionLowered = version.ToLowerInvariant();

                                try
                                {
                                    var metadata = default(TMetadata);

                                    var nuspecUri =
                                        $"{flatContainerUri}/{idLowered}/{versionLowered}/{idLowered}.nuspec";
                                    using (var nuspecStream = await http.GetStreamAsync(nuspecUri))
                                    {
                                        var document = LoadDocument(nuspecStream);

                                        var nuspecReader = new NuspecReader(document);

                                        if (SourceType == MetadataSourceType.NuspecOnly)
                                        {
                                            metadata = ReadMetadata(nuspecReader);
                                        }
                                        else if (SourceType == MetadataSourceType.Nupkg)
                                        {
                                            var nupkgUri =
                                                $"{flatContainerUri}/{idLowered}/{versionLowered}/{idLowered}.{versionLowered}.nupkg";
                                            metadata = await FetchMetadataAsync(http, nupkgUri, nuspecReader, id, version, logger);
                                        }
                                    }

                                    if (ShouldWriteMetadata(metadata))
                                    {
                                        WriteMetadata(id, version, metadata, package.Created, csv, logger, ref counter, ref lastCreatedDate, cursor);
                                    }
                                }
                                catch (Exception e)
                                {
                                    await logger.LogPackageError(id, version, e);
                                }
                            }
                        }, DegreeOfParallelism
                    );

                    if (counter > 0 && lastCreatedDate.HasValue)
                    {
                        await cursor.Write(lastCreatedDate.Value);
                    }
                }
            }
        }

        public async Task Update(SqlConnection connection, string fileName)
        {
            if (!File.Exists(fileName))
            {
                throw new ArgumentException($"File '{fileName}' doesn't exist");
            }

            using (var context = new EntitiesContext(connection, readOnly: false))
            using (var cursor = new FileCursor(CursorFileName))
            using (var logger = new Logger(ErrorsFileName))
            {
                var startTime = await cursor.Read();

                logger.Log($"Starting database update - Cursor time: {startTime:u}");

                var repository = new EntityRepository<Package>(context);

                var packages = repository.GetAll().Include(p => p.PackageRegistration);
                if (QueryIncludes != null)
                {
                    packages = packages.Include(QueryIncludes);
                }

                var csvBag = new ConcurrentBag<PackageMetadata>(GetCsvMetadataAsync(fileName));
                var counter = 0;
                var lastCreatedDate = default(DateTime?);
                await Repeat(
                    async () =>
                    {
                        await Task.Yield();
                        while (csvBag.TryTake(out var metadata))
                        {
                            var package = packages.FirstOrDefault(p => p.PackageRegistration.Id == metadata.Id && p.NormalizedVersion == metadata.Version);
                            if (package != null)
                            {
                                UpdatePackage(package, metadata.Metadata, context);
                                logger.LogPackage(metadata.Id, metadata.Version, "Metadata updated.");
                                CommitBatch(context, metadata, package.Created, logger, ref counter,
                                    ref lastCreatedDate, cursor);
                            }
                            else
                            {
                                await logger.LogPackageError(metadata.Id, metadata.Version, "Could not find package in the database.");
                            }
                        }
                    }, DegreeOfParallelism
                );
                    
                if (counter > 0)
                {
                    await CommitBatch(context, cursor, logger, lastCreatedDate);
                }
            }
        }

        protected virtual TMetadata ReadMetadata(NuspecReader reader) => default;

        protected virtual TMetadata ReadMetadata(IList<string> files, NuspecReader nuspecReader) => default;

        protected abstract bool ShouldWriteMetadata(TMetadata metadata);

        protected abstract void ConfigureClassMap(PackageMetadataClassMap map);

        protected abstract void UpdatePackage(Package package, TMetadata metadata, EntitiesContext context);

        /// <summary>
        /// Creates a number of tasks specified by <paramref name="degreeOfParallelism"/> using <paramref name="taskFactory"/> and then runs them in parallel.
        /// </summary>
        /// <param name="taskFactory">Creates each task to run.</param>
        /// <param name="degreeOfParallelism">The number of tasks to create. Defaults to <see cref="ServicePointManager.DefaultConnectionLimit"/></param>
        /// <returns>A task that completes when all tasks have completed.</returns>
        private static Task Repeat(Func<Task> taskFactory, int? degreeOfParallelism = null)
        {
            return Task.WhenAll(
                Enumerable
                    .Repeat(taskFactory, degreeOfParallelism ?? ServicePointManager.DefaultConnectionLimit)
                    .Select(f => f()));
        }

        private void WriteMetadata(string id, string version, TMetadata metadata, DateTime packageCreated,
            CsvWriter csv, Logger logger, ref int counter, ref DateTime? lastCreatedDate, FileCursor cursor)
        {
            // We need this to be thread-safe as it's called by multiple tasks concurrently
            collectSemaphore.Wait();

            try
            {
                if (ShouldWriteMetadata(metadata))
                {
                    var record = new PackageMetadata(id, version, metadata, packageCreated);

                    csv.WriteRecord(record);

                    csv.NextRecordAsync().Wait();

                    logger.LogPackage(id, version, $"Metadata saved");
                }

                counter++;

                if (!lastCreatedDate.HasValue || lastCreatedDate < packageCreated)
                {
                    lastCreatedDate = packageCreated;
                }

                if (counter >= CollectBatchSize)
                {
                    logger.Log($"Writing {packageCreated:u} to cursor...");
                    cursor.Write(packageCreated).Wait();
                    counter = 0;

                    // Write a monitoring cursor (not locked) so for a large job we can inspect progress
                    if (!string.IsNullOrEmpty(MonitoringCursorFileName))
                    {
                        File.WriteAllText(MonitoringCursorFileName, packageCreated.ToString("G"));
                    }
                }
            }
            finally
            {
                collectSemaphore.Release();
            }
        }

        private IEnumerable<PackageMetadata> GetCsvMetadataAsync(string fileName)
        {
            using (var csv = CreateCsvReader(fileName))
            {
                var result = TryReadMetadata(csv).Result;

                while (result.Success)
                {
                    yield return result.Metadata;
                }
            }
        }

        private void CommitBatch(EntitiesContext context, PackageMetadata metadata, DateTime packageCreated,
            Logger logger, ref int counter, ref DateTime? lastCreatedDate, FileCursor cursor)
        {
            // We need this to be thread-safe as it's called by multiple tasks concurrently
            updateSemaphore.Wait();

            try
            {
                if (!lastCreatedDate.HasValue || lastCreatedDate < packageCreated)
                {
                    lastCreatedDate = metadata.Created;
                }

                counter++;
                if (counter >= UpdateBatchSize)
                {
                    CommitBatch(context, cursor, logger, metadata.Created).Wait();
                    counter = 0;
                }
            }
            finally
            {
                updateSemaphore.Release();
            }
        }

        private static async Task<string> GetFlatContainerUri(Uri serviceDiscoveryUri)
        {
            var client = new ServiceDiscoveryClient(serviceDiscoveryUri);

            var result = await client.GetEndpointsForResourceType("PackageBaseAddress/3.0.0");

            return result.First().AbsoluteUri.TrimEnd('/');
        }

        private async Task<TMetadata> FetchMetadataAsync(
            HttpClient httpClient, string nupkgUri, NuspecReader nuspecReader, string id, string version, Logger logger)
        {
            var httpZipProvider = new HttpZipProvider(httpClient);

            var zipDirectoryReader = await httpZipProvider.GetReaderAsync(new Uri(nupkgUri));
            var zipDirectory = await zipDirectoryReader.ReadAsync();
            var files = zipDirectory
                .Entries
                .Select(x => x.GetName())
                .ToList();

            return ReadMetadata(files, nuspecReader);
        }

        private static XDocument LoadDocument(Stream stream)
        {
            var settings = new XmlReaderSettings
            {
                IgnoreWhitespace = true,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
            };
            
            // This is intentionally separate from the object initializer so that FXCop can see it.
            settings.XmlResolver = null;

            using (var streamReader = new StreamReader(stream))
            using (var xmlReader = XmlReader.Create(streamReader, settings))
            {
                return XDocument.Load(xmlReader, LoadOptions.None);
            }
        }

        private CsvWriter CreateCsvWriter(string fileName)
        {
            var configuration = CreateCsvConfiguration();

            var writer = new StreamWriter(fileName, append: true) { AutoFlush = true };

            // Seek to the end for appending...
            writer.BaseStream.Seek(0, SeekOrigin.End);

            return new CsvWriter(writer, configuration);
        }

        private CsvReader CreateCsvReader(string fileName)
        {
            var configuration = CreateCsvConfiguration();

            var reader = new StreamReader(fileName);

            var csvReader = new CsvReader(reader, configuration);
            csvReader.Configuration.MissingFieldFound = null;
            return csvReader;
        }

        private Configuration CreateCsvConfiguration()
        {
            var configuration = new Configuration
            {
                HasHeaderRecord = false,
            };

            var map = new PackageMetadataClassMap();

            ConfigureClassMap(map);

            configuration.RegisterClassMap(map);

            return configuration;
        }

        private static async Task<(bool Success, PackageMetadata Metadata)> TryReadMetadata(CsvReader reader)
        {
            if (await reader.ReadAsync())
            {
                return (true, reader.GetRecord<PackageMetadata>());
            }

            return (false, null);
        }

        private async Task CommitBatch(EntitiesContext context, FileCursor cursor, Logger logger, DateTime? cursorTime)
        {
            logger.Log("Committing batch...");

            var count = await context.SaveChangesAsync();

            if (cursorTime.HasValue)
            {
                await cursor.Write(cursorTime.Value);

                // Write a monitoring cursor (not locked) so for a large job we can inspect progress
                if (!string.IsNullOrEmpty(MonitoringCursorFileName))
                {
                    File.WriteAllText(MonitoringCursorFileName, cursorTime.Value.ToString("G"));
                }
            }

            logger.Log($"{count} packages saved.");
        }

        protected class PackageMetadata
        {
            public PackageMetadata(string id, string version, TMetadata metadata, DateTime created)
            {
                Id = id;
                Version = version;
                Metadata = metadata;
                Created = created;
            }

            public PackageMetadata()
            {
                // Used for CSV deserialization.
            }

            public string Id { get; set; }

            public string Version { get; set; }

            public TMetadata Metadata { get; set; }

            public DateTime Created { get; set; }
        }

        protected class PackageMetadataClassMap : ClassMap<PackageMetadata>
        {
            public PackageMetadataClassMap()
            {
                Map(x => x.Created).Index(0).TypeConverter<DateTimeConverter>();
                Map(x => x.Id).Index(1);
                Map(x => x.Version).Index(2);
            }
        }

        private class FileCursor : IDisposable
        {
            public FileCursor(string fileName)
            {
                Stream = File.Open(fileName, FileMode.OpenOrCreate);
                Writer = new StreamWriter(Stream) { AutoFlush = true };
            }

            public FileStream Stream { get; }

            private StreamWriter Writer { get; }

            public async Task<DateTime> Read()
            {
                using (var reader = new StreamReader(Stream, Encoding.UTF8, false, 1024, leaveOpen: true))
                {
                    reader.BaseStream.Seek(0, SeekOrigin.Begin);

                    var value = await reader.ReadLineAsync();

                    if (DateTime.TryParse(value, out var cursorTime))
                    {
                        return cursorTime;
                    }

                    return DateTime.MinValue;
                }
            }

            public Task Write(DateTime cursor)
            {
                Writer.BaseStream.Seek(0, SeekOrigin.Begin);

                return Writer.WriteAsync(cursor.ToString("o"));
            }

            public void Dispose()
            {
                Writer.Dispose();
            }
        }

        private class Logger : IDisposable
        {
            public Logger(string fileName)
            {
                var stream = File.Open(fileName, FileMode.OpenOrCreate);

                stream.Seek(0, SeekOrigin.Begin);

                Writer = new StreamWriter(stream) { AutoFlush = true };
            }

            private StreamWriter Writer { get; }

            public void Log(string message)
            {
                Console.WriteLine($"[{DateTime.Now:u}] {message}");
            }

            public void LogPackage(string id, string version, string message)
            {
                Log($"[{id}@{version}] {message}");
            }

            public Task LogPackageError(string id, string version, Exception exception)
            {
                return LogPackageError(id, version, exception.ToString());
            }

            public async Task LogPackageError(string id, string version, string message)
            {
                LogPackage(id, version, message);

                await Writer.WriteLineAsync($"[{id}@{version}] {message}");
                await Writer.WriteLineAsync();
            }

            public void Dispose()
            {
                Writer.Dispose();
            }
        }

        /// <summary>
        /// This enum allows our logic to respond to a package's need for only a nupsec to determine metadata, or whether
        /// it needs access to the .nupkg for analysis of the package
        /// </summary>
        public enum MetadataSourceType
        {
            /// <summary>
            /// Just the nuspec will suffice for metadata extraction
            /// </summary>
            NuspecOnly,
            /// <summary>
            /// We need to dig deeper into the bupkg for the metadata
            /// </summary>
            Nupkg
        }
    }
}
