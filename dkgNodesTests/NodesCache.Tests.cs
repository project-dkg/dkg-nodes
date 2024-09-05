// Copyright (C) 2024 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of dkg service node
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions
// are met:
// 1. Redistributions of source code must retain the above copyright
// notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
// TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR CONTRIBUTORS
// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.
using dkgServiceNode.Models;
using dkgServiceNode.Services.Cache;

namespace dkgNodesTests
{
    [TestFixture]
    public class NodeCacheTests
    {
        private readonly NodesCache nodesCache = new();
        [SetUp]
        public void SetUp()
        {
            for (int i = 1; i <= 2; i++)
            {
                var node = nodesCache.GetNodeById(i);
                if (node != null)
                {
                    nodesCache.DeleteNodeFromCache(node);
                }
            }
        }

        [Test]
        public void LoadNodesToCache_ShouldLoadNodes()
        {
            
            var nodes = new List<Node>
            {
                new Node { Id = 1, Name = "Node1" },
                new Node { Id = 2, Name = "Node2" }
            };

            
            nodesCache.LoadNodesToCache(nodes);

          
            nodes = nodesCache.GetAllNodes();
            Assert.Multiple(() =>
            {
                Assert.That(nodes.Count, Is.EqualTo(2));
                Assert.That(nodes.First(n => n.Id == 1).Name, Is.EqualTo("Node1"));
                Assert.That(nodes.First(n => n.Id == 2).Name, Is.EqualTo("Node2"));
            });
        }

        [Test]
        public void GetNodeById_ShouldReturnCorrectNode()
        {
            
            var node = new Node { Id = 1, Name = "Node1" };
            nodesCache.LoadNodeToCache(node);

            
            var result = nodesCache.GetNodeById(1);

          
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Name, Is.EqualTo("Node1"));
        }

        [Test]
        public void AddNodeToCache_ShouldAddNode()
        {
            
            var node = new Node { Id = 1, Name = "Node1" };

            
            nodesCache.LoadNodeToCache(node);

          
            var result = nodesCache.GetNodeById(1);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Name, Is.EqualTo("Node1"));
        }

        [Test]
        public void UpdateNodeInCache_ShouldUpdateNode()
        {
            
            var node = new Node { Id = 1, Name = "Node1" };
            nodesCache.LoadNodeToCache(node);

            
            node.Name = "UpdatedNode1";
            nodesCache.UpdateNodeInCache(node);

          
            var result = nodesCache.GetNodeById(1);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Name, Is.EqualTo("UpdatedNode1"));
        }

        [Test]
        public void DeleteNodeFromCache_ShouldRemoveNode()
        {
            
            var node = new Node { Id = 1, Name = "Node1" };
            nodesCache.LoadNodeToCache(node);

            
            node = nodesCache.GetNodeById(1);
            Assert.That(node, Is.Not.Null);
            nodesCache.DeleteNodeFromCache(node);

          
            var result = nodesCache.GetNodeById(1);
            Assert.That(result, Is.Null);
        }

        [Test]
        public void NodeExists_ShouldReturnTrueIfExists()
        {
            
            var node = new Node { Id = 1, Name = "Node1" };
            nodesCache.LoadNodeToCache(node);

            
            var exists = nodesCache.GetNodeById(1) != null;

          
            Assert.That(exists, Is.True);
        }

        [Test]
        public void NodeExists_ShouldReturnFalseIfNotExists()
        {
            
            var exists = nodesCache.GetNodeById(1) != null;

          
            Assert.That(exists, Is.False);
        }
    }

}
