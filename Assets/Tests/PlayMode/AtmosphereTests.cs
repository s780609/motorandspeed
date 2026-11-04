using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MotorAndSpeed.Tests
{
    /// <summary>
    /// 光影與畫面效果：午後陽光、三色環境光、漸層天空、全域後製（兩個場景都要有）。
    /// </summary>
    public class AtmosphereTests
    {
        [UnityTest]
        public IEnumerator WuguWorld_HasAfternoonLightSkyAndPost()
        {
            SceneManager.LoadScene("WuguWorld");
            yield return null;

            AssertLook();
            Assert.IsTrue(RenderSettings.fog, "五股世界要有霧");
        }

        [UnityTest]
        public IEnumerator Prototype_HasAfternoonLightSkyAndPost()
        {
            SceneManager.LoadScene("Prototype");
            yield return null;

            AssertLook();
        }

        private static void AssertLook()
        {
            Light sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).First(l => l.type == LightType.Directional);
            Assert.That(sun.intensity, Is.InRange(1.2f, 1.6f), "太陽強度");
            Assert.AreEqual(LightShadows.Soft, sun.shadows);
            Assert.That(sun.transform.eulerAngles.x, Is.InRange(30f, 50f), "午後陽光的仰角");

            Assert.AreEqual(AmbientMode.Trilight, RenderSettings.ambientMode);
            Assert.IsNotNull(RenderSettings.skybox, "要有漸層天空");
            Assert.AreEqual(CameraClearFlags.Skybox, Camera.main.clearFlags);
            Assert.IsTrue(Camera.main.GetUniversalAdditionalCameraData().renderPostProcessing, "相機要開後製");

            Volume volume = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).FirstOrDefault(v => v.isGlobal);
            Assert.IsNotNull(volume, "要有全域 Volume");
            Assert.IsTrue(volume.sharedProfile.TryGet(out Tonemapping tonemapping) && tonemapping.active, "要有色調映射");
            Assert.IsTrue(volume.sharedProfile.TryGet(out Bloom bloom) && bloom.intensity.value > 0f, "要有泛光");
        }
    }
}
