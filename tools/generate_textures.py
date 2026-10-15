"""產生台灣街景用的貼圖：招牌、窗戶、冷氣、店面、路面標字、電線桿警示套。

招牌名稱都是虛構的，避免使用真實品牌。
執行：python tools/generate_textures.py
"""
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "Assets" / "Textures" / "Street"
FONT_BOLD = "C:/Windows/Fonts/msjhbd.ttc"

# (店名, 底色, 字色)
SIGNS = [
    ("大吉不動產", "#1E6B45", "#FFFFFF"),
    ("阿明機車行", "#C62828", "#FFFFFF"),
    ("古早味豆花", "#F4E3B2", "#8B2E16"),
    ("晨光早餐店", "#F6C343", "#B3261E"),
    ("阿嬤滷肉飯", "#7A1F1F", "#FFE7A0"),
    ("金華眼鏡", "#1F4E8C", "#FFFFFF"),
    ("仁心牙醫診所", "#FFFFFF", "#1E6FB8"),
    ("順發五金行", "#2E7D32", "#FFF59D"),
    ("明日補習班", "#283593", "#FFEB3B"),
    ("福記中藥行", "#5D4037", "#FFE082"),
    ("好運彩券行", "#E53935", "#FFFF8D"),
    ("茶語手搖飲", "#00897B", "#FFFFFF"),
]


def font(size):
    return ImageFont.truetype(FONT_BOLD, size)


def centered_text(draw, box, text, size, fill):
    left, top, right, bottom = box
    f = font(size)
    bbox = draw.textbbox((0, 0), text, font=f)
    w, h = bbox[2] - bbox[0], bbox[3] - bbox[1]
    draw.text((left + (right - left - w) / 2 - bbox[0], top + (bottom - top - h) / 2 - bbox[1]), text, font=f, fill=fill)


def horizontal_sign(name, bg, fg):
    img = Image.new("RGB", (1024, 256), bg)
    d = ImageDraw.Draw(img)
    d.rectangle((10, 10, 1013, 245), outline=fg, width=6)
    size = min(170, int(900 / len(name)))
    centered_text(d, (20, 20, 1004, 236), name, size, fg)
    return img


def vertical_sign(name, bg, fg):
    img = Image.new("RGB", (192, 768), bg)
    d = ImageDraw.Draw(img)
    d.rectangle((8, 8, 183, 759), outline=fg, width=5)
    cell = 740 / len(name)
    size = int(min(150, cell * 0.85))
    for i, ch in enumerate(name):
        top = 14 + i * cell
        centered_text(d, (14, top, 178, top + cell), ch, size, fg)
    return img


def window(kind):
    img = Image.new("RGB", (256, 256), "#C9CCCF")
    d = ImageDraw.Draw(img)
    # 玻璃與反光
    d.rectangle((14, 14, 241, 241), fill="#3E5468")
    d.polygon([(60, 241), (130, 14), (160, 14), (90, 241)], fill="#56708A")
    if kind == "grill":
        d.rectangle((14, 14, 90, 241), fill="#D9C9A3")
        for x in range(14, 250, 34):
            d.rectangle((x, 6, x + 6, 250), fill="#3A3D40")
        for y in (6, 126, 244):
            d.rectangle((6, y, 250, y + 6), fill="#3A3D40")
    elif kind == "curtain":
        d.rectangle((14, 14, 80, 241), fill="#E8D5B0")
        d.rectangle((176, 14, 241, 241), fill="#E8D5B0")
    else:
        d.rectangle((122, 14, 134, 241), fill="#C9CCCF")
    return img


def air_conditioner():
    img = Image.new("RGB", (256, 170), "#F2F2EE")
    d = ImageDraw.Draw(img)
    d.rectangle((4, 4, 251, 165), outline="#BDBDB8", width=4)
    for r in range(60, 8, -12):
        d.ellipse((168 - r, 85 - r, 168 + r, 85 + r), outline="#77797C", width=4)
    d.rectangle((24, 30, 90, 40), fill="#BDBDB8")
    return img


def storefront(kind):
    img = Image.new("RGB", (512, 384), "#B8BDC2")
    d = ImageDraw.Draw(img)
    if kind == "shutter":
        # 鐵捲門
        d.rectangle((0, 0, 511, 383), fill="#9EA4A9")
        for y in range(8, 384, 16):
            d.line((0, y, 511, y), fill="#7D8287", width=4)
        d.rectangle((226, 350, 286, 362), fill="#5C6166")
    elif kind == "glass":
        d.rectangle((16, 16, 495, 383), fill="#2F3B47")
        for row, y in enumerate((90, 170, 250)):
            d.rectangle((40, y, 300, y + 8), fill="#8A8F94")
            for i in range(9):
                colors = ["#E57373", "#FFD54F", "#81C784", "#64B5F6"]
                d.rectangle((48 + i * 28, y - 40, 68 + i * 28, y), fill=colors[(i + row) % 4])
        d.rectangle((330, 60, 480, 383), outline="#C9CCCF", width=8)
    else:
        # 營業中的店：暖色燈光、櫃台、海報
        d.rectangle((16, 16, 495, 383), fill="#F3DFAE")
        d.rectangle((16, 260, 495, 383), fill="#A86B3C")
        d.rectangle((60, 60, 170, 200), fill="#E35D5B")
        d.rectangle((200, 60, 310, 200), fill="#4E9AD3")
        d.rectangle((340, 40, 470, 120), fill="#FFFFFF")
        centered_text(d, (340, 40, 470, 120), "營業中", 36, "#C62828")
    return img


def road_text(top, bottom):
    # 路面標字：字體拉長，方便騎士從遠處看清楚
    img = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    centered_text(d, (0, 0, 256, 128), top, 110, (245, 245, 245, 255))
    centered_text(d, (0, 128, 256, 256), bottom, 110, (245, 245, 245, 255))
    return img.resize((256, 512))


def route_number_text():
    # 路面「107縣道」：由遠到近直式排列（107 / 縣 / 道）
    img = Image.new("RGBA", (256, 384), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    white = (245, 245, 245, 255)
    centered_text(d, (0, 0, 256, 128), "107", 100, white)
    centered_text(d, (0, 128, 256, 256), "縣", 110, white)
    centered_text(d, (0, 256, 256, 384), "道", 110, white)
    return img.resize((256, 768))


def tower_window():
    # 高樓外牆的一格（約 3 公尺寬、3.1 公尺高），會在牆面上重複鋪滿
    img = Image.new("RGB", (256, 256), "#E2E0D9")
    d = ImageDraw.Draw(img)
    d.rectangle((30, 40, 226, 196), fill="#3E5468")
    d.polygon([(70, 196), (120, 40), (140, 40), (90, 196)], fill="#56708A")
    d.rectangle((0, 196, 255, 214), fill="#C9C6BE")
    d.rectangle((126, 40, 130, 196), fill="#B9BCBF")
    return img


def facade_tile(kind):
    # 街屋外牆的一格（約 3 公尺寬、一層樓高），會在整面牆上重複鋪滿；底色用白色，遊戲中再依建築染色
    img = Image.new("RGB", (256, 256), "#F4F2EC")
    d = ImageDraw.Draw(img)
    d.rectangle((0, 238, 255, 255), fill="#D9D6CE")
    if kind == "apartment":
        # 公寓：兩扇小窗加凸出鐵窗
        for left in (24, 140):
            d.rectangle((left, 70, left + 92, 200), fill="#3E5468")
            for x in range(left, left + 93, 18):
                d.rectangle((x, 62, x + 4, 206), fill="#4A4F54")
            d.rectangle((left - 6, 56, left + 98, 64), fill="#9AA2A8")
        return img
    d.rectangle((40, 60, 216, 200), fill="#3E5468")
    d.polygon([(80, 200), (130, 60), (150, 60), (100, 200)], fill="#56708A")
    if kind == "grill":
        for x in range(40, 220, 22):
            d.rectangle((x, 52, x + 5, 208), fill="#3A3D40")
        d.rectangle((32, 126, 224, 131), fill="#3A3D40")
    else:
        d.rectangle((40, 60, 96, 200), fill="#E8D5B0")
        d.rectangle((160, 60, 216, 200), fill="#E8D5B0")
    # 窗下的冷氣
    d.rectangle((150, 208, 230, 236), fill="#F8F8F5", outline="#BDBDB8", width=2)
    d.ellipse((196, 210, 222, 234), outline="#77797C", width=2)
    return img


def race_chevron():
    # 比賽圍欄的方向箭頭：紅底白色「＞」，一格約 1.2 公尺寬，會在圍欄上重複
    img = Image.new("RGB", (256, 256), "#C8282B")
    d = ImageDraw.Draw(img)
    d.polygon([(60, 30), (130, 30), (210, 128), (130, 226), (60, 226), (140, 128)], fill="#F7F7F2")
    d.rectangle((0, 0, 255, 10), fill="#F7F7F2")
    d.rectangle((0, 245, 255, 255), fill="#F7F7F2")
    return img


def pole_stripes():
    img = Image.new("RGB", (128, 128), "#F2C400")
    d = ImageDraw.Draw(img)
    for offset in range(-128, 256, 64):
        d.polygon([(offset, 128), (offset + 32, 128), (offset + 160, 0), (offset + 128, 0)], fill="#1E1E1E")
    return img


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for i, (name, bg, fg) in enumerate(SIGNS):
        horizontal_sign(name, bg, fg).save(OUT / f"sign_h_{i:02d}.png")
        vertical_sign(name, bg, fg).save(OUT / f"sign_v_{i:02d}.png")
    for kind in ("grill", "curtain", "slide"):
        window(kind).save(OUT / f"window_{kind}.png")
    for kind in ("shutter", "glass", "open"):
        storefront(kind).save(OUT / f"storefront_{kind}.png")
    air_conditioner().save(OUT / "air_conditioner.png")
    road_text("機車", "專用").save(OUT / "road_scooter_only.png")
    road_text("機車", "待轉").save(OUT / "road_scooter_wait.png")
    route_number_text().save(OUT / "road_route_107.png")
    tower_window().save(OUT / "tower_window.png")
    for kind in ("grill", "curtain", "apartment"):
        facade_tile(kind).save(OUT / f"facade_tile_{kind}.png")
    race_chevron().save(OUT / "race_chevron.png")
    pole_stripes().save(OUT / "pole_stripes.png")
    print(f"textures: {len(list(OUT.glob('*.png')))} files in {OUT}")


if __name__ == "__main__":
    main()
