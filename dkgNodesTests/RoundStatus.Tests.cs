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

using dkgServiceNode.Constants;

namespace dkgNodesTests
{
    [TestFixture]
    public class RoundStatusTests
    {
        [Test]
        public void TestImplicitConversionToRStatus()
        {
            RoundStatus roundStatus = RoundStatusConstants.Started;
            RStatus status = roundStatus;
            Assert.That(status, Is.EqualTo(RStatus.Registration));
        }

        [Test]
        public void TestImplicitConversionFromRStatus()
        {
            RStatus status = RStatus.Registration;
            RoundStatus roundStatus = status;
            Assert.That(roundStatus, Is.EqualTo(RoundStatusConstants.Started));
        }

        [Test]
        public void TestIsRunning()
        {
            RoundStatus roundStatus = RoundStatusConstants.CreatingDeals;
            Assert.That(roundStatus.IsRunning(), Is.True);

            roundStatus = RoundStatusConstants.ProcessingDeals;
            Assert.That(roundStatus.IsRunning(), Is.True);

            roundStatus = RoundStatusConstants.ProcessingResponses;
            Assert.That(roundStatus.IsRunning(), Is.True);
        }

        [Test]
        public void TestIsNotRunning()
        {
            RoundStatus roundStatus = RoundStatusConstants.Finished;
            Assert.That(roundStatus.IsRunning(), Is.False);
        }

        [Test]
        public void TestGetRoundStatusById()
        {
            RoundStatus roundStatus = RoundStatusConstants.GetRoundStatusById((short)RStatus.Finished);
            Assert.That(roundStatus, Is.EqualTo(RoundStatusConstants.Finished));
        }

        [Test]
        public void TestGetRoundStatusByIdReturnsUnknownForInvalidId()
        {
            RoundStatus roundStatus = RoundStatusConstants.GetRoundStatusById((short)999);
            Assert.That(roundStatus, Is.EqualTo(RoundStatusConstants.Unknown));
        }

        [Test]
        public void TestNextStatusId()
        {
            RoundStatus roundStatus = RoundStatusConstants.Started;
            Assert.That(roundStatus.NextStatusId(), Is.EqualTo(RStatus.CreatingDeals));

            roundStatus = RoundStatusConstants.CreatingDeals;
            Assert.That(roundStatus.NextStatusId(), Is.EqualTo(RStatus.ProcessingDeals));

            roundStatus = RoundStatusConstants.ProcessingDeals;
            Assert.That(roundStatus.NextStatusId(), Is.EqualTo(RStatus.ProcessingResponses));
        }

        [Test]
        public void TestNextStatus()
        {
            RoundStatus roundStatus = RoundStatusConstants.Started;
            Assert.That(roundStatus.NextStatus(), Is.EqualTo(RoundStatusConstants.CreatingDeals));

            roundStatus = RoundStatusConstants.CreatingDeals;
            Assert.That(roundStatus.NextStatus(), Is.EqualTo(RoundStatusConstants.ProcessingDeals));

            roundStatus = RoundStatusConstants.ProcessingDeals;
            Assert.That(roundStatus.NextStatus(), Is.EqualTo(RoundStatusConstants.ProcessingResponses));
        }

        [Test]
        public void TestCancelStatus()
        {
            RoundStatus roundStatus = RoundStatusConstants.Started;
            Assert.That(roundStatus.CancelStatus(), Is.EqualTo(RoundStatusConstants.Cancelled));
        }

        [Test]
        public void TestImplicitConversionToShort()
        {
            RoundStatus roundStatus = RoundStatusConstants.Started;
            short status = roundStatus;
            Assert.That(status, Is.EqualTo((short)RStatus.Registration));
        }

        [Test]
        public void TestImplicitConversionFromShort()
        {
            short status = (short)RStatus.Registration;
            RoundStatus roundStatus = status;
            Assert.That(roundStatus, Is.EqualTo(RoundStatusConstants.Started));
        }

        [Test]
        public void TestToStringReturnsName()
        {
            RoundStatus roundStatus = RoundStatusConstants.Started;
            Assert.That(roundStatus.ToString(), Is.EqualTo("Collecting applications"));
        }

        [Test]
        public void TestEqualityOperatorReturnsTrueForSameStatus()
        {
            RoundStatus roundStatus1 = RoundStatusConstants.Started;
            RoundStatus roundStatus2 = RoundStatusConstants.Started;
            Assert.That(roundStatus1 == roundStatus2, Is.True);
        }

        [Test]
        public void TestEqualityOperatorReturnsFalseForDifferentStatus()
        {
            RoundStatus roundStatus1 = RoundStatusConstants.Started;
            RoundStatus roundStatus2 = RoundStatusConstants.Finished;
            Assert.That(roundStatus1 == roundStatus2, Is.False);
        }

        [Test]
        public void TestInequalityOperatorReturnsFalseForSameStatus()
        {
            RoundStatus roundStatus1 = RoundStatusConstants.Started;
            RoundStatus roundStatus2 = RoundStatusConstants.Started;
            Assert.That(roundStatus1 != roundStatus2, Is.False);
        }

        [Test]
        public void TestInequalityOperatorReturnsTrueForDifferentStatus()
        {
            RoundStatus roundStatus1 = RoundStatusConstants.Started;
            RoundStatus roundStatus2 = RoundStatusConstants.Finished;
            Assert.That(roundStatus1 != roundStatus2, Is.True);
        }

        [Test]
        public void TestEqualsReturnsTrueForSameStatus()
        {
            RoundStatus roundStatus1 = RoundStatusConstants.Started;
            RoundStatus roundStatus2 = RoundStatusConstants.Started;
            Assert.That(roundStatus1.Equals(roundStatus2), Is.True);
        }

        [Test]
        public void TestEqualsReturnsFalseForDifferentStatus()
        {
            RoundStatus roundStatus1 = RoundStatusConstants.Started;
            RoundStatus roundStatus2 = RoundStatusConstants.Finished;
            Assert.That(roundStatus1.Equals(roundStatus2), Is.False);
        }

        [Test]
        public void TestGetHashCodeReturnsSameValueForSameStatus()
        {
            RoundStatus roundStatus1 = RoundStatusConstants.Started;
            RoundStatus roundStatus2 = RoundStatusConstants.Started;
            Assert.That(roundStatus1.GetHashCode(), Is.EqualTo(roundStatus2.GetHashCode()));
        }

        [Test]
        public void TestGetHashCodeReturnsDifferentValueForDifferentStatus()
        {
            RoundStatus roundStatus1 = RoundStatusConstants.Started;
            RoundStatus roundStatus2 = RoundStatusConstants.Finished;
            Assert.That(roundStatus1.GetHashCode(), Is.Not.EqualTo(roundStatus2.GetHashCode()));
        }

        [Test]
        public void TestIsRunningStaticMethodReturnsTrueForRunningStatus()
        {
            Assert.That(RoundStatus.IsRunning(RStatus.CreatingDeals), Is.True);
            Assert.That(RoundStatus.IsRunning(RStatus.ProcessingDeals), Is.True);
            Assert.That(RoundStatus.IsRunning(RStatus.ProcessingResponses), Is.True);
        }

        [Test]
        public void TestIsRunningStaticMethodReturnsFalseForNonRunningStatus()
        {
            Assert.That(RoundStatus.IsRunning(RStatus.Finished), Is.False);
        }

        [Test]
        public void TestIsVersatileReturnsTrueForVersatileStatus()
        {
            RoundStatus roundStatus = RoundStatusConstants.Started;
            Assert.That(roundStatus.IsVersatile(), Is.True);
        }

        [Test]
        public void TestIsVersatileReturnsFalseForNonVersatileStatus()
        {
            RoundStatus roundStatus = RoundStatusConstants.Finished;
            Assert.That(roundStatus.IsVersatile(), Is.False);
        }

        [Test]
        public void TestGetRoundStatusByIdReturnsCorrectStatusForValidRStatus()
        {
            RoundStatus roundStatus = RoundStatusConstants.GetRoundStatusById(RStatus.Finished);
            Assert.That(roundStatus, Is.EqualTo(RoundStatusConstants.Finished));
        }

        [Test]
        public void TestGetRoundStatusByIdReturnsUnknownForInvalidRStatus()
        {
            RoundStatus roundStatus = RoundStatusConstants.GetRoundStatusById((RStatus)999);
            Assert.That(roundStatus, Is.EqualTo(RoundStatusConstants.Unknown));
        }

        [Test]
        public void TestIsRunningReturnsFalseForNotStartedStatus()
        {
            RoundStatus roundStatus = RoundStatusConstants.NotStarted;
            Assert.That(roundStatus.IsRunning(), Is.False);
        }

        [Test]
        public void TestIsRunningReturnsFalseForFinishedStatus()
        {
            RoundStatus roundStatus = RoundStatusConstants.Finished;
            Assert.That(roundStatus.IsRunning(), Is.False);
        }

        [Test]
        public void TestIsRunningReturnsFalseForCancelledStatus()
        {
            RoundStatus roundStatus = RoundStatusConstants.Cancelled;
            Assert.That(roundStatus.IsRunning(), Is.False);
        }

        [Test]
        public void TestIsRunningReturnsFalseForFailedStatus()
        {
            RoundStatus roundStatus = RoundStatusConstants.Failed;
            Assert.That(roundStatus.IsRunning(), Is.False);
        }

        [Test]
        public void TestIsVersatileReturnsFalseForUnknownStatus()
        {
            RoundStatus roundStatus = RoundStatusConstants.Unknown;
            Assert.That(roundStatus.IsVersatile(), Is.False);
        }

        [Test]
        public void TestIsVersatileReturnsFalseForCancelledStatus()
        {
            RoundStatus roundStatus = RoundStatusConstants.Cancelled;
            Assert.That(roundStatus.IsVersatile(), Is.False);
        }

        [Test]
        public void TestIsVersatileReturnsFalseForFailedStatus()
        {
            RoundStatus roundStatus = RoundStatusConstants.Failed;
            Assert.That(roundStatus.IsVersatile(), Is.False);
        }

    }
}
