using UnityEngine;
using UnityEngine.InputSystem;

namespace MotorAndSpeed.UI
{
    /// <summary>
    /// 遊戲外殼：右上角模式選單、Esc 離開遊戲（網頁版不能離開，Esc 也會被瀏覽器拿去退出全螢幕）。
    /// 遊戲啟動時自動建立，切換場景也不會消失。
    /// </summary>
    public class GameShell : MonoBehaviour
    {
        private GUIStyle _style;

        /// <summary>這個平台能不能用 Esc 離開遊戲。</summary>
        public static bool CanQuit(RuntimePlatform platform)
        {
            return platform != RuntimePlatform.WebGLPlayer;
        }

        /// <summary>畫面左下角的操作提示。</summary>
        public static string Hint(bool canQuit)
        {
            return canQuit ? "右上角選單：切換模式　Esc：離開" : "右上角選單：切換模式";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            GameObject shell = new GameObject("GameShell");
            DontDestroyOnLoad(shell);
            shell.AddComponent<GameShell>();
            shell.AddComponent<ModeMenu>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && CanQuit(Application.platform))
                Application.Quit();
        }

        private void OnGUI()
        {
            UiFont.ApplyToSkin();
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
                _style.normal.textColor = Color.white;
            }

            GUI.Label(new Rect(20f, Screen.height - 56f, 600f, 30f), Hint(CanQuit(Application.platform)), _style);
        }
    }
}
