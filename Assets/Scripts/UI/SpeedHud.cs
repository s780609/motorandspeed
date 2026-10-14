using MotorAndSpeed.Scooter;
using UnityEngine;

namespace MotorAndSpeed.UI
{
    /// <summary>
    /// 原型用的簡易儀表：時速、騰空狀態與操作說明。
    /// </summary>
    public class SpeedHud : MonoBehaviour
    {
        [SerializeField] private ScooterController _target;

        private GUIStyle _speedStyle;
        private GUIStyle _helpStyle;

        private void OnGUI()
        {
            if (_target == null)
                return;

            if (_speedStyle == null)
            {
                _speedStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 40,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.LowerRight,
                };
                _speedStyle.normal.textColor = Color.white;
                _helpStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
                _helpStyle.normal.textColor = Color.white;
            }

            int kilometersPerHour = Mathf.RoundToInt(Mathf.Abs(_target.Speed) * 3.6f);
            string airborne = _target.IsGrounded ? string.Empty : "\n騰空！";
            GUI.Label(new Rect(Screen.width - 340f, Screen.height - 150f, 320f, 130f), $"{kilometersPerHour} km/h{airborne}", _speedStyle);
            GUI.Label(new Rect(20f, 20f, 600f, 40f), "W / S：油門 / 煞車（停住後倒車）　A / D：轉向　R：回起點", _helpStyle);
        }
    }
}
