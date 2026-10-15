using System.Collections;
using MotorAndSpeed.Game;
using MotorAndSpeed.Scooter;
using MotorAndSpeed.Traffic;
using MotorAndSpeed.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MotorAndSpeed.Tests
{
    /// <summary>
    /// 五股世界的遊戲模式：選單、AI 比賽、時限送貨。
    /// </summary>
    public class GameModeTests
    {
        private GameModeController _modes;
        private ScooterController _player;
        private WorldTraffic _traffic;

        [UnityTest]
        public IEnumerator ModeMenu_HasAllModes()
        {
            yield return LoadWorld();

            Assert.IsNotNull(ModeMenu.Instance, "右上角應該要有模式選單");
            Assert.AreEqual(GameModes.All.Length, ModeMenu.Instance.OptionCount);
        }

        [UnityTest]
        public IEnumerator SprintRace_ClearsTrafficLocksDuringCountdownThenAiRaces()
        {
            yield return LoadWorld();
            _modes.StartMode(GameMode.RaceSprint);
            yield return null;

            Assert.IsFalse(_traffic.TrafficEnabled, "比賽要淨空道路");
            Assert.AreEqual(3, _modes.Racers.Count, "要有 3 位 AI 對手");
            Assert.IsTrue(_player.InputLocked, "倒數時不能偷跑");

            yield return new WaitForSeconds(3.5f);
            Assert.IsTrue(_modes.RaceStarted);
            Assert.IsFalse(_player.InputLocked);

            float[] start = new float[_modes.Racers.Count];
            for (int i = 0; i < start.Length; i++)
            {
                start[i] = _modes.Racers[i].Distance;
            }

            yield return new WaitForSeconds(5f);
            for (int i = 0; i < start.Length; i++)
            {
                Assert.Greater(_modes.Racers[i].Distance - start[i], 30f, $"AI {i} 出發後 5 秒應該跑超過 30 公尺");
            }
        }

        [UnityTest]
        public IEnumerator CircuitRace_StartsOnFirstLap()
        {
            yield return LoadWorld();
            _modes.StartMode(GameMode.RaceCircuit);
            yield return null;

            Assert.IsNotNull(_modes.Race);
            Assert.AreEqual(1, _modes.Race.GetLap(0));
            Assert.AreEqual(3, _modes.Racers.Count);
        }

        [UnityTest]
        public IEnumerator Delivery_AllKindsHaveFiveMinutes()
        {
            yield return LoadWorld();
            foreach (GameMode mode in new[] { GameMode.DeliveryChain, GameMode.DeliverySingle, GameMode.DeliveryPickup })
            {
                _modes.StartMode(mode);
                yield return null;

                Assert.IsNotNull(_modes.Delivery);
                Assert.AreEqual(300f, _modes.Delivery.TimeRemaining, 1f, $"{mode} 應該是 5 分鐘");
                Assert.IsTrue(_traffic.TrafficEnabled, "送貨時路上要有車");
            }
        }

        [UnityTest]
        public IEnumerator DeliveryChain_ReachingTargetAddsTimeAndPicksNextTarget()
        {
            yield return LoadWorld();
            _modes.StartMode(GameMode.DeliveryChain);
            yield return null;

            Vector3 first = _modes.TargetPoint;
            _player.TeleportTo(first + Vector3.up * 0.1f, _player.transform.rotation);
            yield return null;
            yield return null;

            Assert.AreEqual(1, _modes.Delivery.Delivered);
            Assert.Greater(_modes.Delivery.TimeRemaining, 300f, "送達後要加時間");
            Assert.AreNotEqual(first, _modes.TargetPoint, "要換下一個送貨地點");
        }

        [UnityTest]
        public IEnumerator DeliveryPickup_FirstPickupThenDropoff()
        {
            yield return LoadWorld();
            _modes.StartMode(GameMode.DeliveryPickup);
            yield return null;
            Assert.AreEqual(DeliveryStage.Pickup, _modes.Delivery.Stage);

            _player.TeleportTo(_modes.TargetPoint + Vector3.up * 0.1f, _player.transform.rotation);
            yield return null;
            yield return null;

            Assert.AreEqual(DeliveryStage.Dropoff, _modes.Delivery.Stage);
            Assert.IsFalse(_modes.Delivery.IsOver);
        }

        [UnityTest]
        public IEnumerator DeliveryPickup_ShowsCustomerOnMapAndNavigationArrow()
        {
            yield return LoadWorld();
            MiniMap map = Object.FindFirstObjectByType<MiniMap>();
            _modes.StartMode(GameMode.DeliveryPickup);
            yield return null;
            yield return null;

            Assert.IsTrue(map.SecondaryTarget.HasValue, "取貨時就要在地圖上標出客人位置");
            Assert.IsTrue(map.NavigationArrowShown, "畫面上方要有導航箭頭");
            Vector3 customer = map.SecondaryTarget.Value;

            _player.TeleportTo(_modes.TargetPoint + Vector3.up * 0.1f, _player.transform.rotation);
            yield return null;
            yield return null;

            Assert.AreEqual(0f, Vector3.Distance(customer, _modes.TargetPoint), 0.01f, "取貨後要送去一開始標出的客人");
            Assert.IsFalse(map.SecondaryTarget.HasValue);
        }

        [UnityTest]
        public IEnumerator Race_FencesSideRoadsThenRemovesThem()
        {
            yield return LoadWorld();
            _modes.StartMode(GameMode.RaceCircuit);
            yield return null;

            Assert.Greater(_modes.Fences.Count, 5, "賽道經過的路口要把支線圍起來");
            foreach (GameObject fence in _modes.Fences)
            {
                Assert.IsNotNull(fence.GetComponentInChildren<BoxCollider>(), "圍欄要擋得住");
            }

            _modes.StartMode(GameMode.FreeRide);
            yield return null;

            Assert.AreEqual(0, _modes.Fences.Count);
            Assert.IsNull(GameObject.Find("RaceFence"), "圍欄要清掉");
        }

        [UnityTest]
        public IEnumerator FreeRideAfterRace_RestoresTraffic()
        {
            yield return LoadWorld();
            _modes.StartMode(GameMode.RaceSprint);
            yield return null;
            Assert.AreEqual(GameMode.RaceSprint, ModeMenu.Instance.Current, "選單要顯示目前模式");

            _modes.StartMode(GameMode.FreeRide);
            yield return null;

            Assert.AreEqual(GameMode.FreeRide, ModeMenu.Instance.Current);
            Assert.IsTrue(_traffic.TrafficEnabled);
            Assert.AreEqual(0, _modes.Racers.Count);
            Assert.IsFalse(_player.InputLocked);
        }

        private IEnumerator LoadWorld()
        {
            SceneManager.LoadScene("WuguWorld");
            yield return null;
            yield return null;
            _modes = Object.FindFirstObjectByType<GameModeController>();
            _player = Object.FindFirstObjectByType<ScooterController>();
            _traffic = Object.FindFirstObjectByType<WorldTraffic>();
        }
    }
}
