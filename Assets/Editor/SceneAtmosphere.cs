using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MotorAndSpeed.EditorTools
{
    /// <summary>
    /// 場景的光影與畫面效果：午後陽光、三色環境光、漸層天空、霧，以及全域後製（色調映射、泛光、色彩調整、暗角）。
    /// 五股世界與測試場景共用，數值同時顧及 PC 與網頁版（網頁版的陰影、反鋸齒由 Mobile 品質等級的 RP 資產控制）。
    /// </summary>
    internal static class SceneAtmosphere
    {
        internal const string SKY_MATERIAL_PATH = "Assets/Settings/WorldSky.mat";
        internal const string LOOK_PROFILE_PATH = "Assets/Settings/WorldLook.asset";

        // 霧色要和天空的地平線接近，遠處建築才會自然融進天空
        internal static readonly Color FogColor = Hex("#C9D6E0");

        private static readonly Color SunColor = Hex("#FFE9CC");
        private static readonly Color AmbientSky = Hex("#A9C3DE");
        private static readonly Color AmbientEquator = Hex("#C6C4B8");
        private static readonly Color AmbientGround = Hex("#6B6457");

        /// <summary>
        /// 套用到目前開啟的場景；fog 為 false 時不開霧（測試場景）。
        /// </summary>
        internal static void Apply(Camera camera, bool fog, float fogStart, float fogEnd)
        {
            Light sun = FindOrCreateSun();
            // 西南方的午後陽光，仰角 38 度，影子斜斜的比較有立體感
            sun.transform.rotation = Quaternion.Euler(38f, 60f, 0f);
            sun.color = SunColor;
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;
            RenderSettings.sun = sun;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;

            RenderSettings.skybox = SkyMaterial();
            RenderSettings.fog = fog;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
            RenderSettings.fogColor = FogColor;

            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = FogColor;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            Volume volume = new GameObject("PostProcessing").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = LookProfile();
        }

        private static Light FindOrCreateSun()
        {
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                    return light;
            }

            Light created = new GameObject("Directional Light").AddComponent<Light>();
            created.type = LightType.Directional;

            return created;
        }

        private static Material SkyMaterial()
        {
            // 內建的程序天空：天頂藍、地平線偏白，地面色用霧色
            Material sky = AssetDatabase.LoadAssetAtPath<Material>(SKY_MATERIAL_PATH);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, SKY_MATERIAL_PATH);
            }

            sky.SetFloat("_SunDisk", 2f);
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_SunSizeConvergence", 6f);
            sky.SetFloat("_AtmosphereThickness", 0.85f);
            sky.SetColor("_SkyTint", Hex("#9CB8D6"));
            sky.SetColor("_GroundColor", FogColor);
            sky.SetFloat("_Exposure", 1.25f);
            EditorUtility.SetDirty(sky);

            return sky;
        }

        private static VolumeProfile LookProfile()
        {
            // 每次重建都重新產生，數值以程式為準
            AssetDatabase.DeleteAsset(LOOK_PROFILE_PATH);
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, LOOK_PROFILE_PATH);

            Tonemapping tonemapping = Add<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            Bloom bloom = Add<Bloom>(profile);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.65f);
            bloom.highQualityFiltering.Override(false);

            ColorAdjustments color = Add<ColorAdjustments>(profile);
            color.postExposure.Override(0.15f);
            color.contrast.Override(10f);
            color.saturation.Override(12f);

            Vignette vignette = Add<Vignette>(profile);
            vignette.intensity.Override(0.18f);

            AssetDatabase.SaveAssets();

            return profile;
        }

        private static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            T component = profile.Add<T>();
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);

            return component;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);

            return color;
        }
    }
}
