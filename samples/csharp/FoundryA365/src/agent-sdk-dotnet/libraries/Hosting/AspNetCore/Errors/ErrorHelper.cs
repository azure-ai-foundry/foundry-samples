
using Microsoft.Agents.Core.Errors;

namespace Microsoft.Agents.Hosting.AspNetCore.Errors
{
    /// <summary>
    /// Error helper for the Agent SDK core system
    /// This is used to setup the localized error codes for the AgentSDK
    /// 
    /// Each Error should be created as as an AgentAuthErrorDefinition and added to the ErrorHelper class
    /// Each definition should include an error code as a - from the base error code, a description sorted in the Resource.resx file to support localization, and a help link pointing to an AKA link to get help for the given error. 
    /// 
    /// 
    /// when used, there are is 2 methods in used in the general space. 
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
    internal static partial class ErrorHelper
    {
        // Base error code for the builder: -40500
        internal static readonly AgentErrorDefinition HttpProactiveMissingCreateBody = new(-40500, Properties.Resources.HttpProactiveMissingCreateBody, "https://aka.ms/M365AgentsErrorCodes/#-40500");
        internal static readonly AgentErrorDefinition HttpProactiveMissingConversationBody = new(-40501, Properties.Resources.HttpProactiveMissingConversationBody, "https://aka.ms/M365AgentsErrorCodes/#-40501");
        internal static readonly AgentErrorDefinition HttpProactiveMissingActivityBody = new(-40502, Properties.Resources.HttpProactiveMissingActivityBody, "https://aka.ms/M365AgentsErrorCodes/#-40502");
        internal static readonly AgentErrorDefinition HttpProactiveMissingSendBody = new(-40503, Properties.Resources.HttpProactiveMissingSendBody, "https://aka.ms/M365AgentsErrorCodes/#-40503");
        internal static readonly AgentErrorDefinition HttpProactiveDuplicateContinueKey = new(-40504, Properties.Resources.HttpProactiveDuplicateContinueKey, "https://aka.ms/M365AgentsErrorCodes/#-40504");
    }
}
