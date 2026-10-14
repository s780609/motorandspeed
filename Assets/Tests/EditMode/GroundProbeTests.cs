using System.Collections.Generic;
using MotorAndSpeed.Scooter;
using NUnit.Framework;
using UnityEngine;

namespace MotorAndSpeed.Tests
{
    public class GroundProbeTests
    {
        private const float PROBE_LENGTH = 0.5f;

        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject created in _created)
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        [Test]
        public void TryGetGround_GroundWithinProbeLength_ReturnsTrue()
        {
            CreateGround(withCollider: true, isTrigger: false);

            bool grounded = GroundProbe.TryGetGround(new Vector3(0f, 0.3f, 0f), PROBE_LENGTH, out RaycastHit hit);

            Assert.IsTrue(grounded);
            Assert.AreEqual(0.3f, hit.distance, 0.01f);
        }

        [Test]
        public void TryGetGround_FlyingHigherThanProbeLength_ReturnsFalse()
        {
            CreateGround(withCollider: true, isTrigger: false);

            bool grounded = GroundProbe.TryGetGround(new Vector3(0f, 2f, 0f), PROBE_LENGTH, out RaycastHit _);

            Assert.IsFalse(grounded);
        }

        [Test]
        public void TryGetGround_GroundWithoutCollider_ReturnsFalse()
        {
            CreateGround(withCollider: false, isTrigger: false);

            bool grounded = GroundProbe.TryGetGround(new Vector3(0f, 0.3f, 0f), PROBE_LENGTH, out RaycastHit _);

            Assert.IsFalse(grounded);
        }

        [Test]
        public void TryGetGround_TriggerBelow_IsIgnored()
        {
            CreateGround(withCollider: true, isTrigger: true);

            bool grounded = GroundProbe.TryGetGround(new Vector3(0f, 0.3f, 0f), PROBE_LENGTH, out RaycastHit _);

            Assert.IsFalse(grounded);
        }

        private void CreateGround(bool withCollider, bool isTrigger)
        {
            // 用 Cube（BoxCollider）：Plane 的非凸 MeshCollider 不支援設成觸發器
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.05f, 0f);
            ground.transform.localScale = new Vector3(10f, 0.1f, 10f);
            _created.Add(ground);
            Collider collider = ground.GetComponent<Collider>();
            if (!withCollider)
            {
                Object.DestroyImmediate(collider);
            }
            else
            {
                collider.isTrigger = isTrigger;
            }

            Physics.SyncTransforms();
        }
    }
}
