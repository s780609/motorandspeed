using MotorAndSpeed.UI;
using NUnit.Framework;
using UnityEngine;

namespace MotorAndSpeed.Tests
{
    public class MiniMapTests
    {
        [Test]
        public void Projection_SquareBounds_MapsCornersAndCenter()
        {
            MapProjection projection = new MapProjection(new Vector2(0f, 0f), new Vector2(100f, 100f), 100);

            Assert.AreEqual(1f, projection.MetersPerPixel, 0.0001f);
            AssertVector(new Vector2(0f, 0f), projection.WorldToUv(new Vector3(0f, 0f, 0f)));
            AssertVector(new Vector2(1f, 1f), projection.WorldToUv(new Vector3(100f, 0f, 100f)));
            AssertVector(new Vector2(0.5f, 0.25f), projection.WorldToUv(new Vector3(50f, 0f, 25f)));
            AssertVector(new Vector2(50f, 25f), projection.WorldToPixel(new Vector3(50f, 0f, 25f)));
        }

        [Test]
        public void Projection_WideBounds_UsesLongestSideAndKeepsCenter()
        {
            MapProjection projection = new MapProjection(new Vector2(0f, 0f), new Vector2(200f, 100f), 100);

            Assert.AreEqual(200f, projection.WorldSize, 0.0001f);
            Assert.AreEqual(2f, projection.MetersPerPixel, 0.0001f);
            AssertVector(new Vector2(0.5f, 0.5f), projection.WorldToUv(new Vector3(100f, 0f, 50f)));
        }

        [Test]
        public void HeadingUp_FacingNorth_NorthIsAheadEastIsRight()
        {
            AssertVector(new Vector2(0f, 10f), MapProjection.ToHeadingUp(Vector3.zero, 0f, new Vector3(0f, 0f, 10f)));
            AssertVector(new Vector2(10f, 0f), MapProjection.ToHeadingUp(Vector3.zero, 0f, new Vector3(10f, 0f, 0f)));
        }

        [Test]
        public void HeadingUp_FacingEast_EastIsAheadNorthIsLeft()
        {
            AssertVector(new Vector2(0f, 10f), MapProjection.ToHeadingUp(Vector3.zero, 90f, new Vector3(10f, 0f, 0f)));
            AssertVector(new Vector2(-10f, 0f), MapProjection.ToHeadingUp(Vector3.zero, 90f, new Vector3(0f, 0f, 10f)));
        }

        [Test]
        public void HeadingUp_IsRelativeToPlayerPosition()
        {
            AssertVector(new Vector2(0f, 5f), MapProjection.ToHeadingUp(new Vector3(3f, 0f, 3f), 0f, new Vector3(3f, 0f, 8f)));
        }

        [Test]
        public void Zoom_InHalvesOutDoubles_ClampedToRange()
        {
            Assert.AreEqual(0.5f, MapProjection.Zoom(1f, 1, 0.25f, 8f), 0.0001f);
            Assert.AreEqual(4f, MapProjection.Zoom(1f, -2, 0.25f, 8f), 0.0001f);
            Assert.AreEqual(0.25f, MapProjection.Zoom(0.3f, 1, 0.25f, 8f), 0.0001f);
            Assert.AreEqual(8f, MapProjection.Zoom(6f, -1, 0.25f, 8f), 0.0001f);
        }

        [Test]
        public void DrawPolyline_ColorsPixelsOnTheLineOnly()
        {
            const int size = 20;
            Color32 background = new Color32(0, 0, 0, 255);
            Color32 road = new Color32(200, 200, 200, 255);
            Color32[] pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = background;
            }

            MapTextureBuilder.DrawPolyline(pixels, size, new[] { new Vector2(2f, 10f), new Vector2(17f, 10f) }, 1.5f, road);

            Assert.AreEqual(road, pixels[10 * size + 10], "線上的點");
            Assert.AreEqual(road, pixels[11 * size + 10], "線寬範圍內的點");
            Assert.AreEqual(background, pixels[15 * size + 10], "離線很遠的點");
        }

        [Test]
        public void DrawPolyline_OutsideTexture_DoesNotThrow()
        {
            Color32[] pixels = new Color32[10 * 10];

            Assert.DoesNotThrow(() => MapTextureBuilder.DrawPolyline(pixels, 10, new[] { new Vector2(-50f, -50f), new Vector2(60f, 60f) }, 3f, new Color32(255, 0, 0, 255)));
        }

        private static void AssertVector(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(0f, Vector2.Distance(expected, actual), 0.001f, $"expected {expected} but was {actual}");
        }
    }
}
