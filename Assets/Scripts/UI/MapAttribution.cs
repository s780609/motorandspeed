using UnityEngine;

namespace MotorAndSpeed.UI
{
    /// <summary>
    /// 地圖資料來源標示（OpenStreetMap 的 ODbL 授權要求標示出處）。
    /// </summary>
    public class MapAttribution : MonoBehaviour
    {
        private GUIStyle _style;

        private void OnGUI()
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 12 };
                _style.normal.textColor = new Color(1f, 1f, 1f, 0.8f);
            }

            GUI.Label(new Rect(12f, Screen.height - 26f, 400f, 22f), "地圖資料 © OpenStreetMap contributors", _style);
        }
    }
}
