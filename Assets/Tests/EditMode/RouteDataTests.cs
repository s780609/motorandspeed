using System.Linq;
using MotorAndSpeed.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MotorAndSpeed.Tests
{
    public class RouteDataTests
    {
        private const string ROUTE_PATH = "Assets/Data/wugu_route.json";

        [Test]
        public void FromJson_ReadsAllSections()
        {
            string json = "{\"attribution\":\"OSM\",\"main\":{\"name\":\"成泰路\",\"width\":20,\"points\":[{\"x\":0,\"z\":0},{\"x\":0,\"z\":10}],\"routeStart\":1,\"routeEnd\":9},"
                + "\"lanes\":[{\"name\":\"巷\",\"width\":7,\"points\":[{\"x\":1,\"z\":2}]}],"
                + "\"intersections\":[{\"name\":\"路口\",\"s\":5,\"x\":0,\"z\":5,\"signal\":true,\"roads\":[{\"name\":\"支線\",\"width\":12,\"dirX\":1,\"dirZ\":0,\"route\":false}]}],"
                + "\"start\":{\"x\":0,\"z\":0,\"dirX\":0,\"dirZ\":1}}";

            RouteData data = RouteData.FromJson(json);

            Assert.AreEqual("成泰路", data.main.name);
            Assert.AreEqual(2, data.main.points.Count);
            Assert.AreEqual(9f, data.main.routeEnd, 0.001f);
            Assert.AreEqual("巷", data.lanes[0].name);
            Assert.IsTrue(data.intersections[0].signal);
            Assert.AreEqual(12f, data.intersections[0].roads[0].width, 0.001f);
            Assert.AreEqual(1f, data.start.dirZ, 0.001f);
        }

        [Test]
        public void WuguRoute_HasRealRouteContent()
        {
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(ROUTE_PATH);
            Assert.IsNotNull(asset, $"找不到 {ROUTE_PATH}，請先執行 python tools/build_route_data.py");

            RouteData data = RouteData.FromJson(asset.text);

            Assert.Greater(data.main.points.Count, 100);
            Assert.Greater(data.main.routeEnd - data.main.routeStart, 2000f);
            Assert.AreEqual(2, data.lanes.Count);
            Assert.GreaterOrEqual(data.intersections.Count, 15);
            Assert.GreaterOrEqual(data.intersections.Count(i => i.signal), 8);
        }
    }
}
