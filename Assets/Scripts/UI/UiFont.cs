using UnityEngine;

namespace MotorAndSpeed.UI
{
    /// <summary>
    /// 遊戲 UI 用的中文字型（Noto Sans TC 子集，SIL OFL 授權）。網頁版沒有系統中文字型，要用打包進去的字型才看得到中文。
    /// 子集由 tools/build_font_subset.py 產生；程式新增中文字串後要重新產生。
    /// </summary>
    public static class UiFont
    {
        public const string RESOURCE_PATH = "Fonts/NotoSansTC-UI";

        private static Font _font;

        /// <summary>中文字型；找不到時退回 Unity 內建字型。</summary>
        public static Font Font
        {
            get
            {
                if (_font == null)
                    _font = Resources.Load<Font>(RESOURCE_PATH);

                if (_font == null)
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                return _font;
            }
        }

        /// <summary>
        /// 在 OnGUI 開頭呼叫：把預設 GUI 皮膚的字型換成中文字型（從皮膚複製的樣式沒有指定字型時會用皮膚字型）。
        /// </summary>
        public static void ApplyToSkin()
        {
            if (GUI.skin.font != Font)
                GUI.skin.font = Font;
        }
    }
}
