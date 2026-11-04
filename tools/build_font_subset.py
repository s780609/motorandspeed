"""產生遊戲 UI 用的中文字型子集（網頁版沒有系統中文字型，要把字型打包進去）。

收集 Assets/Scripts 裡所有非 ASCII 字元、wugu_world.json 裡的地名，加上 ASCII 可列印字元與常用全形標點，
寫進 tools/data/font_chars.txt，再用 fontTools 只保留這些字。
執行（需要 fonttools）：python -I tools/build_font_subset.py <完整字型.otf> <輸出.otf> [授權檔]
"""
import json
import shutil
import sys
from pathlib import Path

from fontTools import subset

ROOT = Path(__file__).resolve().parent.parent
SCRIPTS = ROOT / "Assets" / "Scripts"
WORLD_JSON = ROOT / "Assets" / "Data" / "wugu_world.json"
CHARS_FILE = ROOT / "tools" / "data" / "font_chars.txt"
ASCII = "".join(chr(c) for c in range(0x20, 0x7F))
PUNCTUATION = "：、，。！？（）「」『』→←↑↓▼▲…・—～／＋－％°　"


def collect_strings(value, out):
    if isinstance(value, str):
        out.append(value)
    elif isinstance(value, dict):
        for item in value.values():
            collect_strings(item, out)
    elif isinstance(value, list):
        for item in value:
            collect_strings(item, out)


def collect_chars():
    chars = set(ASCII) | set(PUNCTUATION)
    for path in SCRIPTS.rglob("*.cs"):
        chars |= {c for c in path.read_text(encoding="utf-8") if ord(c) > 0x7F}
    strings = []
    collect_strings(json.loads(WORLD_JSON.read_text(encoding="utf-8")), strings)
    for text in strings:
        chars |= {c for c in text if ord(c) > 0x7F}
    # 換行、BOM 之類的控制字元不放進字型
    return "".join(sorted(c for c in chars if c.isprintable() or c == "　"))


def build(source, target, license_path=None):
    chars = collect_chars()
    CHARS_FILE.parent.mkdir(parents=True, exist_ok=True)
    CHARS_FILE.write_text(chars, encoding="utf-8")

    options = subset.Options()
    options.layout_features = ["*"]
    options.hinting = False
    options.desubroutinize = True
    options.notdef_outline = True
    font = subset.load_font(str(source), options)
    subsetter = subset.Subsetter(options)
    subsetter.populate(text=chars)
    subsetter.subset(font)
    Path(target).parent.mkdir(parents=True, exist_ok=True)
    subset.save_font(font, str(target), options)
    if license_path:
        shutil.copyfile(license_path, Path(target).parent / "OFL.txt")
    print(f"{len(chars)} 個字 → {target}（{Path(target).stat().st_size / 1024:.0f} KB）")


if __name__ == "__main__":
    if len(sys.argv) < 3:
        sys.exit("用法：python -I tools/build_font_subset.py <完整字型.otf> <輸出.otf> [授權檔]")
    build(sys.argv[1], sys.argv[2], sys.argv[3] if len(sys.argv) > 3 else None)
