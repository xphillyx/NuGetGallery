// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class StartValidationViewModel
    {
        public Guid ValidationTrackingId { get; set; }

        [Required]
        public string ContentType { get; set; }

        [Url]
        [Required]
        public string ContentUrl { get; set; }

        [Required]
        public string Properties { get; set; }
    }
}