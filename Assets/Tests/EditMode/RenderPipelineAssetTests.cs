using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Rendering.Universal;

namespace MotorAndSpeed.Tests
{
    /// <summary>
    /// 畫質設定：PC 用完整效果；網頁版（WebGL 預設的 Mobile 等級）顧效能，而且不編進 PC 的 shader 變體。
    /// </summary>
    public class RenderPipelineAssetTests
    {
        [Test]
        public void PcAsset_HasLongSoftShadowsAndMsaa()
        {
            UniversalRenderPipelineAsset pc = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");

            Assert.GreaterOrEqual(pc.shadowDistance, 80f);
            Assert.AreEqual(4, pc.shadowCascadeCount);
            Assert.AreEqual(4, pc.msaaSampleCount);
        }

        [Test]
        public void WebAsset_IsLightweight()
        {
            UniversalRenderPipelineAsset web = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");

            Assert.AreEqual(1, web.shadowCascadeCount);
            Assert.LessOrEqual(web.msaaSampleCount, 2);
            Assert.LessOrEqual(web.renderScale, 0.85f);
            Assert.IsTrue(web.supportsHDR, "泛光需要 HDR");
            Assert.IsTrue(web.supportsSoftShadows);
            Assert.LessOrEqual(web.shadowDistance, 45f);

            UniversalRendererData renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/Mobile_Renderer.asset");
            Assert.IsFalse(renderer.rendererFeatures.Any(f => f != null && f.GetType().Name.Contains("AmbientOcclusion")), "網頁版不開 SSAO");
        }

        [Test]
        public void PcQualityLevel_ExcludesWebGL()
        {
            string text = File.ReadAllText("ProjectSettings/QualitySettings.asset");
            int pc = text.IndexOf("name: PC");
            int excluded = text.IndexOf("excludedTargetPlatforms:", pc);
            int end = text.IndexOf("m_TextureMipmapLimitGroupNames", excluded);

            StringAssert.Contains("- WebGL", text.Substring(excluded, end - excluded), "PC 品質等級要排除 WebGL，網頁版才不會編進 SSAO 等 shader 變體");
        }
    }
}
