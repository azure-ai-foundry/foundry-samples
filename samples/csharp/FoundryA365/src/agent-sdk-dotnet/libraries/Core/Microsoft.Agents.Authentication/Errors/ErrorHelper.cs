// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Errors;

namespace Microsoft.Agents.Authentication.Errors
{
    /// <summary>
    /// Error helper for the Authentication core system
    /// This is used to setup the localized error codes for the authentication subsystem of the AgentSDK
    /// 
    /// Note: specific auth providers are expected to implement their own error codes inside their own libraries. 
    /// 
    /// Each Error should be created as an AgentAuthErrorDefinition and added to the ErrorHelper class
    /// Each definition should include an error code as a - from the base error code, a description sorted in the Resource.resx file to support localization, and a help link pointing to an AKA link to get help for the given error. 
    /// 
    /// when used, there are 2 methods.. 
    /// Method 1: 
    ///     throw new IndexOutOfRangeException(ErrorHelper.MissingAuthenticationConfiguration.description)
    ///     {
    ///         HResult = ErrorHelper.MissingAuthenticationConfiguration.code,
    ///         HelpLink = ErrorHelper.MissingAuthenticationConfiguration.helplink
    ///     };
    /// 
/// </summary>
internal static class ErrorHelper
    {
        // Base error code for the authentication provider: -40000

        internal static readonly AgentErrorDefinition MissingAuthenticationConfiguration = new(-40000, Properties.Resources.Error_MissingAuthenticationConfig, "https://aka.ms/M365AgentsErrorCodes/#-40000");
        internal static readonly AgentErrorDefinition ConnectionNotFoundByName = new(-40001, Properties.Resources.Error_ConnectionNotFoundByName, "https://aka.ms/M365AgentsErrorCodes/#-40001");
        internal static readonly AgentErrorDefinition FailedToCreateAuthModuleProvider = new(-40002, Properties.Resources.Error_FailedToCreateAuthModuleProvider, "https://aka.ms/M365AgentsErrorCodes/#-40002");
        internal static readonly AgentErrorDefinition AuthProviderTypeNotFound = new(-40003, Properties.Resources.Error_AuthModuleNotFound, "https://aka.ms/M365AgentsErrorCodes/#-40003");
        internal static readonly AgentErrorDefinition AuthProviderTypeInvalidConstructor = new(-40004, Properties.Resources.Error_InvalidAuthProviderConstructor, "https://aka.ms/M365AgentsErrorCodes/#-40004");
        internal static readonly AgentErrorDefinition ConfigurationSectionNotFound = new(-40005, Properties.Resources.Error_ConfigurationSectionNotFound, "https://aka.ms/M365AgentsErrorCodes/#-40005");
        internal static readonly AgentErrorDefinition ConfigurationSectionNotProvided = new(-40006, Properties.Resources.Error_ConfigurationSectionNotProvided, "https://aka.ms/M365AgentsErrorCodes/#-40006");

    }
}
