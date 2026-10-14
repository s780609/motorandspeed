using System.Collections;
using MotorAndSpeed.Scooter;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MotorAndSpeed.Tests
{
    /// <summary>
    /// 載入測試場景，用虛擬鍵盤實際騎騎看。
    /// </summary>
    public class ScooterRideTests : InputTestFixture
    {
        private Keyboard _keyboard;
        private ScooterController _scooter;
        private Vector3 _start;

        [UnityTest]
        public IEnumerator HoldThrottle_RidesStraightForwardOnGround()
        {
            yield return LoadPrototype();

            Press(_keyboard.wKey);
            yield return new WaitForSeconds(2f);

            Assert.IsTrue(_scooter.IsGrounded);
            Assert.Greater(_scooter.Speed, 5f);
            Assert.Greater(_scooter.transform.position.z - _start.z, 5f);
            Assert.AreEqual(_start.x, _scooter.transform.position.x, 0.5f);
        }

        [UnityTest]
        public IEnumerator ThrottleAndSteerRight_TurnsRight()
        {
            yield return LoadPrototype();

            Press(_keyboard.wKey);
            yield return new WaitForSeconds(1f);
            Press(_keyboard.dKey);
            yield return new WaitForSeconds(1f);

            Assert.Greater(_scooter.transform.position.x - _start.x, 0.5f);
            Assert.Greater(_scooter.transform.eulerAngles.y, 10f);
        }

        [UnityTest]
        public IEnumerator FullThrottleOverRamp_GoesAirborneThenLands()
        {
            yield return LoadPrototype();

            Press(_keyboard.wKey);
            bool wasAirborne = false;
            float elapsed = 0f;
            while (elapsed < 15f && !(wasAirborne && _scooter.IsGrounded))
            {
                if (!_scooter.IsGrounded && _scooter.transform.position.y > 0.5f)
                    wasAirborne = true;

                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.IsTrue(wasAirborne, "衝過跳台時應該要騰空");
            Assert.IsTrue(_scooter.IsGrounded, "騰空後應該要落地");
            Assert.Greater(_scooter.Speed, 5f, "落地後應該還在前進");
        }

        private IEnumerator LoadPrototype()
        {
            SceneManager.LoadScene("Prototype");
            yield return null;
            yield return new WaitForFixedUpdate();

            // 場景載入後才加入虛擬鍵盤，避免裝置在載入過程中失效
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _scooter = Object.FindFirstObjectByType<ScooterController>();
            _start = _scooter.transform.position;
        }
    }
}
