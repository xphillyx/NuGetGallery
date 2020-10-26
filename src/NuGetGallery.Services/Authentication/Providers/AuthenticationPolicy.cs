// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGetGallery.Authentication.Providers
{
    public class AuthenticationPolicy
    {
        public string Email { get; set; }

        public bool EnforceMultiFactorAuthentication { get; set; }

        public bool ForceReenterCredentials { get; set; }

        private const string _enforceMutliFactorAuthenticationToken = "enforce_mfa";
        private const string _emailToken = "email";
        private const string _forceReenterCredentials = "force_reenter_credentials";

        public IDictionary<string, string> GetProperties()
        {
            var dictionary = new Dictionary<string, string>();
            dictionary.Add(_emailToken, Email);
            dictionary.Add(_enforceMutliFactorAuthenticationToken, EnforceMultiFactorAuthentication.ToString());
            dictionary.Add(_forceReenterCredentials, ForceReenterCredentials.ToString());

            return dictionary;
        }

        public static bool TryGetPolicyFromProperties(IDictionary<string, string> properties, out AuthenticationPolicy policy)
        {
            if (properties != null
                && properties.TryGetValue(_emailToken, out string email)
                && properties.TryGetValue(_enforceMutliFactorAuthenticationToken, out string enforceMfaValue)
                && properties.TryGetValue(_forceReenterCredentials, out string forceReenterCredentials))
            {
                policy = new AuthenticationPolicy()
                {
                    Email = email,
                    EnforceMultiFactorAuthentication = Convert.ToBoolean(enforceMfaValue),
                    ForceReenterCredentials = Convert.ToBoolean(forceReenterCredentials)
                };

                return true;
            }

            policy = null;
            return false;
        }

    }
}