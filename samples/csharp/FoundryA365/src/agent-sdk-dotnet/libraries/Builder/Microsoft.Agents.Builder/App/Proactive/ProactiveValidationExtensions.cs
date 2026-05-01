// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Core.Models;
using System;

namespace Microsoft.Agents.Builder.App.Proactive
{
    public static class ProactiveValidationExtensions
    {
        /// <summary>
        /// Validates a CreateConversation instance to ensure all required properties are valid.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when any required property is missing or invalid.</exception>
        public static void Validate(this CreateConversationOptions createConversation)
        {
            if (string.IsNullOrWhiteSpace(createConversation.ChannelId))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ProactiveInvalidCreateConversationInstance, null, nameof(CreateConversationOptions.ChannelId));
            }

            if (string.IsNullOrWhiteSpace(createConversation.ServiceUrl))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ProactiveInvalidCreateConversationInstance, null, nameof(CreateConversationOptions.ServiceUrl));
            }

            if (string.IsNullOrWhiteSpace(createConversation.Scope))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ProactiveInvalidCreateConversationInstance, null, nameof(CreateConversationOptions.Scope));
            }

            if (createConversation.Parameters == null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ProactiveInvalidCreateConversationInstance, null, nameof(CreateConversationOptions.Parameters));
            }
            createConversation.Parameters.Validate();
        }

        /// <summary>
        /// Validates a Conversation instance to ensure all required properties are valid.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when any required property is missing or invalid.</exception>
        public static void Validate(this Conversation conversation, bool validateConversation = true)
        {
            if (conversation.Reference == null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ProactiveInvalidConversationInstance, null, nameof(Conversation.Reference));
            }
            conversation.Reference.Validate(validateConversation);

            if (conversation.Claims == null || !conversation.Claims.TryGetValue("aud", out _))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ProactiveInvalidConversationInstance, null, "aud claim is required in Claims");
            }
        }

        /// <summary>
        /// Validates a ConversationParameters instance to ensure all required properties are valid.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when any required property is missing or invalid.</exception>
        public static void Validate(this ConversationParameters parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters.Agent?.Id))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ProactiveInvalidConversationParametersInstance, null, $"{nameof(ConversationParameters.Agent)}.{nameof(ConversationParameters.Agent.Id)}");
            }
        }

        /// <summary>
        /// Validates a ConversationReference instance to ensure all required properties are valid.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when any required property is missing or invalid.</exception>
        public static void Validate(this ConversationReference reference, bool validateConversation = true)
        {
            if (string.IsNullOrWhiteSpace(reference.ChannelId))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ProactiveInvalidConversationReferenceInstance, null, nameof(ConversationReference.ChannelId));
            }
            if (string.IsNullOrWhiteSpace(reference.Agent?.Id))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ProactiveInvalidConversationReferenceInstance, null, $"{nameof(ConversationReference.Agent)}.{nameof(ConversationReference.Agent.Id)}");
            }
            if (string.IsNullOrWhiteSpace(reference.User?.Id))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ProactiveInvalidConversationReferenceInstance, null, $"{nameof(ConversationReference.User)}.{nameof(ConversationReference.User.Id)}");
            }
            if (string.IsNullOrWhiteSpace(reference.ServiceUrl))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ProactiveInvalidConversationReferenceInstance, null, nameof(ConversationReference.ServiceUrl));
            }

            if (validateConversation)
            {
                if (reference.Conversation == null)
                {
                    throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ProactiveInvalidConversationReferenceInstance, null, nameof(ConversationReference.Conversation));
                }
                if (string.IsNullOrWhiteSpace(reference.Conversation.Id))
                {
                    throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ProactiveInvalidConversationReferenceInstance, null, $"{nameof(ConversationReference.Conversation)}.{nameof(ConversationReference.Conversation.Id)}");
                }
            }
        }
    }
}
