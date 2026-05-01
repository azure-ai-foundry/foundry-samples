// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Builder;

namespace DialogRootBot;

// This Controller exposes the HTTP endpoints for the Connector that DialogSkillBot uses to
// communicate (send replies) with DialogRootBot.
[AllowAnonymous]
[ApiController]
[Route("api/agentresponse")]
public class AgentResponseController(IChannelApiHandler handler) : ChannelApiController(handler)
{
}
