// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Reflection;

namespace Microsoft.Agents.Core.Errors
{
    /// <summary>
    /// This class describes the error definition
    /// 
    /// Each Error should be created as an AgentAuthErrorDefinition and added to the ErrorHelper class
    /// Each definition should include an error code as a - from the base error code, a description sorted in the Resource.resx file to support localization, and a help link pointing to an AKA link to get help for the given error. 
    /// 
    /// 
    /// when used, there are 2 methods used in the general space.
    /// Method 1: 
    /// Throw a new exception with the error code, description and helplink
    ///     throw new IndexOutOfRangeException(ErrorHelper.MissingAuthenticationConfiguration.description)
    ///     {
    ///         HResult = ErrorHelper.MissingAuthenticationConfiguration.code,
    ///         HelpLink = ErrorHelper.MissingAuthenticationConfiguration.helplink
    ///     };
    ///
    /// Method 2: 
    /// 
    ///     throw Microsoft.Agents.Core.Errors.ExceptionHelper.GenerateException&lt;OperationCanceledException&gt;(
    ///         ErrorHelper.NullIAccessTokenProvider, ex, $"{AgentClaims.GetAppId(claimsIdentity)}:{serviceUrl}");
    /// 
    /// </summary>
    /// <param name="code">Error code for the exception</param>
    /// <param name="description">Displayed Error message</param>
    /// <param name="helplink">Help URL Link for the Error.</param>
#if !NETSTANDARD
    public record AgentErrorDefinition(int code, string description, string helplink);
#else
    public class AgentErrorDefinition(int code, string description, string helplink)
    {
        /// <summary>
        /// Error code for the exception
        /// </summary>
        public int code { get; } = code;
        /// <summary>
        /// Displayed Error message
        /// </summary>
        public string description { get; } = description;
        /// <summary>
        /// Help URL Link for the Error.
        /// </summary>
        public string helplink { get; } = helplink;
    }
#endif

    public static class ExceptionHelper
    {
        public static T GenerateException<T>(AgentErrorDefinition errorDefinition, Exception innerException, params string[] messageFormat) where T : Exception
        {
            var excp = innerException != null
                ? (T)Activator.CreateInstance(typeof(T), new object[] { string.Format(errorDefinition.description, messageFormat), innerException })
                : (T)Activator.CreateInstance(typeof(T), new object[] { string.Format(errorDefinition.description, messageFormat) });

#if !NETSTANDARD
            excp.HResult = errorDefinition.code;
#else
            // HResult is not available in .NET Standard
            var hResultProperty = excp.GetType().GetProperty("HResult", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (hResultProperty != null && hResultProperty.CanWrite)
            {
                hResultProperty.SetValue(excp, errorDefinition.code);
            }
#endif
            excp.HelpLink = errorDefinition.helplink;
            return excp;
        }
    }
}
