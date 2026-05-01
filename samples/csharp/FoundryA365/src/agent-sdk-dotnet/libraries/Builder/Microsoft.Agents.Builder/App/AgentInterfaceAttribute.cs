// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Declares a transport protocol for interacting with the agent.
    /// This allows agents to expose the same functionality over multiple transport mechanisms.
    /// </summary>
    /// <remarks>
    /// This is optional if there is a single AgentApplication in the project.  If there are multiple 
    /// AgentApplications, this attribute must be used to specify the transport protocol and path for each AgentApplication.<br/><br/>
    /// The <c>protocol</c> argument is host and protocol specific.  In Agents SDK, see <see cref="Microsoft.Agents.Builder.App.AgentTransportProtocol"/>.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class AgentInterfaceAttribute(string protocol, string path, string? processDelegate = null) : Attribute
    {
        /// <summary>
        /// The transport protocol supported at this URL.
        /// <para>
        /// </para>
        /// </summary>
        public string Protocol { get; } = protocol;

        /// <summary>
        /// The relative path where this interface is available.
        /// <para>
        /// <code>
        /// "/api/messages",
        /// </code>
        /// </para>
        /// </summary>
        public string Path { get; } = path;

        /// <summary>
        /// The name of the AgentApplication method that will process requests for this interface.
        /// </summary>
        /// <remarks>
        /// <para>If not specified, incoming requests call an Host specific method.  For example, CloudAdapter.ProcessAsync.  The indicated
        /// method for the AspNet CloudAdapter would be <code>Task MyProcessAsync(HttpRequest, HttpResponse, IAgentHttpAdapter, IAgent, CancellationToken)</code></para>
        /// <para>
        /// Example for AspNet:
        /// <code>
        /// [AgentInterface(protocol: AgentTransportProtocol.ActivityProtocol, path: "/api/messages"), processDelegate: MyProcessDelegate]
        /// class MyAgent : AgentApplication
        /// {
        ///    private async Task MyProcessDelegate(HttpRequest httpRequest, HttpResponse httpResponse, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken)
        ///    {
        ///        // Custom processing logic here
        ///        
        ///        await adapter.ProcessAsync(httpRequest, httpResponse, agent, cancellationToken).ConfigureAwait(false);
        ///        
        ///        // And/or custom processing logic here
        ///    }
        /// }
        /// </code>
        /// </para>
        /// </remarks>
        public string? ProcessDelegate { get; } = processDelegate;

    }
}