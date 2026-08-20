/*
 * Copyright (c) 2026, Sjofn LLC
 * All rights reserved.
 *
 * - Redistribution and use in source and binary forms, with or without
 *   modification, are permitted provided that the following conditions are met:
 *
 * - Redistributions of source code must retain the above copyright notice, this
 *   list of conditions and the following disclaimer.
 * - Neither the name of the openmetaverse.co nor the names
 *   of its contributors may be used to endorse or promote products derived from
 *   this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 */

using System.Net;
using System.Reflection;
using LibreMetaverse.Packets;
using LibreMetaverse.Tests.TestHelpers;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    [Category("Inventory")]
    public class RemoveInventoryItemHandlerTests
    {
        private static FakeGridClient CreateClientWithStore(out UUID itemId)
        {
            var client = new FakeGridClient();
            var storeField = typeof(InventoryManager).GetField("_Store",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var store = new Inventory(client, client.Self.AgentID);
            storeField!.SetValue(client.Inventory, store);

            itemId = UUID.Random();
            store[itemId] = new InventoryItem(itemId) { Name = "Doomed Item", ParentUUID = UUID.Random() };

            return client;
        }

        [Test]
        public void RemoveInventoryItem_RemovesItemFromStore_AndRaisesEvent()
        {
            var client = CreateClientWithStore(out var itemId);
            var sim = new Simulator(client, new IPEndPoint(IPAddress.Loopback, 13), 0);

            InventoryObjectRemovedEventArgs? received = null;
            client.Inventory.Store!.InventoryObjectRemoved += (_, e) => received = e;

            var packet = new RemoveInventoryItemPacket
            {
                AgentData = new RemoveInventoryItemPacket.AgentDataBlock
                {
                    AgentID = client.Self.AgentID, SessionID = client.Self.SessionID
                },
                InventoryData = new[]
                {
                    new RemoveInventoryItemPacket.InventoryDataBlock { ItemID = itemId }
                }
            };

            var handler = typeof(InventoryManager).GetMethod("RemoveInventoryItemHandler",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(handler, Is.Not.Null);
            handler!.Invoke(client.Inventory, new object?[] { null, new PacketReceivedEventArgs(packet, sim) });

            Assert.That(client.Inventory.Store!.Contains(itemId), Is.False);
            Assert.That(received, Is.Not.Null);
            Assert.That(received!.Obj.UUID, Is.EqualTo(itemId));
        }

        [Test]
        public void RemoveInventoryItem_UnknownItem_DoesNotThrow()
        {
            var client = CreateClientWithStore(out _);
            var sim = new Simulator(client, new IPEndPoint(IPAddress.Loopback, 13), 0);

            var packet = new RemoveInventoryItemPacket
            {
                AgentData = new RemoveInventoryItemPacket.AgentDataBlock
                {
                    AgentID = client.Self.AgentID, SessionID = client.Self.SessionID
                },
                InventoryData = new[]
                {
                    new RemoveInventoryItemPacket.InventoryDataBlock { ItemID = UUID.Random() }
                }
            };

            var handler = typeof(InventoryManager).GetMethod("RemoveInventoryItemHandler",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(handler, Is.Not.Null);

            Assert.DoesNotThrow(() =>
                handler!.Invoke(client.Inventory, new object?[] { null, new PacketReceivedEventArgs(packet, sim) }));
        }
    }
}
