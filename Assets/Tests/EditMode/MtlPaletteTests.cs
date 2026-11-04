using System.Collections.Generic;
using MotorAndSpeed.World;
using NUnit.Framework;
using UnityEngine;

namespace MotorAndSpeed.Tests
{
    public class MtlPaletteTests
    {
        [Test]
        public void Parse_ReadsSmoothnessAndMetallic()
        {
            Dictionary<string, MtlPalette.Entry> palette = MtlPalette.Parse("newmtl rim\nKd 0.7 0.6 0.5\nNs 700\nPm 0.8\nillum 2\n");

            MtlPalette.Entry rim = palette["rim"];
            Assert.AreEqual(new Color(0.7f, 0.6f, 0.5f, 1f), rim.BaseColor);
            Assert.AreEqual(0.7f, rim.Smoothness, 0.001f);
            Assert.AreEqual(0.8f, rim.Metallic, 0.001f);
            Assert.AreEqual(Color.black, rim.Emission);
        }

        [Test]
        public void Parse_ReadsEmission()
        {
            Dictionary<string, MtlPalette.Entry> palette = MtlPalette.Parse("newmtl headlight\nKd 1 0.9 0.8\nKe 1.6 1.44 1.28\n");

            Assert.AreEqual(new Color(1.6f, 1.44f, 1.28f, 1f), palette["headlight"].Emission);
        }

        [Test]
        public void Parse_MissingFields_UseDefaults()
        {
            Dictionary<string, MtlPalette.Entry> palette = MtlPalette.Parse("# 註解\nnewmtl seat\nKd 0.2 0.2 0.2\n\nnewmtl tire\n");

            Assert.AreEqual(MtlPalette.DEFAULT_SMOOTHNESS, palette["seat"].Smoothness, 0.001f);
            Assert.AreEqual(0f, palette["seat"].Metallic, 0.001f);
            Assert.IsTrue(palette.ContainsKey("tire"));
        }
    }
}
