// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.KeyVault;

namespace NuGetGallery
{
    public class SecretRefresher : ISecretRefresher
    {
        private readonly IRefreshableSecretReaderFactory _factory;
        private readonly ILogger<SecretRefresher> _logger;

        public SecretRefresher(
            IRefreshableSecretReaderFactory factory,
            ILogger<SecretRefresher> logger)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// We can initialize the "last refresh" time to the current time since secrets are loaded as the app is
        /// starting.
        /// </summary>
        public DateTimeOffset LastRefresh { get; private set; } = DateTimeOffset.UtcNow;

        public async Task RefreshContinuouslyAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                _logger.LogInformation("Trying to refresh the secrets.");

                TimeSpan delay;
                try
                {
                    await _factory.RefreshAsync(token);
                    delay = TimeSpan.FromMinutes(15);
                    LastRefresh = DateTimeOffset.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogError(0, ex, "An exception was thrown while refreshing the secrets.");

                    delay = TimeSpan.FromMinutes(5);
                }

                if (token.IsCancellationRequested)
                {
                    return;
                }

                _logger.LogInformation(
                    "Waiting {Duration} before attempting to refresh the secrets again.",
                    delay);
                await Task.Delay(delay, token);
            }
        }
    }
}
