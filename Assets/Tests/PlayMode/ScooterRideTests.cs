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

        [UnityTest]
        public IEnumerator GrazingIntoCurbAtSpeed_DoesNotBounceHigh([Values(6f, 12f, 20f)] float angle)
        {
            yield return LoadPrototype();

            // 斜斜地擦上 15 公分高的路緣石（人行道）：路緣石的邊從右邊斜切進車子的行進路線
            Vector3 forward = _scooter.transform.forward;
            Vector3 right = _scooter.transform.right;
            Quaternion curbRotation = Quaternion.AngleAxis(-angle, Vector3.up) * _scooter.transform.rotation;
            Vector3 curbRight = curbRotation * Vector3.right;
            GameObject curb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            curb.transform.rotation = curbRotation;
            curb.transform.localScale = new Vector3(6f, 0.15f, 80f);
            // 路緣石左邊緣在前方 45 公尺處剛好通過行進路線
            curb.transform.position = _start + forward * 45f + curbRight * 3f + Vector3.up * 0.075f;
            Rigidbody body = _scooter.GetComponent<Rigidbody>();

            Press(_keyboard.wKey);
            float highest = _scooter.transform.position.y;
            float fastestUp = 0f;
            float elapsed = 0f;
            while (elapsed < 5f && Vector3.Dot(_scooter.transform.position - _start, forward) < 75f)
            {
                highest = Mathf.Max(highest, _scooter.transform.position.y);
                fastestUp = Mathf.Max(fastestUp, body.linearVelocity.y);
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.Less(highest - _start.y, 0.45f, $"斜斜擦上路緣石只能小跳一下（角度 {angle}°，最高 {highest - _start.y:F2} 公尺，往上 {fastestUp:F1} m/s）");
        }

        [UnityTest]
        public IEnumerator AfterBeingHit_StopsSpinning()
        {
            yield return LoadPrototype();
            yield return new WaitForSeconds(0.5f);

            // 模擬被撞：車身突然帶有繞垂直軸的旋轉
            _scooter.GetComponent<Rigidbody>().angularVelocity = new Vector3(0f, 8f, 0f);
            yield return new WaitForSeconds(1f);
            float before = _scooter.transform.eulerAngles.y;
            yield return new WaitForSeconds(1f);
            float after = _scooter.transform.eulerAngles.y;

            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(before, after)), 2f, "被撞之後不應該一直原地打轉");
        }

        [UnityTest]
        public IEnumerator SideHit_DeflectsHeadingThenSettles()
        {
            yield return LoadPrototype();
            yield return new WaitForSeconds(0.5f);
            float before = _scooter.transform.eulerAngles.y;

            // 一個很重的方塊以 9 m/s 從右側撞過來
            GameObject rammer = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rammer.transform.localScale = new Vector3(1f, 1.5f, 2f);
            Rigidbody rammerBody = rammer.AddComponent<Rigidbody>();
            rammerBody.isKinematic = true;
            Vector3 right = _scooter.transform.right;
            rammerBody.position = _scooter.transform.position + right * 2.5f + Vector3.up * 0.8f;
            for (float t = 0f; t < 0.4f; t += Time.fixedDeltaTime)
            {
                rammerBody.MovePosition(rammerBody.position - right * 9f * Time.fixedDeltaTime);
                yield return new WaitForFixedUpdate();
            }

            Object.Destroy(rammer);
            yield return new WaitForSeconds(0.6f);
            float deflected = Mathf.DeltaAngle(before, _scooter.transform.eulerAngles.y);
            yield return new WaitForSeconds(1f);
            float settled = Mathf.DeltaAngle(before, _scooter.transform.eulerAngles.y);

            Assert.Greater(Mathf.Abs(deflected), 3f, "被撞後車頭應該要偏一點");
            Assert.Less(Mathf.Abs(deflected), 30f, "車頭偏太多了");
            Assert.Less(Mathf.Abs(settled - deflected), 2f, "偏完之後要穩住，不能繼續轉");
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
