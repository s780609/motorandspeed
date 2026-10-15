using System;
using MotorAndSpeed.World;
using NUnit.Framework;
using UnityEngine;

namespace MotorAndSpeed.Tests
{
    public class RoutePathTests
    {
        private RoutePath _path;

        [SetUp]
        public void SetUp()
        {
            // L 形路徑：先往北 10 公尺，再往東 10 公尺
            _path = new RoutePath(new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 10f),
                new Vector3(10f, 0f, 10f),
            });
        }

        [Test]
        public void Length_IsSumOfSegments()
        {
            Assert.AreEqual(20f, _path.Length, 0.001f);
        }

        [Test]
        public void GetPoint_OnFirstAndSecondSegment()
        {
            AssertVector(new Vector3(0f, 0f, 5f), _path.GetPoint(5f));
            AssertVector(new Vector3(5f, 0f, 10f), _path.GetPoint(15f));
        }

        [Test]
        public void GetPoint_OutOfRange_ClampsToEnds()
        {
            AssertVector(new Vector3(0f, 0f, 0f), _path.GetPoint(-5f));
            AssertVector(new Vector3(10f, 0f, 10f), _path.GetPoint(25f));
        }

        [Test]
        public void GetTangentAndRight_FacingNorth_RightIsEast()
        {
            AssertVector(Vector3.forward, _path.GetTangent(5f));
            AssertVector(Vector3.right, _path.GetRight(5f));
        }

        [Test]
        public void GetTangent_OnSecondSegment_FacesEast()
        {
            AssertVector(Vector3.right, _path.GetTangent(15f));
        }

        [Test]
        public void GetClosestDistance_ProjectsOntoNearestSegment()
        {
            Assert.AreEqual(13f, _path.GetClosestDistance(new Vector3(3f, 0f, 12f)), 0.001f);
            Assert.AreEqual(4f, _path.GetClosestDistance(new Vector3(-2f, 0f, 4f)), 0.001f);
        }

        [Test]
        public void Constructor_LessThanTwoPoints_Throws()
        {
            Assert.Throws<ArgumentException>(() => new RoutePath(new[] { Vector3.zero }));
        }

        private static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(0f, Vector3.Distance(expected, actual), 0.001f, $"expected {expected} but was {actual}");
        }
    }
}
