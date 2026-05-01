// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.CopilotStudio.Client.Models
{
    /// <summary>
    /// Base type for responses from an externally orchestrated conversation turn.
    /// Use pattern matching to handle the specific response types:
    /// <see cref="Microsoft.Agents.CopilotStudio.Client.Models.OrchestratedActivityResponse"/>, <see cref="Microsoft.Agents.CopilotStudio.Client.Models.OrchestratedStateResponse"/>,
    /// <see cref="Microsoft.Agents.CopilotStudio.Client.Models.OrchestratedErrorResponse"/>, and <see cref="Microsoft.Agents.CopilotStudio.Client.Models.OrchestratedEndResponse"/>.
    /// </summary>
#if !NETSTANDARD
    public abstract record OrchestratedResponse;

    /// <summary>
    /// An activity (message) from the copilot.
    /// </summary>
    /// <param name="Activity">The activity received from the agent.</param>
    public record OrchestratedActivityResponse(Activity Activity) : OrchestratedResponse;

    /// <summary>
    /// Agent state information returned after a turn completes.
    /// </summary>
    /// <param name="AgentState">The agent state payload containing status and metadata.</param>
    public record OrchestratedStateResponse(AgentStatePayload AgentState) : OrchestratedResponse;

    /// <summary>
    /// An error returned during an orchestrated turn.
    /// </summary>
    /// <param name="Error">The error payload containing code and message.</param>
    public record OrchestratedErrorResponse(OrchestratedErrorPayload Error) : OrchestratedResponse;

    /// <summary>
    /// Signals that the orchestrated turn has completed and the SSE stream has ended.
    /// </summary>
    /// <param name="Data">The raw data payload from the end event, if any.</param>
    public record OrchestratedEndResponse(string? Data = null) : OrchestratedResponse;
#else
    public abstract class OrchestratedResponse { }

    /// <summary>
    /// An activity (message) from the copilot.
    /// </summary>
    public class OrchestratedActivityResponse : OrchestratedResponse
    {
        /// <summary>
        /// The activity received from the agent.
        /// </summary>
        public Activity Activity { get; }

        /// <summary>
        /// Creates an <see cref="Microsoft.Agents.CopilotStudio.Client.Models.OrchestratedActivityResponse"/> with the specified activity.
        /// </summary>
        /// <param name="activity">The activity received from the agent.</param>
        public OrchestratedActivityResponse(Activity activity)
        {
            Activity = activity;
        }
    }

    /// <summary>
    /// Agent state information returned after a turn completes.
    /// </summary>
    public class OrchestratedStateResponse : OrchestratedResponse
    {
        /// <summary>
        /// The agent state payload containing status and metadata.
        /// </summary>
        public AgentStatePayload AgentState { get; }

        /// <summary>
        /// Creates an <see cref="Microsoft.Agents.CopilotStudio.Client.Models.OrchestratedStateResponse"/> with the specified agent state.
        /// </summary>
        /// <param name="agentState">The agent state payload.</param>
        public OrchestratedStateResponse(AgentStatePayload agentState)
        {
            AgentState = agentState;
        }
    }

    /// <summary>
    /// An error returned during an orchestrated turn.
    /// </summary>
    public class OrchestratedErrorResponse : OrchestratedResponse
    {
        /// <summary>
        /// The error payload containing code and message.
        /// </summary>
        public OrchestratedErrorPayload Error { get; }

        /// <summary>
        /// Creates an <see cref="Microsoft.Agents.CopilotStudio.Client.Models.OrchestratedErrorResponse"/> with the specified error.
        /// </summary>
        /// <param name="error">The error payload.</param>
        public OrchestratedErrorResponse(OrchestratedErrorPayload error)
        {
            Error = error;
        }
    }

    /// <summary>
    /// Signals that the orchestrated turn has completed and the SSE stream has ended.
    /// </summary>
    public class OrchestratedEndResponse : OrchestratedResponse
    {
        /// <summary>
        /// The raw data payload from the end event, if any.
        /// </summary>
        public string? Data { get; }

        public OrchestratedEndResponse(string? data = null)
        {
            Data = data;
        }
    }
#endif
}
