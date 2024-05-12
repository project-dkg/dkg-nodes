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

using dkgCommon.Constants;
namespace dkgNodesTests
{

    [TestFixture]
    public class NodeStatusTests
    {
        [Test]
        public void TestImplicitConversionToNStatus()
        {
            NodeStatus nodeStatus = NodeStatusConstants.RunningStepThree;
            NStatus status = nodeStatus;
            Assert.That(status, Is.EqualTo(NStatus.RunningStepThree));
        }

        [Test]
        public void TestImplicitConversionFromNStatus()
        {
            NStatus status = NStatus.WaitingStepThree;
            NodeStatus nodeStatus = status;
            Assert.That(nodeStatus, Is.EqualTo(NodeStatusConstants.WaitingStepThree));
        }

        [Test]
        public void TestIsRunning()
        {
            NodeStatus nodeStatus = NodeStatusConstants.RunningStepOne;
            Assert.That(nodeStatus.IsRunning(), Is.True);
        }

        [Test]
        public void TestIsNotRunning()
        {
            NodeStatus nodeStatus = NodeStatusConstants.Finished;
            Assert.That(nodeStatus.IsRunning(), Is.False);
        }

        [Test]
        public void TestGetNodeStatusById()
        {
            NodeStatus nodeStatus = NodeStatusConstants.GetNodeStatusById((short)NStatus.Finished);
            Assert.That(nodeStatus, Is.EqualTo(NodeStatusConstants.Finished));
        }

        [Test]
        public void TestGetNodeStatusByIdReturnsUnknownForInvalidId()
        {
            NodeStatus nodeStatus = NodeStatusConstants.GetNodeStatusById((short)999);
            Assert.That(nodeStatus, Is.EqualTo(NodeStatusConstants.Unknown));
        }

        [Test]
        public void TestToStringReturnsName()
        {
            NodeStatus nodeStatus = NodeStatusConstants.WaitingStepTwo;
            Assert.That(nodeStatus.ToString(), Is.EqualTo("Waiting for step two of dkg algorithm"));
        }

        [Test]
        public void TestEqualityOperatorReturnsTrueForSameStatus()
        {
            NodeStatus nodeStatus1 = NodeStatusConstants.WaitingStepTwo;
            NodeStatus nodeStatus2 = NodeStatusConstants.WaitingStepTwo;
            Assert.That(nodeStatus1 == nodeStatus2, Is.True);
        }

        [Test]
        public void TestEqualityOperatorReturnsFalseForDifferentStatus()
        {
            NodeStatus nodeStatus1 = NodeStatusConstants.WaitingStepThree;
            NodeStatus nodeStatus2 = NodeStatusConstants.Finished;
            Assert.That(nodeStatus1 == nodeStatus2, Is.False);
        }

        [Test]
        public void TestInequalityOperatorReturnsFalseForSameStatus()
        {
            NodeStatus nodeStatus1 = NodeStatusConstants.WaitingStepThree;
            NodeStatus nodeStatus2 = NodeStatusConstants.WaitingStepThree;
            Assert.That(nodeStatus1 != nodeStatus2, Is.False);
        }

        [Test]
        public void TestInequalityOperatorReturnsTrueForDifferentStatus()
        {
            NodeStatus nodeStatus1 = NodeStatusConstants.RunningStepThree;
            NodeStatus nodeStatus2 = NodeStatusConstants.Finished;
            Assert.That(nodeStatus1 != nodeStatus2, Is.True);
        }

        [Test]
        public void TestEqualsReturnsTrueForSameStatus()
        {
            NodeStatus nodeStatus1 = NodeStatusConstants.RunningStepThree;
            NodeStatus nodeStatus2 = NodeStatusConstants.RunningStepThree;
            Assert.That(nodeStatus1.Equals(nodeStatus2), Is.True);
        }

        [Test]
        public void TestEqualsReturnsFalseForDifferentStatus()
        {
            NodeStatus nodeStatus1 = NodeStatusConstants.RunningStepOne;
            NodeStatus nodeStatus2 = NodeStatusConstants.Finished;
            Assert.That(nodeStatus1.Equals(nodeStatus2), Is.False);
        }

        [Test]
        public void TestGetHashCodeReturnsSameValueForSameStatus()
        {
            NodeStatus nodeStatus1 = NodeStatusConstants.RunningStepOne;
            NodeStatus nodeStatus2 = NodeStatusConstants.RunningStepOne;
            Assert.That(nodeStatus1.GetHashCode(), Is.EqualTo(nodeStatus2.GetHashCode()));
        }

        [Test]
        public void TestGetHashCodeReturnsDifferentValueForDifferentStatus()
        {
            NodeStatus nodeStatus1 = NodeStatusConstants.RunningStepOne;
            NodeStatus nodeStatus2 = NodeStatusConstants.Finished;
            Assert.That(nodeStatus1.GetHashCode(), Is.Not.EqualTo(nodeStatus2.GetHashCode()));
        }

        [Test]
        public void TestIsRunningStaticMethodReturnsTrueForRunningStatus()
        {
            Assert.That(NodeStatus.IsRunning(NStatus.RunningStepOne), Is.True);
        }

        [Test]
        public void TestIsRunningStaticMethodReturnsFalseForNonRunningStatus()
        {
            Assert.That(NodeStatus.IsRunning(NStatus.Finished), Is.False);
        }
        [Test]
        public void TestImplicitConversionToShort()
        {
            NodeStatus nodeStatus = NodeStatusConstants.RunningStepThree;
            short status = nodeStatus;
            Assert.That(status, Is.EqualTo((short)NStatus.RunningStepThree));
        }

        [Test]
        public void TestImplicitConversionFromShort()
        {
            short status = (short)NStatus.WaitingStepThree;
            NodeStatus nodeStatus = status;
            Assert.That(nodeStatus, Is.EqualTo(NodeStatusConstants.WaitingStepThree));
        }

        [Test]
        public void TestGetNodeStatusByIdReturnsCorrectStatusForValidId()
        {
            NodeStatus nodeStatus = NodeStatusConstants.GetNodeStatusById((short)NStatus.RunningStepOne);
            Assert.That(nodeStatus, Is.EqualTo(NodeStatusConstants.RunningStepOne));
        }

        [Test]
        public void TestGetNodeStatusByIdReturnsCorrectStatusForValidNStatus()
        {
            NodeStatus nodeStatus = NodeStatusConstants.GetNodeStatusById(NStatus.RunningStepOne);
            Assert.That(nodeStatus, Is.EqualTo(NodeStatusConstants.RunningStepOne));
        }

        [Test]
        public void TestGetNodeStatusByIdReturnsUnknownForInvalidNStatus()
        {
            NodeStatus nodeStatus = NodeStatusConstants.GetNodeStatusById((NStatus)999);
            Assert.That(nodeStatus, Is.EqualTo(NodeStatusConstants.Unknown));
        }
    }
}