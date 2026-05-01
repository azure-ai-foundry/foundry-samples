// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Hosting.A2A.Protocol;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.A2A;

internal interface ITaskStore
{
    Task<CreateOrContinueResult> CreateOrContinueTaskAsync(string contextId, string taskId, TaskState state = TaskState.Working, Message message = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="taskId"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="KeyNotFoundException">Thrown when the task with the specified ID does not exist.</exception>"
    Task<AgentTask> GetTaskAsync(string taskId, CancellationToken cancellationToken = default);

    Task<AgentTask> UpdateTaskAsync(AgentTask task, CancellationToken cancellationToken = default);

    Task<AgentTask> UpdateArtifactAsync(TaskArtifactUpdateEvent artifactUpdate, CancellationToken cancellationToken = default);

    Task<AgentTask> UpdateStatusAsync(TaskStatusUpdateEvent statusUpdate, CancellationToken cancellationToken = default);

    Task<AgentTask> UpdateMessageAsync(Message message, CancellationToken cancellationToken = default);
}

internal class CreateOrContinueResult
{
    public bool IsNewTask { get; set; }

    public AgentTask Task { get; set; }
}
