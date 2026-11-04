"""UI 中文字型子集的測試：字型要涵蓋程式裡所有中文字，而且夠小。
需要 fonttools；沒有安裝時略過。執行：python -B -m unittest discover -s tools -p "test_*.py"
"""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

ROOT = Path(__file__).resolve().parent.parent
FONT = ROOT / "Assets" / "Resources" / "Fonts" / "NotoSansTC-UI.otf"

try:
    from fontTools.ttLib import TTFont
    import build_font_subset
except ImportError:
    TTFont = None


@unittest.skipIf(TTFont is None, "沒有安裝 fonttools")
class FontSubsetTests(unittest.TestCase):
    def test_font_covers_every_character_used(self):
        # 程式新增中文字串卻忘了重做子集時，這個測試會失敗
        cmap = TTFont(str(FONT)).getBestCmap()
        missing = [c for c in build_font_subset.collect_chars() if ord(c) not in cmap and c != " "]
        self.assertEqual([], missing, "字型缺字，請重新執行 tools/build_font_subset.py")

    def test_font_is_small(self):
        self.assertLess(FONT.stat().st_size, 1.5 * 1024 * 1024)

    def test_license_ships_with_font(self):
        self.assertIn("SIL Open Font License", (FONT.parent / "OFL.txt").read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
