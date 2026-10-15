using MotorAndSpeed.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MotorAndSpeed.Tests
{
    public class BridgeTests
    {
        [Test]
        public void FromJson_ReadsBridges()
        {
            string json = "{\"bridges\":[{\"name\":\"成蘆橋\",\"width\":17.6,\"points\":[{\"x\":0,\"z\":0},{\"x\":10,\"z\":-5}]}]}";

            WorldData data = WorldData.FromJson(json);

            Assert.AreEqual(1, data.bridges.Count);
            Assert.AreEqual("成蘆橋", data.bridges[0].name);
            Assert.AreEqual(17.6f, data.bridges[0].width, 0.01f);
            Assert.AreEqual(2, data.bridges[0].points.Count);
        }

        [Test]
        public void WorldData_HasHalfOfChengluBridge()
        {
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/wugu_world.json");
            WorldData data = WorldData.FromJson(asset.text);

            BridgeData bridge = data.bridges.Find(b => b.name == "成蘆橋");
            Assert.IsNotNull(bridge, "要有成蘆橋");
            float length = new RoutePath(bridge.points.ConvertAll(p => new Vector3(p.x, 0f, p.z))).Length;
            Assert.That(length, Is.InRange(550f, 750f), "成蘆橋全長約 1.3 公里，只做一半");
            Assert.IsFalse(data.edges.Exists(e => e.name == "成蘆橋"), "高架橋不能混進平面路網");
        }

        [Test]
        public void Profile_RampsFromGroundToDeck()
        {
            Assert.AreEqual(0f, BridgeProfile.HeightAt(0f), 0.001f, "起點貼著地面");
            Assert.AreEqual(BridgeProfile.DECK_HEIGHT, BridgeProfile.HeightAt(BridgeProfile.RAMP_LENGTH), 0.001f);
            Assert.AreEqual(BridgeProfile.DECK_HEIGHT, BridgeProfile.HeightAt(600f), 0.001f, "引道之後維持橋面高度");
        }

        [Test]
        public void Profile_RampIsGentleAndRising()
        {
            float previous = 0f;
            for (float s = 1f; s <= BridgeProfile.RAMP_LENGTH + 10f; s += 1f)
            {
                float height = BridgeProfile.HeightAt(s);
                Assert.GreaterOrEqual(height, previous, "引道只會往上");
                Assert.Less(height - previous, 0.1f, "坡度要小於 10%");
                previous = height;
            }
        }
    }
}
