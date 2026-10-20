using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MotorAndSpeed.Scooter;
using MotorAndSpeed.Traffic;
using MotorAndSpeed.UI;
using MotorAndSpeed.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MotorAndSpeed.Tests
{
    /// <summary>
    /// 載入五股世界：騎得出去、車流會動會轉彎會停紅燈、可以清空、小地圖可縮放。
    /// </summary>
    public class WuguWorldTests : InputTestFixture
    {
        private Keyboard _keyboard;
        private ScooterController _scooter;
        private WorldTraffic _traffic;

        [UnityTest]
        public IEnumerator HoldThrottle_RidesForwardFromStart()
        {
            yield return LoadWorld();
            Vector3 start = _scooter.transform.position;

            Press(_keyboard.wKey);
            yield return new WaitForSeconds(3f);

            Assert.IsTrue(_scooter.IsGrounded);
            Assert.Greater(_scooter.Speed, 5f);
            Assert.Greater(Vector3.Distance(start, _scooter.transform.position), 10f);
        }

        [UnityTest]
        public IEnumerator Traffic_SpawnsNearPlayerAndMoves()
        {
            yield return LoadWorld();
            Assert.GreaterOrEqual(_traffic.Vehicles.Count, 100);

            List<Vector3> before = new List<Vector3>();
            float distanceSum = 0f;
            foreach (WorldVehicle vehicle in _traffic.Vehicles)
            {
                before.Add(vehicle.transform.position);
                distanceSum += Vector3.Distance(vehicle.transform.position, _scooter.transform.position);
            }

            Assert.Less(distanceSum / before.Count, 450f, "車輛應該生成在玩家附近");
            yield return new WaitForSeconds(3f);

            float moved = 0f;
            for (int i = 0; i < before.Count; i++)
            {
                moved += Vector3.Distance(before[i], _traffic.Vehicles[i].transform.position);
            }

            Assert.Greater(moved / before.Count, 5f, "NPC 平均應該移動超過 5 公尺");
        }

        [UnityTest]
        public IEnumerator Traffic_IncludesBajiuScooters()
        {
            yield return LoadWorld();
            int bajiu = 0;
            foreach (WorldVehicle vehicle in _traffic.Vehicles)
            {
                if (!vehicle.name.StartsWith("NpcScooterBajiu"))
                {
                    Assert.IsFalse(vehicle.IsReckless, "只有 8+9 會亂騎");
                    continue;
                }

                bajiu++;
                Assert.IsTrue(vehicle.IsScooter, "8+9 要走機車車道");
                Assert.IsTrue(vehicle.IsReckless, "8+9 要切西瓜、飆車、闖紅燈");
            }

            // 8+9 約占 15%，120 台車裡至少要有 8 台
            Assert.GreaterOrEqual(bajiu, 8, "路上的 8+9 太少");
        }

        [UnityTest]
        public IEnumerator Traffic_TurnsOntoOtherRoadsAtJunctions()
        {
            yield return LoadWorld();
            int[] edges = new int[_traffic.Vehicles.Count];
            for (int i = 0; i < edges.Length; i++)
            {
                edges[i] = _traffic.Vehicles[i].Edge;
            }

            yield return new WaitForSeconds(20f);

            int changed = 0;
            for (int i = 0; i < edges.Length; i++)
            {
                if (_traffic.Vehicles[i].Edge != edges[i])
                    changed++;
            }

            Assert.GreaterOrEqual(changed, 5, "20 秒內應該有車輛過路口換到別的路段");
        }

        [UnityTest]
        public IEnumerator Traffic_StopsAtRedLight()
        {
            yield return LoadWorld();

            bool stoppedAtRed = false;
            for (float elapsed = 0f; elapsed < 60f && !stoppedAtRed; elapsed += 0.5f)
            {
                foreach (WorldVehicle vehicle in _traffic.Vehicles)
                {
                    if (!vehicle.gameObject.activeInHierarchy || vehicle.IsTurning || vehicle.Speed > 0.3f)
                        continue;

                    int junction = vehicle.AheadJunction;
                    float stopLineGap = vehicle.DistanceToJunction - _traffic.JunctionRadius(junction) - 4.6f;
                    if (_traffic.TryGetSignal(junction, vehicle.Edge, out SignalState state) && state == SignalState.Red && stopLineGap < 3f)
                        stoppedAtRed = true;
                }

                yield return new WaitForSeconds(0.5f);
            }

            Assert.IsTrue(stoppedAtRed, "60 秒內應該要有 NPC 停在紅燈的停止線前");
        }

        [UnityTest]
        public IEnumerator Traffic_CanBeClearedAndRestored()
        {
            yield return LoadWorld();

            _traffic.SetTrafficEnabled(false);
            yield return null;
            Assert.IsFalse(_traffic.Vehicles[0].gameObject.activeSelf, "比賽模式要淨空道路");

            _traffic.SetTrafficEnabled(true);
            yield return null;
            Assert.IsTrue(_traffic.Vehicles[0].gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator MiniMap_BuildsMapAndZoomsWithKeys()
        {
            yield return LoadWorld();
            yield return null;
            MiniMap map = Object.FindFirstObjectByType<MiniMap>();
            Assert.IsNotNull(map, "世界場景應該要有小地圖");
            Assert.IsNotNull(map.MapTexture, "小地圖貼圖應該已產生");

            float before = map.MetersPerPixel;
            Press(_keyboard.equalsKey, queueEventOnly: true);
            yield return null;
            yield return null;

            Assert.Less(map.MetersPerPixel, before, "按 + 應該要放大");
        }

        [UnityTest]
        public IEnumerator ChengluBridge_DeckIsRideableAndEndIsBlocked()
        {
            yield return LoadWorld();
            TextAsset asset = Resources.FindObjectsOfTypeAll<TextAsset>().First(t => t.name == "wugu_world");
            BridgeData bridge = WorldData.FromJson(asset.text).bridges[0];
            RoutePath path = new RoutePath(bridge.points.ConvertAll(p => new Vector3(p.x, 0f, p.z)));

            // 橋中段：從高處往下打，要先打到橋面
            Vector3 middle = path.GetPoint(path.Length * 0.6f);
            Assert.IsTrue(Physics.Raycast(middle + Vector3.up * 30f, Vector3.down, out RaycastHit hit, 40f));
            Assert.AreEqual(BridgeProfile.DECK_HEIGHT, hit.point.y, 0.3f, "橋面要在高架高度");

            // 斷頭的地方要擋起來，不能直接騎出去
            Vector3 end = path.GetPoint(path.Length - 6f) + Vector3.up * (BridgeProfile.DECK_HEIGHT + 0.6f);
            Assert.IsTrue(Physics.Raycast(end, path.GetTangent(path.Length - 6f), 8f), "橋的盡頭要有護欄");
        }

        [UnityTest]
        public IEnumerator Parks_HaveNoBuildings_MediansAreRaised()
        {
            yield return LoadWorld();
            TextAsset asset = Resources.FindObjectsOfTypeAll<TextAsset>().First(t => t.name == "wugu_world");
            WorldData data = WorldData.FromJson(asset.text);

            // 公園裡不能有房子（房子是高於 4 公尺的方塊碰撞體）
            ParkData park = data.parks.Find(p => p.name == "防災體健公園");
            List<Vector2> outline = park.points.ConvertAll(p => new Vector2(p.x, p.z));
            List<string> buildings = new List<string>();
            foreach (BoxCollider box in Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None))
            {
                Vector3 center = box.bounds.center;
                if (box.bounds.size.y > 4f && Polygon.Contains(outline, new Vector2(center.x, center.z)))
                    buildings.Add(box.name);
            }

            Assert.IsEmpty(buildings, "防災體健公園裡有建築：" + string.Join("、", buildings));

            // 分隔島比路面高（路緣石）
            MedianData median = data.medians.OrderByDescending(m => m.points.Count).First();
            RoutePoint middle = median.points[median.points.Count / 2];
            Assert.IsTrue(Physics.Raycast(new Vector3(middle.x, 10f, middle.z), Vector3.down, out RaycastHit hit, 20f));
            Assert.Greater(hit.point.y, 0.12f, "分隔島要高出路面");
        }

        [UnityTest]
        public IEnumerator MiniMap_NKeyTogglesNorthUp()
        {
            yield return LoadWorld();
            yield return null;
            MiniMap map = Object.FindFirstObjectByType<MiniMap>();
            _scooter.TeleportTo(_scooter.transform.position, Quaternion.Euler(0f, 90f, 0f));
            yield return null;

            Assert.IsFalse(map.NorthUp, "預設是車頭朝上");
            Assert.AreEqual(90f, map.MapRotation, 1f, "車頭朝上時地圖跟著面向旋轉");

            Press(_keyboard.nKey, queueEventOnly: true);
            yield return null;
            yield return null;

            Assert.IsTrue(map.NorthUp, "按 N 切換成正北朝上");
            Assert.AreEqual(0f, map.MapRotation, 0.01f, "正北朝上時地圖不旋轉");
        }

        private IEnumerator LoadWorld()
        {
            SceneManager.LoadScene("WuguWorld");
            yield return null;
            yield return new WaitForFixedUpdate();

            // 場景載入後才加入虛擬鍵盤，避免裝置在載入過程中失效
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _scooter = Object.FindFirstObjectByType<ScooterController>();
            _traffic = Object.FindFirstObjectByType<WorldTraffic>();
        }
    }
}
