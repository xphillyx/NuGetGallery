// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Globalization;
using NuGet.Services.Logging;

namespace NuGetGallery
{
    public class ProcessIdTelemetryEnricher : SupportPropertiesTelemetryInitializer
    {
        private static readonly string ProcessId = Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture);

        public ProcessIdTelemetryEnricher() : base("ProcessId", ProcessId)
        {
        }
    }
}