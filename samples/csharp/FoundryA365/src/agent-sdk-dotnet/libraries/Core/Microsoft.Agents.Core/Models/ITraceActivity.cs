// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Represents a point in an Agent's logic, to help with Agent debugging.
    /// </summary>
    /// <remarks>
    /// The trace activity typically is logged by transcript history components to become part of a
    /// transcript history. In remote debugging scenarios the trace activity can be sent to the client
    /// so that the activity can be inspected as part of the debug flow.
    ///
    /// Trace activities are normally not shown to the user, and are internal to transcript logging
    /// and developer debugging.
    ///
    /// See also InspectionMiddleware.
    /// </remarks>
    public interface ITraceActivity : IActivity
    {
    }
}
