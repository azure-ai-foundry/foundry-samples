// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Storage.Tests
{
    public class MemoryStorageTests : StorageBaseTests, IDisposable
    {
        private IStorage storage;

        public MemoryStorageTests()
        {
            storage = new MemoryStorage();
        }

        public void Dispose()
        {
            storage = new MemoryStorage();
        }

        [Fact]
        public async Task MemoryStorage_ReadValidation()
        {
            await ReadValidation(storage);
        }

        [Fact]
        public async Task MemoryStorage_CreateObjectTest()
        {
            await CreateObjectTest(storage);
        }

        [Fact]
        public async Task MemoryStorage_ReadUnknownTest()
        {
            await ReadUnknownTest(storage);
        }

        [Fact]
        public async Task MemoryStorage_UpdateObjectTest()
        {
            await UpdateObjectTest<Exception>(storage);
        }

        [Fact]
        public async Task MemoryStorage_DeleteObjectTest()
        {
            await DeleteObjectTest(storage);
        }

        [Fact]
        public async Task MemoryStorage_HandleCrazyKeys()
        {
            await HandleCrazyKeys(storage);
        }

        [Fact]
        public async Task Nested()
        {
            var storage = new MemoryStorage();

            var outer = new Outer()
            {
                State = new Dictionary<string, object> 
                { 
                    ["key1"] = new Inner() { Name = "inner" } 
                }
            };

            var changes = new Dictionary<string, object>
                {
                    { "change1", outer.State },
                };

            await storage.WriteAsync(changes, default);
            var items = await storage.ReadAsync(new[] { "change1" }, default);

            Assert.NotEmpty(items);
        }

        [Fact]
        public async Task WriteAsync_ShouldThrowOnNullStoreItemChanges()
        {
            var storage = new MemoryStorage();

            await Assert.ThrowsAsync<ArgumentNullException>(() => storage.WriteAsync<StoreItem>(null, CancellationToken.None));
        }

        [Fact]
        public async Task ReadAsync_ShouldReturnWrittenStoreItem()
        {
            var storeItem = new StoreItem
            {
                Id = 1,
                Topic = "topic",
                ETag = "test",
            };

            var key = "key1";

            var changes = new Dictionary<string, StoreItem>
            {
                { key, storeItem }
            };

            var storage = new MemoryStorage();

            await storage.WriteAsync(changes, CancellationToken.None);

            var readStoreItems = new Dictionary<string, StoreItem>(await storage.ReadAsync<StoreItem>([key], CancellationToken.None));

            Assert.NotNull(readStoreItems);
            Assert.Single(readStoreItems);
            Assert.Equal(storeItem.Id, readStoreItems[key].Id);
            Assert.Equal(storeItem.Topic, readStoreItems[key].Topic);
            Assert.NotNull(readStoreItems[key].ETag);
        }
    }

    class Inner
    {
        public string Name { get; set; }
    }

    class Outer
    {
        public IDictionary<string, object> State { get; set; }
    }
}
