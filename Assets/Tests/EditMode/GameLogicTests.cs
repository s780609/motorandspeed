using MotorAndSpeed.Game;
using MotorAndSpeed.World;
using NUnit.Framework;
using UnityEngine;

namespace MotorAndSpeed.Tests
{
    public class DeliverySessionTests
    {
        [Test]
        public void Chain_EachDeliveryAddsTimeAndCount()
        {
            DeliverySession session = new DeliverySession(DeliveryKind.Chain, 300f, 45f);
            session.Tick(100f);
            session.ReachTarget();

            Assert.AreEqual(1, session.Delivered);
            Assert.AreEqual(245f, session.TimeRemaining, 0.001f);
            Assert.IsFalse(session.IsOver);
            Assert.AreEqual(DeliveryStage.Dropoff, session.Stage);
        }

        [Test]
        public void Chain_TimeUp_EndsWithDeliveredCount()
        {
            DeliverySession session = new DeliverySession(DeliveryKind.Chain, 300f, 45f);
            session.ReachTarget();
            session.ReachTarget();
            session.Tick(400f);

            Assert.IsTrue(session.IsOver);
            Assert.AreEqual(2, session.Delivered);
            Assert.AreEqual(0f, session.TimeRemaining, 0.001f);
        }

        [Test]
        public void Single_DeliveredInTime_Succeeds()
        {
            DeliverySession session = new DeliverySession(DeliveryKind.Single, 300f);
            session.Tick(120f);
            session.ReachTarget();

            Assert.IsTrue(session.IsOver);
            Assert.IsTrue(session.Succeeded);
        }

        [Test]
        public void Single_TimeUp_Fails()
        {
            DeliverySession session = new DeliverySession(DeliveryKind.Single, 300f);
            session.Tick(301f);

            Assert.IsTrue(session.IsOver);
            Assert.IsFalse(session.Succeeded);
        }

        [Test]
        public void PickupDropoff_FirstPickupThenDeliver()
        {
            DeliverySession session = new DeliverySession(DeliveryKind.PickupDropoff, 300f);
            Assert.AreEqual(DeliveryStage.Pickup, session.Stage);

            session.ReachTarget();
            Assert.AreEqual(DeliveryStage.Dropoff, session.Stage);
            Assert.IsFalse(session.IsOver);

            session.ReachTarget();
            Assert.IsTrue(session.IsOver);
            Assert.IsTrue(session.Succeeded);
        }

        [Test]
        public void AfterOver_NothingChanges()
        {
            DeliverySession session = new DeliverySession(DeliveryKind.Single, 10f);
            session.Tick(11f);
            session.ReachTarget();
            session.Tick(5f);

            Assert.IsFalse(session.Succeeded);
            Assert.AreEqual(0, session.Delivered);
        }
    }

    public class RaceProgressTests
    {
        [Test]
        public void Sprint_PositionsFollowDistance()
        {
            RaceProgress race = new RaceProgress(3, 1000f, 1, false);
            race.Update(0, 300f);
            race.Update(1, 500f);
            race.Update(2, 100f);

            Assert.AreEqual(2, race.GetPosition(0));
            Assert.AreEqual(1, race.GetPosition(1));
            Assert.AreEqual(3, race.GetPosition(2));
        }

        [Test]
        public void Sprint_ReachingTheEnd_Finishes()
        {
            RaceProgress race = new RaceProgress(1, 1000f, 1, false);
            race.Update(0, 990f);
            Assert.IsFalse(race.IsFinished(0));

            race.Update(0, 998f);
            Assert.IsTrue(race.IsFinished(0));
        }

        [Test]
        public void Circuit_CrossingStartLine_CountsLap()
        {
            RaceProgress race = new RaceProgress(1, 1000f, 3, true);
            race.Update(0, 500f);
            race.Update(0, 950f);
            race.Update(0, 30f);

            Assert.AreEqual(2, race.GetLap(0));
            Assert.AreEqual(1030f, race.GetProgress(0), 0.001f);
        }

        [Test]
        public void Circuit_RidingBackwardsOverTheLine_LosesTheLap()
        {
            RaceProgress race = new RaceProgress(1, 1000f, 3, true);
            race.Update(0, 500f);
            race.Update(0, 950f);
            race.Update(0, 30f);
            race.Update(0, 970f);

            Assert.AreEqual(1, race.GetLap(0));
        }

        [Test]
        public void Circuit_AfterAllLaps_Finishes()
        {
            RaceProgress race = new RaceProgress(1, 1000f, 2, true);
            for (int lap = 0; lap < 2; lap++)
            {
                race.Update(0, 500f);
                race.Update(0, 950f);
                race.Update(0, 20f);
            }

            Assert.IsTrue(race.IsFinished(0));
            Assert.AreEqual(2, race.GetLap(0));
        }

        [Test]
        public void FinishedRacer_StaysAheadOfLaterProgress()
        {
            RaceProgress race = new RaceProgress(2, 1000f, 1, false);
            race.Update(0, 999f);
            race.Update(1, 998.5f);

            Assert.AreEqual(1, race.GetPosition(0), "先完賽的應該排第一");
            Assert.AreEqual(2, race.GetPosition(1));
        }
    }

    public class RacingLineTests
    {
        private RoutePath _track;

        [SetUp]
        public void SetUp()
        {
            // 先直線 200 公尺，接著一個 90 度急彎
            _track = new RoutePath(new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 200f),
                new Vector3(5f, 0f, 205f),
                new Vector3(100f, 0f, 205f),
            });
        }

        [Test]
        public void Straight_FullSpeed()
        {
            Assert.AreEqual(27f, RacingLine.TargetSpeed(_track, 20f, 27f, 30f, 7f), 0.001f);
        }

        [Test]
        public void SharpCornerAhead_SlowsDown()
        {
            float speed = RacingLine.TargetSpeed(_track, 190f, 27f, 30f, 7f);

            Assert.Greater(speed, 3f);
            Assert.Less(speed, 20f);
        }

        [Test]
        public void CatchUp_BehindFaster_AheadSlower_CloseNeutral()
        {
            Assert.Greater(RacingLine.CatchUpFactor(100f, 300f), 1f);
            Assert.Less(RacingLine.CatchUpFactor(400f, 100f), 1f);
            Assert.AreEqual(1f, RacingLine.CatchUpFactor(200f, 205f), 0.02f);
        }
    }
}
