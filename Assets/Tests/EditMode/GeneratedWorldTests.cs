using System.IO;
using System.Linq;
using NUnit.Framework;

namespace MotorAndSpeed.Tests
{
    /// <summary>
    /// 產生出來的世界資料大小預算：影響網頁版下載量與 GitHub 單檔上限（場景檔超過 100MB 會被拒絕推送）。
    /// </summary>
    public class GeneratedWorldTests
    {
        private const string GENERATED_FOLDER = "Assets/Generated/WuguWorld";
        private const string WORLD_SCENE = "Assets/Scenes/WuguWorld.unity";
        private const long MB = 1024L * 1024L;

        [Test]
        public void GeneratedWorld_FileSizeWithinBudget()
        {
            long total = Directory.GetFiles(GENERATED_FOLDER, "*.asset").Sum(file => new FileInfo(file).Length);

            Assert.LessOrEqual(total, 195L * MB, $"Assets/Generated 共 {total / MB} MB");
        }

        [Test]
        public void WorldScene_FileSizeWithinBudget()
        {
            long size = new FileInfo(WORLD_SCENE).Length;

            Assert.LessOrEqual(size, 60L * MB, $"WuguWorld.unity 共 {size / MB} MB");
        }
    }
}
