// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.BotBuilderSamples
{
    /// <summary>
    /// User state properties for Greeting.
    /// </summary>
    public class PasswordResetState
    {
        public string MobileNumber { get; set; }
        public string OTP { get; set; }

       
    }
}