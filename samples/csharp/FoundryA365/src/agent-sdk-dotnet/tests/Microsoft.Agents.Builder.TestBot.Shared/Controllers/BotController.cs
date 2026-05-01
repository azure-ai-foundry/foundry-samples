// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Builder.TestBot.Shared.Controllers
{
    // This ASP Controller is created to handle a request. Dependency Injection will provide the Adapter and IBot
    // implementation at runtime. Multiple different IBot implementations running at different endpoints can be
    // achieved by specifying a more specific type for the bot constructor argument.
    [Route("api")]
    [ApiController]
    public class BotController : ControllerBase
    {
        private readonly IAgentHttpAdapter _adapter;
        private IAgent _bot;
        private readonly Func<string, IAgent> _service;

        public BotController(IAgentHttpAdapter adapter, Func<string, IAgent> service)
        {
            _adapter = adapter;
            _service = service;
        }

        [Route("{*botname}")]
        [HttpPost]
        [HttpGet]
        public async Task PostAsync(string botname)
        {
            // Delegate the processing of the HTTP POST to the adapter.
            // The adapter will invoke the bot.
            _bot = _service(botname) ?? throw new Exception($"The endpoint '{botname}' is not associated with a bot.");

            await _adapter.ProcessAsync(Request, Response, _bot);
        }
    }
}
