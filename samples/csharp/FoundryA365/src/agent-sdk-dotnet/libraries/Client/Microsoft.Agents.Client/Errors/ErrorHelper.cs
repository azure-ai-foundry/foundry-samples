
using Microsoft.Agents.Core.Errors;

namespace Microsoft.Agents.Client.Errors
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
        // Base error code for the client: -90000

        internal static readonly AgentErrorDefinition AgentHostMissingProperty = new AgentErrorDefinition(-90000, Properties.Resources.AgentHostMissingProperty, "https://aka.ms/M365AgentsErrorCodes/#-90000");
        internal static readonly AgentErrorDefinition AgentMissingProperty = new AgentErrorDefinition(-90001, Properties.Resources.AgentMissingProperty, "https://aka.ms/M365AgentsErrorCodes/#-90001");
        internal static readonly AgentErrorDefinition AgentNotFound = new AgentErrorDefinition(-90002, Properties.Resources.AgentNotFound, "https://aka.ms/M365AgentsErrorCodes/#-90002");
        internal static readonly AgentErrorDefinition SendToAgentFailed = new AgentErrorDefinition(-90003, Properties.Resources.SendToAgentFailed, "https://aka.ms/M365AgentsErrorCodes/#-90003");
        internal static readonly AgentErrorDefinition SendToAgentUnsuccessful = new AgentErrorDefinition(-90004, Properties.Resources.SendToAgentUnsuccessful, "https://aka.ms/M365AgentsErrorCodes/#-90004");
        internal static readonly AgentErrorDefinition SendToAgentUnauthorized = new AgentErrorDefinition(-90005, Properties.Resources.SendToAgentUnauthorized, "https://aka.ms/M365AgentsErrorCodes/#-90005");
        internal static readonly AgentErrorDefinition AgentTokenProviderNotFound = new AgentErrorDefinition(-90006, Properties.Resources.AgentTokenProviderNotFound, "https://aka.ms/M365AgentsErrorCodes/#-90006");
    }

}
