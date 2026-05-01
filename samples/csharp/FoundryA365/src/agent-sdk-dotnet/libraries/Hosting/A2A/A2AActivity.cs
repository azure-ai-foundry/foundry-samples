// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Hosting.A2A.JsonRpc;
using Microsoft.Agents.Hosting.A2A.Protocol;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.Agents.Hosting.A2A;

/// <summary>
/// Concerning A2A to/from Activity
/// </summary>
internal static class A2AActivity
{
    public const string DefaultUserId = "unknown";

    private const string EntityTypeTemplate = "application/vnd.microsoft.entity.{0}";
    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, object>> _schemas = [];

    public static (IActivity, string? contextId, string? taskId, Message? message) ActivityFromRequest(JsonRpcRequest jsonRpcRequest, MessageSendParams sendParams = null, bool isStreaming = true)
    {
        if (jsonRpcRequest.Params == null)
        {
            throw new A2AException("Params is null", A2AErrors.InvalidParams).WithRequestId(jsonRpcRequest.Id);
        }

        sendParams ??= A2AModel.MessageSendParamsFromRequest(jsonRpcRequest);
        if (sendParams?.Message?.Parts == null)
        {
            throw new A2AException("Invalid MessageSendParams", A2AErrors.InvalidParams).WithRequestId(jsonRpcRequest.Id);
        }

        var contextId = sendParams.Message.ContextId ?? Guid.NewGuid().ToString("N");

        // taskId is our conversationId
        var taskId = sendParams.Message.TaskId ?? Guid.NewGuid().ToString("N");
        
        var activity = CreateActivity(taskId, sendParams.Message.Parts, true, isStreaming);
        activity.RequestId = jsonRpcRequest.Id.ToString();

        sendParams.Message.ContextId = contextId;
        sendParams.Message.TaskId = taskId;

        return (activity, contextId, taskId, sendParams.Message);
    }

    public static TaskStatusUpdateEvent CreateStatusUpdate(string contextId, string taskId, TaskState taskState, string artifactId = null, bool isFinal = false, IActivity activity = null)
    {
        var artifact = CreateArtifact(activity, artifactId);

        return new TaskStatusUpdateEvent()
        {
            TaskId = taskId,
            ContextId = contextId,
            Status = new TaskStatus()
            {
                State = taskState,
                Timestamp = DateTimeOffset.UtcNow,
                Message = artifact == null ? null : new Message() { MessageId = Guid.NewGuid().ToString("N"), Parts = artifact.Parts, Role = MessageRole.Agent },
            },
            Final = isFinal
        };
    }

    public static TaskStatusUpdateEvent CreateStatusUpdate(string contextId, string taskId, TaskStatus status, bool isFinal = false)
    {
        return new TaskStatusUpdateEvent()
        {
            TaskId = taskId,
            ContextId = contextId,
            Status = status,
            Final = isFinal
        };
    }

    public static TaskArtifactUpdateEvent CreateArtifactUpdate(string contextId, string taskId, IActivity activity, string artifactId = null, bool append = false, bool lastChunk = false)
    {
        var artifact = CreateArtifact(activity, artifactId) ?? throw new ArgumentException("Invalid activity to convert to payload");

        return new TaskArtifactUpdateEvent()
        {
            TaskId = taskId,
            ContextId = contextId,
            Artifact = artifact,
            Append = append,
            LastChunk = lastChunk
        };
    }

    public static Message CreateMessage(string contextId, string taskId, IActivity activity, bool includeEntities = true)
    {
        var artifact = CreateArtifact(activity, includeEntities: includeEntities) ?? throw new ArgumentException("Invalid activity to convert to payload");

        return new Message()
        {
            TaskId = taskId,
            ContextId = contextId,
            MessageId = Guid.NewGuid().ToString("N"),
            Parts = artifact.Parts,
            Role = MessageRole.Agent
        };
    }

    public static AgentTask CreateTask(string contextId, string taskId, TaskState taskState, IActivity activity)
    {
        var artifact = CreateArtifact(activity);
        return A2AModel.TaskForState(contextId, taskId, taskState, artifact);
    }

    public static Artifact? CreateArtifact(IActivity activity, string artifactId = null, bool includeEntities = true)
    {
        if (activity == null)
        {
            return null;
        }

        var artifact = new Artifact()
        {
            ArtifactId = artifactId ?? Guid.NewGuid().ToString("N")
        };

        if (activity?.Text != null)
        {
            artifact.Parts = artifact.Parts.Add(new TextPart()
            {
                Text = activity.Text
            });
        }

        if (activity?.Value != null)
        {
            artifact.Parts = artifact.Parts.Add(new DataPart()
            {
                Data = activity.Value.ToJsonElements()
            });
        }

        foreach (var attachment in activity?.Attachments ?? Enumerable.Empty<Attachment>())
        {
            if (attachment.ContentUrl == null && attachment.Content is not string)
            {
                continue;
            }

            artifact.Parts = artifact.Parts.Add(new FilePart()
            {
                Uri = attachment.ContentUrl,
                Bytes = attachment.Content as string,
                MimeType = attachment.ContentType,
                Name = attachment.Name,

            });
        }

        if (includeEntities)
        {
            foreach (var entity in activity?.Entities ?? Enumerable.Empty<Entity>())
            {
                if (entity is not StreamInfo)
                {
                    if (!_schemas.TryGetValue(entity.GetType(), out var cachedMetadata))
                    {
                        cachedMetadata = entity.ToA2AMetadata(string.Format(EntityTypeTemplate, entity.Type));
                        _schemas.TryAdd(entity.GetType(), cachedMetadata);
                    }

                    artifact.Parts = artifact.Parts.Add(new DataPart
                    {
                        Metadata = cachedMetadata,
                        Data = entity.ToJsonElements()
                    });
                }
            }
        }

        return artifact;
    }

    public static Artifact? CreateArtifactFromObject(object data, string name = null, string description = null, string mediaType = null, string artifactId = null)
    {
        if (data == null)
        {
            return null;
        }

        return new Artifact()
        {
            ArtifactId = artifactId ?? Guid.NewGuid().ToString("N"),
            Name = name,
            Description = description,
            Parts = [new DataPart()
            {
                Data = data,
                Metadata = data.ToA2AMetadata(mediaType ?? data.GetType().Name)
            }]
        };
    }

    public static bool HasA2AMessageContent(this IActivity activity)
    {
        return !string.IsNullOrEmpty(activity.Text)
            || (bool) activity.Attachments?.Any();
    }

    public static TaskState GetA2ATaskState(this IActivity activity)
    {
        TaskState taskState = activity.InputHint switch
        {
            InputHints.ExpectingInput => TaskState.InputRequired,
            InputHints.AcceptingInput => TaskState.Working,
            _ => TaskState.Working,
        };

        return taskState;
    }

    private static Activity CreateActivity(
        string conversationId,
        ImmutableArray<Part> parts,
        bool isIngress,
        bool isStreaming = true)
    {
        var bot = new ChannelAccount
        {
            Id = "assistant",
            Role = RoleTypes.Agent,
        };

        var user = new ChannelAccount
        {
            Id = DefaultUserId,
            Role = RoleTypes.User,
        };

        var activity = new Activity()
        {
            Type = ActivityTypes.Message,
            Id = Guid.NewGuid().ToString("N"),
            ChannelId = Channels.A2A,
            DeliveryMode = isStreaming ? DeliveryModes.Stream : DeliveryModes.ExpectReplies,
            Conversation = new ConversationAccount
            {
                Id = conversationId,
            },
            Recipient = isIngress ? bot : user,
            From = isIngress ? user : bot
        };

        foreach (var part in parts)
        {
            if (part is TextPart tp)
            {
                if (activity.Text == null)
                {
                    activity.Text = tp.Text;
                }
                else
                {
                    activity.Text += tp.Text;
                }
            }
            else if (part is FilePart filePart)
            {
                activity.Attachments.Add(new Attachment()
                {
                    ContentType = filePart.MimeType,
                    Name = filePart.Name,
                    ContentUrl = filePart.Uri,
                    Content = filePart.Bytes,
                });
            }
            else if (part is DataPart dataPart)
            {
                activity.Attachments.Add(new Attachment()
                {
                    ContentType = "application/json",
                    Name = "A2A DataPart",
                    Content = ProtocolJsonSerializer.ToJson(dataPart),
                });
            }
        }

        return activity;
    }
}