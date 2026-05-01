// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Connector;
using Microsoft.Agents.Connector.RestClients;
using Microsoft.Agents.Connector.Types;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Core.Connector.Tests
{
    public class AttachmentsRestClientTests
    {
        private const string AttachmentId = "test-attachment";
        private const string ViewId = "test-view";
        private readonly Uri UriEndpoint = new Uri("http://localhost");
        private readonly Mock<HttpClient> MockHttpClient = new();

        private static readonly Error NotFoundError = new Error
        {
            Code = "404",
            Message = "Not Found",
            InnerHttpError = new InnerHttpError { StatusCode = 404 }
        };
        private readonly HttpResponseMessage NotFoundResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.NotFound,
            Content = new StringContent(JsonSerializer.Serialize(new ErrorResponse(NotFoundError)))
        };
        private readonly HttpResponseMessage InternalErrorResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.InternalServerError,
            Content = new StringContent(JsonSerializer.Serialize("Internal Error"))
        };

        [Fact]
        public void Constructor_ShouldInstantiateCorrectly()
        {
            Assert.NotNull(UseAttachment());
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullTransport()
        {
            Assert.Throws<ArgumentNullException>(() => new AttachmentsRestClient(null));
        }

        [Fact]
        public async Task GetAttachmentInfoAsync_ShouldThrowOnNullAttachmentId()
        {
            var attachmentClient = UseAttachment();

            await Assert.ThrowsAsync<ArgumentNullException>(() => attachmentClient.GetAttachmentInfoAsync(null, CancellationToken.None));
        }

        [Fact]
        public async Task GetAttachmentInfoAsync_ShouldReturnInfo()
        {
            var attachmentClient = UseAttachment();

            var attachmentInfo = new AttachmentInfo
            {
                Name = "test-attachment-1",
                Type = "image/jpg",
                Views = new List<AttachmentView>()
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(attachmentInfo))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

            var result = await attachmentClient.GetAttachmentInfoAsync(AttachmentId, CancellationToken.None);
            
            Assert.Equal(attachmentInfo.Name, result.Name);
            Assert.Equal(attachmentInfo.Type, result.Type);
        }

        [Fact]
        public async Task GetAttachmentInfoAsync_ShouldThrowWithErrorBody()
        {
            var attachmentClient = UseAttachment();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(NotFoundResponse);

            try
            {
                _ = await attachmentClient.GetAttachmentInfoAsync(AttachmentId, CancellationToken.None);
            }
            catch (ErrorResponseException ex)
            {
                Assert.Equal(NotFoundError.Code, ex.Body.Error.Code);
                Assert.Equal(NotFoundError.Message, ex.Body.Error.Message);
                Assert.Equal(NotFoundError.InnerHttpError.StatusCode, ex.Body.Error.InnerHttpError.StatusCode);
            }
        }

        [Fact]
        public async Task GetAttachmentInfoAsync_ShouldThrowWithoutErrorBody()
        {
            var attachmentClient = UseAttachment();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(InternalErrorResponse);
            
            var exMessage = $"GetAttachmentInfo operation returned an invalid status code '(500) {InternalErrorResponse.StatusCode}'";

            try
            {
                _ = await attachmentClient.GetAttachmentInfoAsync(AttachmentId, CancellationToken.None);
            }
            catch (ErrorResponseException ex)
            {
                Assert.Null(ex.Body);
                Assert.Equal(exMessage, ex.Message);
            }
        }

        [Fact]
        public async Task GetAttachmentAsync_ShouldThrowOnNullAttachmentId()
        {
            var attachmentClient = UseAttachment();

            await Assert.ThrowsAsync<ArgumentNullException>(() => attachmentClient.GetAttachmentAsync(null, ViewId, CancellationToken.None));
        }

        [Fact]
        public async Task GetAttachmentAsync_ShouldThrowOnNullViewId()
        {
            var attachmentClient = UseAttachment();

            await Assert.ThrowsAsync<ArgumentNullException>(() => attachmentClient.GetAttachmentAsync(AttachmentId, null, CancellationToken.None));
        }

        [Fact]
        public async Task GetAttachmentAsync_ShouldReturnAttachment()
        {
            var attachmentClient = UseAttachment();

            var content = "This is a test content";
            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(content));

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(memoryStream)
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

            var result = await attachmentClient.GetAttachmentAsync(AttachmentId, ViewId, CancellationToken.None);

            using (var reader = new StreamReader(result, Encoding.UTF8))
            {
                var resultContent = await reader.ReadToEndAsync();

                Assert.Equal(content, resultContent);
            }
        }

        [Fact]
        public async Task GetAttachmentAsync_ShouldReturnNullOnRedirected()
        {
            var attachmentClient = UseAttachment();

            var response = new HttpResponseMessage(HttpStatusCode.MovedPermanently);

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

            Assert.Null(await attachmentClient.GetAttachmentAsync(AttachmentId, ViewId, CancellationToken.None));
        }

        [Fact]
        public async Task GetAttachmentAsync_ShouldThrowWithErrorBody()
        {
            var attachmentClient = UseAttachment();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(NotFoundResponse);

            try
            {
                _ = await attachmentClient.GetAttachmentAsync(AttachmentId, ViewId, CancellationToken.None);
            }
            catch (ErrorResponseException ex)
            {
                Assert.Equal(NotFoundError.Code, ex.Body.Error.Code);
                Assert.Equal(NotFoundError.Message, ex.Body.Error.Message);
                Assert.Equal(NotFoundError.InnerHttpError.StatusCode, ex.Body.Error.InnerHttpError.StatusCode);
            }
        }

        [Fact]
        public async Task GetAttachmentAsync_ShouldThrowWithoutErrorBody()
        {
            var attachmentClient = UseAttachment();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(InternalErrorResponse);

            var exMessage = $"GetAttachment operation returned an invalid status code '(500) {InternalErrorResponse.StatusCode}'";

            try
            {
                _ = await attachmentClient.GetAttachmentAsync(AttachmentId, ViewId, CancellationToken.None);
            }
            catch (ErrorResponseException ex)
            {
                Assert.Null(ex.Body);
                Assert.Equal(exMessage, ex.Message);
            }
        }

        private AttachmentsRestClient UseAttachment()
        {
            var transport = new Mock<IRestTransport>();
            transport.Setup(x => x.Endpoint)
                .Returns(UriEndpoint);
            transport.Setup(a => a.GetHttpClientAsync())
                .Returns(Task.FromResult(MockHttpClient.Object));

            return new AttachmentsRestClient(transport.Object);
        }
    }
}
