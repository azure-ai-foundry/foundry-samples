// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Public Extensions for <see cref="Microsoft.Agents.Core.Models.IActivity"/>. type
    /// </summary>
    public static class IActivityExtensions
    {
        /// <summary>
        /// Converts an <see cref="Microsoft.Agents.Core.Models.IActivity"/> to a JSON string.
        /// </summary>
        /// <param name="activity">Activity to convert to Json Payload</param>
        /// <returns>JSON String</returns>
        public static string ToJson(this IActivity activity)
        {
            return JsonSerializer.Serialize(activity, ProtocolJsonSerializer.SerializationOptions);
        }

        /// <summary>
        /// Resolves the mentions from the entities of this activity.
        /// </summary>
        /// <returns>The array of mentions; or an empty array, if none are found.</returns>
        /// <remarks>This method is defined on the <see cref="Microsoft.Agents.Core.Models.Activity"/> class, but is only intended
        /// for use with a message activity, where the activity <see cref="Microsoft.Agents.Core.Models.Activity.Type"/> is set to
        /// <see cref="Microsoft.Agents.Core.Models.ActivityTypes.Message"/>.</remarks>
        /// <seealso cref="Microsoft.Agents.Core.Models.Mention"/>
        public static Mention[] GetMentions(this IActivity activity)
        {
            var result = new List<Mention>();
            if (activity.Entities != null)
            {
                foreach (var entity in activity.Entities)
                {
                    if (entity is Mention mention)
                    {
                        result.Add(mention);
                    }
                }
            }
            return [.. result];
        }

        /// <summary>
        /// Remove recipient mention text from Text property.
        /// Use with caution because this function is altering the text on the Activity.
        /// </summary>
        /// <returns>new .Text property value.</returns>
        public static string RemoveRecipientMention<T>(this T activity) where T : IActivity
        {
            return activity.RemoveMentionText(activity.Recipient?.Id);
        }

        /// <summary>
        /// Clone the activity to a new instance of activity. 
        /// </summary>
        /// <param name="activity"></param>
        /// <returns></returns>
        public static IActivity Clone(this IActivity activity)
        {
            return ProtocolJsonSerializer.CloneTo<IActivity>(activity);
        }

        /// <summary>
        /// Gets the channel data for this activity as a strongly-typed object.
        /// </summary>
        /// <typeparam name="T">The type of the object to return.</typeparam>
        /// <returns>The strongly-typed object; or the type's default value, if the ChannelData is null.</returns>
        public static T GetChannelData<T>(this IActivity activity)
        {
            if (activity.ChannelData == null)
            {
                return default;
            }

            if (activity.ChannelData.GetType() == typeof(T))
            {
                return (T)activity.ChannelData;
            }

            return ((JsonElement)activity.ChannelData).Deserialize<T>(ProtocolJsonSerializer.SerializationOptions);
        }

        /// <summary>
        /// Gets the channel data for this activity as a strongly-typed object.
        /// A return value indicates whether the operation succeeded.
        /// </summary>
        /// <typeparam name="T">The type of the object to return.</typeparam>
        /// <param name="instance">When this method returns, contains the strongly-typed object if the operation succeeded,
        /// or the type's default value if the operation failed.</param>
        /// <param name="activity"></param>
        /// <returns>
        /// <c>true</c> if the operation succeeded; otherwise, <c>false</c>.
        /// </returns>
        /// <seealso cref="Microsoft.Agents.Core.Models.IActivityExtensions.GetChannelData{T}(Microsoft.Agents.Core.Models.IActivity)"/>
        public static bool TryGetChannelData<T>(this IActivity activity, out T instance)
        {
            instance = default;

            try
            {
                if (activity.ChannelData == null)
                {
                    return false;
                }

                instance = activity.GetChannelData<T>();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Determines whether the specified activity represents a targeted activity treatment.
        /// </summary>
        /// <remarks>Use this method to identify activities that are specifically marked as targeted
        /// treatments. This can be useful for filtering or processing activities based on their treatment
        /// type.</remarks>
        /// <param name="activity">The activity to evaluate for targeted treatment. Cannot be null.</param>
        /// <returns>true if the activity contains an entity of type ActivityTreatment with a treatment of Targeted; otherwise,
        /// false.</returns>
        public static bool IsTargetedActivity(this IActivity activity)
        {
            if (activity.Entities == null || activity.Entities.Count == 0)
            {
                return false;
            }

            foreach (var entity in activity.Entities)
            {
                if (entity.Type == EntityTypes.ActivityTreatment && entity is ActivityTreatment treatment && treatment.Treatment == ActivityTreatmentTypes.Targeted)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Marks the specified activity as targeted by adding a targeted treatment entity to its collection.
        /// </summary>
        /// <remarks>This method ensures that the activity's Entities collection is initialized before
        /// adding the targeted treatment. Use this extension to indicate that an activity should be processed or
        /// interpreted as targeted in downstream workflows.</remarks>
        /// <param name="activity">The activity to be marked as targeted. Cannot be null.</param>
        public static void MakeTargetedActivity(this IActivity activity)
        {
            if (activity.IsTargetedActivity())
            {
                return;
            }
            activity.Entities ??= [];
            activity.Entities.Add(new ActivityTreatment() { Treatment = ActivityTreatmentTypes.Targeted });
        }
    }
}