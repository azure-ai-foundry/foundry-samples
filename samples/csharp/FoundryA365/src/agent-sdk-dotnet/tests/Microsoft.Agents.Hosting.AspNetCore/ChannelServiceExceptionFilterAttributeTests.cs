// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Hosting.AspNetCore.Tests
{
    public class ChannelServiceExceptionFilterAttributeTests
    {
        public static TheoryData<Type, int> TestDataExceptions = new() {
            { typeof(NotImplementedException), StatusCodes.Status501NotImplemented },
            { typeof(UnauthorizedAccessException), StatusCodes.Status401Unauthorized },
            { typeof(KeyNotFoundException), StatusCodes.Status404NotFound },
            { typeof(ArgumentException), StatusCodes.Status500InternalServerError }
        };

        [Theory]
        [MemberData(nameof(TestDataExceptions))]
        public void OnException_ShouldSetStatusCodeResult(Type exception, int status)
        {
            var attribute = new ChannelServiceExceptionFilterAttribute();
            var httpContext = new DefaultHttpContext();
            var actionContext = new ActionContext
            {
                HttpContext = httpContext,
                RouteData = new RouteData(),
                ActionDescriptor = new ControllerActionDescriptor()
            };
            var context = new ExceptionContext(actionContext, [])
            {
                Exception = (Exception)Activator.CreateInstance(exception)
            };
            attribute.OnException(context);

            Assert.Equal(status, (context.Result as StatusCodeResult).StatusCode);
        }
    }
}