using UnityEngine;
using UnityEngine.InputSystem;

namespace MotorAndSpeed.UI
{
    /// <summary>
    /// 遊戲外殼：右上角模式選單、Esc 離開遊戲。遊戲啟動時自動建立，切換場景也不會消失。
    /// </summary>
    public class GameShell : MonoBehaviour
    {
        private GUIStyle _style;

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
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                Application.Quit();
        }

        private void OnGUI()
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
                _style.normal.textColor = Color.white;
            }

            GUI.Label(new Rect(20f, Screen.height - 56f, 600f, 30f), "右上角選單：切換模式　Esc：離開", _style);
        }
    }
}
