// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Hosting.A2A.JsonRpc;
using Microsoft.Agents.Hosting.A2A.Protocol;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.A2A;

/// <summary>
/// Concerning A2A model helpers for extensions, creation, serialization.
/// </summary>
internal static class A2AModel
{
    private static readonly ConcurrentDictionary<string, JsonNode> _schemas = new();

    public static bool IsTerminal(this AgentTask task)
    {
        return task.Status.State == TaskState.Completed
            || task.Status.State == TaskState.Canceled
            || task.Status.State == TaskState.Rejected
            || task.Status.State == TaskState.Failed;
    }

    public static AgentTask WithHistoryTrimmedTo(this AgentTask task, int? toLength)
    {
        if (!toLength.HasValue || toLength.Value < 0 || task.History.Value.Length <= 0 || task.History.Value.Length <= toLength.Value)
        {
            return task;
        }

        return new AgentTask
        {
            Id = task.Id,
            ContextId = task.ContextId,
            Status = task.Status,
            Artifacts = task.Artifacts,
            Metadata = task.Metadata,
            History = [.. task.History.Value.Skip(task.History.Value.Length - toLength.Value)],
        };
    }

    public static IReadOnlyDictionary<string, object> ToA2AMetadata(this object data, string contentType)
    {
        if (!_schemas.TryGetValue(data.GetType().FullName, out JsonNode schema))
        {
            JsonSchemaExporterOptions exporterOptions = new()
            {
                TreatNullObliviousAsNonNullable = true,
            };

            schema = A2AJsonUtilities.DefaultReflectionOptions.GetJsonSchemaAsNode(data.GetType(), exporterOptions);
            _schemas[data.GetType().FullName] = schema;
        }

        return new Dictionary<string, object>
        {
            { "mimeType", contentType},
            { "type", "object" },
            {
                "schema", schema
            }
        };
    }

    public static AgentTask TaskForState(string contextId, string taskId, TaskState taskState, Artifact artifact = null)
    {
        return new AgentTask()
        {
            Id = taskId,
            ContextId = contextId,
            Status = new Protocol.TaskStatus()
            {
                State = taskState,
                Timestamp = DateTimeOffset.UtcNow,
                Message = artifact == null
                    ? null
                    : new Message()
                    {
                        TaskId = taskId,
                        ContextId = contextId,
                        MessageId = Guid.NewGuid().ToString("N"),
                        Parts = artifact.Parts,
                        Role = MessageRole.Agent
                    }
            },
        };
    }

    #region From Request
    public static async Task<T?> ReadRequestAsync<T>(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            return await JsonSerializer.DeserializeAsync<T>(request.Body);
        }
        catch(A2AException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new A2AException(ex.Message, ex, A2AErrors.InvalidRequest);
        }
    }

    public static MessageSendParams MessageSendParamsFromRequest(JsonRpcRequest jsonRpcRequest)
    {
        MessageSendParams sendParams;

        try
        {
            sendParams = jsonRpcRequest.Params?.Deserialize<MessageSendParams>();
        }
        catch(A2AException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new A2AException(ex.Message, A2AErrors.InvalidParams).WithRequestId(jsonRpcRequest.Id);
        }

        if (sendParams?.Message?.Parts == null || sendParams.Message.Parts.Length == 0)
        {
            throw new A2AException("MessageSendParams.Parts missing or empty", A2AErrors.InvalidParams).WithRequestId(jsonRpcRequest.Id);
        }

        return sendParams;
    }

    public static T ReadParams<T>(JsonRpcRequest jsonRpcRequest)
    {
        if (jsonRpcRequest.Params == null)
        {
            throw new ArgumentException("Params is null");
        }
        return jsonRpcRequest.Params.Value.Deserialize<T>();
    }
    #endregion

    #region To Response
    public static SendStreamingMessageResponse StreamingMessageResponse(JsonRpcId requestId, object payload)
    {
        return new SendStreamingMessageResponse()
        {
            Id = requestId,
            Result = payload
        };
    }
    #endregion
}