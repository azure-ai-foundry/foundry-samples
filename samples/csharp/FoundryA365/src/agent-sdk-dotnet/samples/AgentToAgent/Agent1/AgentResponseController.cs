// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Builder;

namespace Agent1;

// This Controller exposes the HTTP endpoints for the Connector that Agent2 uses to
// communicate (send replies) with Agent1.
[AllowAnonymous]
[ApiController]
[Route("api/agentresponse")]
public class AgentResponseController(IChannelApiHandler handler) : ChannelApiController(handler)
{
}
