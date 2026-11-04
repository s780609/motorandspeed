using System.IO;
using System.Linq;
using MotorAndSpeed.UI;
using NUnit.Framework;
using UnityEngine;

namespace MotorAndSpeed.Tests
{
    /// <summary>
    /// 網頁版沒有系統中文字型：UI 要用打包進去的中文字型，網頁版也不能顯示「Esc：離開」。
    /// </summary>
    public class UiFontTests
    {
        [Test]
        public void ChineseFont_IsBundled()
        {
            Font font = Resources.Load<Font>(UiFont.RESOURCE_PATH);
            Assert.IsNotNull(font, "找不到 Resources/Fonts/NotoSansTC-UI");
            Assert.AreSame(font, UiFont.Font);
            Assert.IsTrue(font.HasCharacter('速'), "字型要有中文字");
        }

        [Test]
        public void NoUi_UsesBuiltinFont()
        {
            foreach (string path in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories).Where(p => !p.EndsWith("UiFont.cs")))
            {
                StringAssert.DoesNotContain("LegacyRuntime.ttf", File.ReadAllText(path), $"{path} 要改用 UiFont.Font，內建字型在網頁版沒有中文");
            }
        }

        [Test]
        public void WebBuild_CannotQuit_AndHidesEscHint()
        {
            Assert.IsFalse(GameShell.CanQuit(RuntimePlatform.WebGLPlayer));
            Assert.IsTrue(GameShell.CanQuit(RuntimePlatform.WindowsPlayer));
            StringAssert.DoesNotContain("Esc", GameShell.Hint(false));
            StringAssert.Contains("Esc：離開", GameShell.Hint(true));
        }
    }
}
