"""產生 Low-poly 模型（OBJ + MTL）與網頁預覽。

座標系：Y 軸朝上、單位公尺、車頭朝 +Z（Unity 匯入 OBJ 時會自動翻轉 X 軸）。
執行：python tools/generate_models.py
"""
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "Assets" / "Models"
PREVIEW = ROOT / "tools" / "preview.html"

# 材質顏色（Low-poly 風格：每種材質只有一個純色）
MATERIALS = {
    "body": "#2E86C1",       # 車殼
    "seat": "#2C2C2C",       # 座墊
    "trim": "#4A4A4A",       # 踏板、避震、深色零件
    "tire": "#1E1E1E",       # 輪胎
    "rim": "#B0B0B0",        # 輪框
    "headlight": "#FFF4C2",  # 大燈
    "taillight": "#D62828",  # 尾燈
    "cone_red": "#E4572E",   # 交通錐紅色
    "cone_white": "#F5F5F5", # 交通錐反光條
    "cone_base": "#333333",  # 交通錐底座
    "penguin_black": "#2B2F3A",  # 企鵝背部
    "penguin_white": "#F7F7F2",  # 企鵝肚子、臉
    "penguin_orange": "#F5A623", # 嘴巴、腳
    "penguin_eye": "#111111",    # 眼睛
    "penguin_blush": "#F4A6B8",  # 腮紅
    "helmet": "#FFD23F",         # 安全帽
    "helmet_stripe": "#F7F7F2",  # 安全帽中間白線
    "helmet_visor": "#3A3A3A",   # 帽簷
    "car_paint": "#E8E8E8",      # 汽車車漆（遊戲中會隨機換色）
    "car_glass": "#2E3B48",      # 車窗
    "truck_cab": "#2F6DB5",      # 卡車車頭（遊戲中會隨機換色）
    "truck_cargo": "#D9DCDF",    # 貨櫃
    "rider_jacket": "#5B7C99",   # NPC 騎士外套（遊戲中會隨機換色）
    "rider_pants": "#3B3F4A",
    "rider_skin": "#E9C2A0",
    "rider_helmet": "#F2F2F2",   # NPC 安全帽（遊戲中會隨機換色）
    "bus_paint": "#2E8B57",      # 公車車身下半部（遊戲中會隨機換色）
    "bus_white": "#F2F2EE",      # 公車車身上半部
    "tree_trunk": "#6B4F3A",     # 行道樹樹幹
    "tree_leaf": "#5E9E4A",      # 行道樹樹冠
    "bajiu_skin": "#C98E62",     # 8+9 騎士：曬黑的皮膚
    "bajiu_tattoo": "#23272C",   # 包手刺青：墨黑
    "bajiu_tattoo_gray": "#5D646C",   # 包手刺青：灰色漸層
    "bajiu_tank": "#F4F4EE",     # 白色吊嘎
    "bajiu_shorts": "#1F2A44",   # 海灘褲
    "bajiu_hair": "#E9C46A",     # 染金刺蝟頭
    "bajiu_shades": "#141414",   # 墨鏡
    "bajiu_gold": "#D4AF37",     # 金項鍊
    "bajiu_slipper_blue": "#2D6CDF",  # 藍白拖
    "bajiu_slipper_white": "#F2F2F2",
    "bajiu_cig": "#F5F1E6",      # 香菸
    "bajiu_ember": "#FF6A2B",    # 菸頭火光
    "tuned_chrome": "#D9DDE2",   # 改裝車：白鐵排氣管
    "tuned_spring": "#E02020",   # 紅色彈簧後避震
    "tuned_neon": "#39E6FF",     # 底盤藍光、燈眉
    "tuned_carbon": "#222428",   # 尾翼
    "tuned_decal": "#B6FF3B",    # 螢光彩貼
    "plate": "#F4F4F0",          # 車牌
    "blinker": "#F39C12",        # 方向燈
    "meter": "#15202B",          # 儀表
    "mirror": "#A9BCCF",         # 後照鏡鏡面
    "junk_primer": "#9C9C92",    # 破爛車：補土色拼裝車殼
    "junk_tape": "#B9B9B2",      # 銀色膠帶
    "junk_foam": "#E8C34A",      # 座墊破洞露出的海綿
    "junk_rust": "#8A4B2A",      # 鏽斑
    "junk_dark": "#1A1A1A",      # 破掉的燈殼
    "chrome": "#E3E6EA",         # 電鍍件（握把端子、腳踏板飾條）
    "glass": "#3C4E5E",          # 安全帽鏡片、公車亭玻璃
    "rider_mask": "#BFD7EA",     # 騎士口罩
    "delivery_box": "#E2513A",   # 企鵝背上的外送保溫箱
    "bus_led": "#FF9A1F",        # 公車路線 LED 板
    "tree_leaf_dark": "#4C8A3C", # 樹冠暗面
    "palm_trunk": "#8C7A5E",     # 大王椰子樹幹
    "palm_leaf": "#6BA34A",      # 大王椰子葉
    "lamp_pole": "#7D858C",      # 路燈桿、公車亭柱子
    "lamp_head": "#FFF6DA",      # 路燈燈罩（自發光）
    "mailbox_red": "#C8102E",    # 紅色郵筒（限時）
    "mailbox_green": "#1E7B3C",  # 綠色郵筒（普通）
    "hydrant": "#D63A2F",        # 消防栓
    "shelter_roof": "#3E6E8E",   # 公車亭頂棚
}

# 材質的表面參數：(光滑度 0~1, 金屬度 0~1, 自發光倍率)，沒列出的用 DEFAULT_SURFACE
# 寫進 MTL：Ns = 光滑度 × 1000、Pm = 金屬度、Ke = 顏色 × 自發光倍率；Unity 的 ObjMaterialImporter 依此設定材質
DEFAULT_SURFACE = (0.15, 0.0, 0.0)
SURFACE = {
    "body": (0.55, 0.0, 0.0), "car_paint": (0.6, 0.0, 0.0), "truck_cab": (0.5, 0.0, 0.0),
    "bus_paint": (0.5, 0.0, 0.0), "bus_white": (0.45, 0.0, 0.0), "seat": (0.3, 0.0, 0.0),
    "tire": (0.05, 0.0, 0.0), "rim": (0.55, 0.8, 0.0), "chrome": (0.7, 0.9, 0.0),
    "tuned_chrome": (0.7, 0.9, 0.0), "bajiu_gold": (0.6, 0.9, 0.0), "tuned_carbon": (0.5, 0.2, 0.0),
    "car_glass": (0.85, 0.0, 0.0), "glass": (0.85, 0.0, 0.0), "mirror": (0.9, 0.6, 0.0),
    "helmet": (0.6, 0.0, 0.0), "rider_helmet": (0.6, 0.0, 0.0), "penguin_eye": (0.7, 0.0, 0.0),
    "plate": (0.3, 0.0, 0.0), "delivery_box": (0.4, 0.0, 0.0),
    "headlight": (0.5, 0.0, 1.6), "taillight": (0.5, 0.0, 1.4), "blinker": (0.5, 0.0, 1.0),
    "tuned_neon": (0.4, 0.0, 2.0), "bus_led": (0.4, 0.0, 1.8), "meter": (0.5, 0.0, 0.6),
    "lamp_head": (0.4, 0.0, 1.8), "bajiu_ember": (0.2, 0.0, 2.0),
}


def hex_to_rgb(hex_color):
    h = hex_color.lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


def mean(points):
    n = len(points)
    return tuple(sum(p[i] for p in points) / n for i in range(3))


def newell_normal(points):
    nx = ny = nz = 0.0
    for i, (x1, y1, z1) in enumerate(points):
        x2, y2, z2 = points[(i + 1) % len(points)]
        nx += (y1 - y2) * (z1 + z2)
        ny += (z1 - z2) * (x1 + x2)
        nz += (x1 - x2) * (y1 + y2)
    length = math.sqrt(nx * nx + ny * ny + nz * nz) or 1.0
    return (nx / length, ny / length, nz / length)


class Model:
    def __init__(self, name):
        self.name = name
        self.faces = []  # (points, material)

    def solid(self, polys, center=None):
        """加入一組封閉形狀的面，並讓每個面的法線朝外。"""
        if center is None:
            center = mean([p for pts, _ in polys for p in pts])
        for pts, mat in polys:
            n = newell_normal(pts)
            c = mean(pts)
            outward = (c[0] - center[0]) * n[0] + (c[1] - center[1]) * n[1] + (c[2] - center[2]) * n[2]
            if outward < 0:
                pts = pts[::-1]
            self.faces.append((pts, mat))

    def box(self, x0, x1, y0, y1, z0, z1, mat):
        polys = []
        for x in (x0, x1):
            polys.append(([(x, y0, z0), (x, y1, z0), (x, y1, z1), (x, y0, z1)], mat))
        for y in (y0, y1):
            polys.append(([(x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)], mat))
        for z in (z0, z1):
            polys.append(([(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)], mat))
        self.solid(polys)

    def extrude(self, profile, x0, x1, mat):
        """把側面輪廓 [(z, y), ...] 沿 X 軸拉出厚度。"""
        cz = sum(z for z, _ in profile) / len(profile)
        cy = sum(y for _, y in profile) / len(profile)
        polys = []
        count = len(profile)
        for i in range(count):
            za, ya = profile[i]
            zb, yb = profile[(i + 1) % count]
            for x in (x0, x1):
                polys.append(([(x, cy, cz), (x, ya, za), (x, yb, zb)], mat))
            polys.append(([(x0, ya, za), (x0, yb, zb), (x1, yb, zb), (x1, ya, za)], mat))
        self.solid(polys, center=((x0 + x1) / 2, cy, cz))

    def cylinder_x(self, cy, cz, r, x0, x1, segments, side_mat, cap_mat):
        """沿 X 軸的圓柱（輪子、握把）。"""
        ring = [(cy + r * math.cos(2 * math.pi * i / segments),
                 cz + r * math.sin(2 * math.pi * i / segments)) for i in range(segments)]
        polys = [([(x0, y, z) for y, z in ring], cap_mat),
                 ([(x1, y, z) for y, z in ring], cap_mat)]
        for i in range(segments):
            ya, za = ring[i]
            yb, zb = ring[(i + 1) % segments]
            polys.append(([(x0, ya, za), (x0, yb, zb), (x1, yb, zb), (x1, ya, za)], side_mat))
        self.solid(polys, center=((x0 + x1) / 2, cy, cz))

    def frustum_y(self, heights, mats, radius_at, segments):
        """沿 Y 軸的截錐，依高度分段上色（交通錐）。"""
        angles = [2 * math.pi * i / segments for i in range(segments)]
        for (ya, yb), mat in zip(zip(heights, heights[1:]), mats):
            ra, rb = radius_at(ya), radius_at(yb)
            polys = []
            for i in range(segments):
                a0, a1 = angles[i], angles[(i + 1) % segments]
                polys.append(([
                    (ra * math.cos(a0), ya, ra * math.sin(a0)),
                    (ra * math.cos(a1), ya, ra * math.sin(a1)),
                    (rb * math.cos(a1), yb, rb * math.sin(a1)),
                    (rb * math.cos(a0), yb, rb * math.sin(a0)),
                ], mat))
            self.solid(polys, center=(0, (ya + yb) / 2, 0))
        top = heights[-1]
        rt = radius_at(top)
        cap = [(rt * math.cos(a), top, rt * math.sin(a)) for a in angles]
        self.solid([(cap, mats[-1])], center=(0, top - 0.1, 0))

    def ellipsoid(self, center, radii, seg_u, seg_v, mat_for, pitch=0.0, yaw=0.0, rows=None):
        """Low-poly 橢球。mat_for(方向) 依面的朝向（旋轉前）決定材質；pitch 繞 X 軸、yaw 繞 Y 軸（角度）。
        rows 只產生從頂端往下的前幾圈（做成碗狀，例如安全帽）。"""
        rx, ry, rz = radii
        p, w = math.radians(pitch), math.radians(yaw)

        def direction(i, j):
            theta = math.pi * j / seg_v
            phi = 2 * math.pi * (i % seg_u) / seg_u
            return (math.sin(theta) * math.cos(phi), math.cos(theta), math.sin(theta) * math.sin(phi))

        def place(d):
            x, y, z = rx * d[0], ry * d[1], rz * d[2]
            y, z = y * math.cos(p) - z * math.sin(p), y * math.sin(p) + z * math.cos(p)
            x, z = x * math.cos(w) + z * math.sin(w), -x * math.sin(w) + z * math.cos(w)
            return (center[0] + x, center[1] + y, center[2] + z)

        polys = []
        for j in range(rows or seg_v):
            for i in range(seg_u):
                if j == 0:
                    dirs = [direction(i, 0), direction(i + 1, 1), direction(i, 1)]
                elif j == seg_v - 1:
                    dirs = [direction(i, j), direction(i + 1, j), direction(i, j + 1)]
                else:
                    dirs = [direction(i, j), direction(i + 1, j), direction(i + 1, j + 1), direction(i, j + 1)]
                avg = mean(dirs)
                length = math.sqrt(sum(c * c for c in avg)) or 1.0
                mat = mat_for(tuple(c / length for c in avg)) if callable(mat_for) else mat_for
                polys.append(([place(d) for d in dirs], mat))
        self.solid(polys, center=center)

    def pyramid(self, base, tip, mat):
        polys = [(list(base), mat)]
        for i in range(len(base)):
            polys.append(([base[i], base[(i + 1) % len(base)], tip], mat))
        self.solid(polys)

    def limb(self, a, b, r, mat_for, seg_u=6, seg_v=4):
        """兩點之間的 Low-poly 肢體（拉長的橢球）；mat_for 收到的方向 +Z 指向 b。"""
        v = [b[i] - a[i] for i in range(3)]
        length = math.sqrt(sum(c * c for c in v))
        pitch = math.degrees(math.asin(-v[1] / length))
        yaw = math.degrees(math.atan2(v[0], v[2]))
        self.ellipsoid(mean([a, b]), (r, r, length / 2 + r * 0.5), seg_u, seg_v, mat_for, pitch=pitch, yaw=yaw)

    def loft(self, sections, mat):
        """把一串點數相同的截面依序連起來（車殼、土除）；每段各自判斷法線朝外，薄的彎曲零件也正確。"""
        for a, b in zip(sections, sections[1:]):
            polys = []
            for j in range(len(a)):
                k = (j + 1) % len(a)
                # 倒角剛好是高度一半時截面會有重疊的點，跳過面積為 0 的三角形
                for tri in ([a[j], a[k], b[k]], [a[j], b[k], b[j]]):
                    if len({tuple(round(c, 6) for c in p) for p in tri}) == 3:
                        polys.append((tri, mat))
            self.solid(polys, center=mean(a + b))
        self.solid([(list(sections[0]), mat)], center=mean(sections[1]))
        self.solid([(list(sections[-1]), mat)], center=mean(sections[-2]))

    def to_obj(self):
        lines = [f"# {self.name} - generated by tools/generate_models.py", "mtllib palette.mtl", f"o {self.name}"]
        v_index = 1
        n_index = 1
        for mat in dict.fromkeys(m for _, m in self.faces):
            lines.append(f"usemtl {mat}")
            for pts, face_mat in self.faces:
                if face_mat != mat:
                    continue
                for x, y, z in pts:
                    lines.append(f"v {x:.4f} {y:.4f} {z:.4f}")
                nx, ny, nz = newell_normal(pts)
                lines.append(f"vn {nx:.4f} {ny:.4f} {nz:.4f}")
                refs = " ".join(f"{v_index + i}//{n_index}" for i in range(len(pts)))
                lines.append(f"f {refs}")
                v_index += len(pts)
                n_index += 1
        return "\n".join(lines) + "\n"

    def triangle_count(self):
        return sum(len(pts) - 2 for pts, _ in self.faces)


def section(z, x0, x1, y0, y1, c):
    """Z 位置上的倒角矩形截面（給 loft 用）。"""
    return [(x0 + c, y0, z), (x1 - c, y0, z), (x1, y0 + c, z), (x1, y1 - c, z),
            (x1 - c, y1, z), (x0 + c, y1, z), (x0, y1 - c, z), (x0, y0 + c, z)]


def build_scooter_body(stock=True):
    # 台灣 125cc 速克達：V 形前擋板、平底腳踏板、往後收窄的側殼、兩段式座墊
    # 前後輪、龍頭轉軸、座墊高度、腳踏板高度與舊版相同，騎士模型不用動
    # stock=False 時不裝原廠排氣管、後避震、車牌（給改裝車換自己的）
    m = Model("scooter_body")
    # 前擋板（車頭）：後面是護腿板，前面斜面往上收窄成 V 形車鼻
    m.loft([section(0.30, -0.21, 0.21, 0.32, 0.55, 0.05),
            section(0.38, -0.22, 0.22, 0.32, 0.90, 0.06),
            section(0.48, -0.20, 0.20, 0.34, 0.98, 0.06),
            section(0.58, -0.15, 0.15, 0.52, 0.98, 0.05),
            section(0.645, -0.10, 0.10, 0.72, 0.96, 0.04)], "body")
    # 大燈：深色外框、主燈、上方一條 LED 燈條；兩側方向燈
    m.box(-0.095, 0.095, 0.785, 0.912, 0.612, 0.648, "trim")
    m.box(-0.08, 0.08, 0.80, 0.90, 0.62, 0.655, "headlight")
    m.box(-0.07, 0.07, 0.918, 0.932, 0.612, 0.646, "headlight")
    for side in (1, -1):
        x0, x1 = sorted((side * 0.11, side * 0.16))
        m.box(x0, x1, 0.86, 0.90, 0.555, 0.595, "blinker")
    # 前碟煞卡鉗（左前輪外側）
    m.box(0.062, 0.095, 0.30, 0.38, 0.70, 0.78, "trim")
    # 前土除：沿前輪的弧形
    fender = []
    for i in range(8):
        a = math.radians(15 + 150 * i / 7)
        cz, cy = 0.65 + math.cos(a) * 0.28, 0.24 + math.sin(a) * 0.28
        dz, dy = math.cos(a), math.sin(a)
        fender.append([(-0.075, cy - dy * 0.012, cz - dz * 0.012), (0.075, cy - dy * 0.012, cz - dz * 0.012),
                       (0.075, cy + dy * 0.012, cz + dz * 0.012), (-0.075, cy + dy * 0.012, cz + dz * 0.012)])
    m.loft(fender, "body")
    # 前叉（左右兩支，上端藏進車頭）
    for side in (1, -1):
        m.limb((side * 0.085, 0.24, 0.65), (side * 0.085, 0.64, 0.55), 0.025, "rim")
    # 腳踏板：車身色底座加黑色防滑墊
    m.box(-0.18, 0.18, 0.26, 0.345, -0.08, 0.36, "body")
    m.box(-0.16, 0.16, 0.345, 0.36, -0.03, 0.32, "trim")
    # 腳踏板兩側電鍍飾條、側殼中段的深色分模線
    for side in (1, -1):
        x0, x1 = sorted((side * 0.18, side * 0.186))
        m.box(x0, x1, 0.30, 0.318, -0.06, 0.34, "chrome")
        x0, x1 = sorted((side * 0.189, side * 0.193))
        m.box(x0, x1, 0.562, 0.572, -0.68, -0.12, "trim")
    # 側殼：從腳踏板往後升高，中段等寬，尾端收窄
    m.loft([section(0.06, -0.17, 0.17, 0.36, 0.74, 0.04),
            section(-0.10, -0.19, 0.19, 0.33, 0.76, 0.04),
            section(-0.70, -0.19, 0.19, 0.49, 0.79, 0.04),
            section(-0.82, -0.17, 0.17, 0.54, 0.80, 0.04),
            section(-0.93, -0.12, 0.12, 0.60, 0.79, 0.03),
            section(-0.985, -0.08, 0.08, 0.63, 0.76, 0.02)], "body")
    # 座墊：前段騎士、後段乘客稍高
    m.loft([section(0.06, -0.14, 0.14, 0.73, 0.78, 0.02),
            section(-0.04, -0.16, 0.16, 0.74, 0.815, 0.03),
            section(-0.42, -0.17, 0.17, 0.76, 0.82, 0.03),
            section(-0.54, -0.17, 0.17, 0.76, 0.865, 0.03),
            section(-0.82, -0.15, 0.15, 0.78, 0.875, 0.03),
            section(-0.89, -0.11, 0.11, 0.78, 0.85, 0.02)], "seat")
    # 尾燈與兩側後方向燈
    m.box(-0.09, 0.09, 0.64, 0.74, -1.00, -0.96, "taillight")
    for side in (1, -1):
        x0, x1 = sorted((side * 0.095, side * 0.13))
        m.box(x0, x1, 0.66, 0.70, -0.95, -0.92, "blinker")
    # 後扶手：沿座墊後段兩側繞到車尾
    for side in (1, -1):
        m.limb((side * 0.165, 0.80, -0.55), (side * 0.15, 0.85, -0.86), 0.015, "trim", 6, 4)
        m.limb((side * 0.15, 0.85, -0.86), (0, 0.85, -0.93), 0.015, "trim", 6, 4)
    # 左側 CVT 傳動箱一路延伸到後輪軸（OBJ 的 +X 匯入 Unity 後會在車身左側）
    m.loft([section(-0.05, 0.05, 0.15, 0.22, 0.40, 0.04),
            section(-0.25, 0.05, 0.18, 0.16, 0.42, 0.05),
            section(-0.55, 0.06, 0.18, 0.14, 0.38, 0.05),
            section(-0.72, 0.07, 0.16, 0.16, 0.32, 0.04)], "trim")
    # 後土除擋泥板
    m.box(-0.07, 0.07, 0.30, 0.56, -0.93, -0.91, "trim")
    if stock:
        # 原廠後避震（左側）
        m.limb((0.14, 0.32, -0.64), (0.14, 0.62, -0.72), 0.025, "seat")
        # 原廠排氣管（右側）：黑色管身加銀色護片
        m.limb((-0.05, 0.20, -0.28), (-0.13, 0.27, -0.45), 0.025, "trim")
        m.limb((-0.13, 0.27, -0.45), (-0.16, 0.42, -0.88), 0.06, "trim", 8, 5)
        m.box(-0.22, -0.195, 0.32, 0.42, -0.76, -0.56, "rim")
        # 車牌
        m.box(-0.09, 0.09, 0.48, 0.60, -0.945, -0.935, "plate")
    return m


def build_bajiu_scooter_tuned():
    # 8+9 改裝車：在原本車身上加改裝件（OBJ 的 -X 匯入 Unity 後在右側）
    m = build_scooter_body(stock=False)
    m.name = "bajiu_scooter_tuned"
    # 白鐵大砲管：從引擎下方接到車尾右側，往上翹
    m.limb((-0.02, 0.16, -0.15), (-0.18, 0.28, -0.45), 0.025, "tuned_chrome")
    m.limb((-0.18, 0.28, -0.45), (-0.23, 0.44, -0.98), 0.07, "tuned_chrome", 8, 5)
    m.cylinder_x(0.43, -0.96, 0.035, -0.26, -0.20, 8, "junk_dark", "junk_dark")
    # 紅色彈簧後避震（左側，傳動箱上方）
    m.limb((0.15, 0.32, -0.64), (0.15, 0.64, -0.72), 0.035, "tuned_spring")
    # 底盤藍光
    m.box(-0.15, 0.15, 0.245, 0.26, -0.03, 0.30, "tuned_neon")
    # 尾翼：兩支立柱撐起一片翼板
    for side in (1, -1):
        x0, x1 = sorted((side * 0.08, side * 0.11))
        m.box(x0, x1, 0.80, 0.93, -0.88, -0.84, "tuned_carbon")
    m.box(-0.23, 0.23, 0.93, 0.96, -0.97, -0.78, "tuned_carbon")
    # 側殼兩側螢光彩貼（貼在等寬的中段）
    for side in (1, -1):
        x0, x1 = sorted((side * 0.188, side * 0.197))
        m.box(x0, x1, 0.60, 0.64, -0.70, -0.10, "tuned_decal")
        m.box(x0, x1, 0.55, 0.58, -0.62, -0.22, "tuned_decal")
    # 燈眉
    for side in (1, -1):
        x0, x1 = sorted((side * 0.09, side * 0.13))
        m.box(x0, x1, 0.90, 0.925, 0.585, 0.615, "tuned_neon")
    # 翹牌：車牌往上翹
    m.extrude([(-0.93, 0.50), (-0.95, 0.49), (-1.04, 0.61), (-1.02, 0.62)], -0.09, 0.09, "plate")
    return m


def build_bajiu_scooter_junk():
    # 8+9 破爛車：補土拼裝、膠帶補丁、座墊破洞、燈破一半、生鏽
    m = build_scooter_body()
    m.name = "bajiu_scooter_junk"
    # 前擋板兩側補土色補丁
    for side in (1, -1):
        x0, x1 = sorted((side * 0.205, side * 0.226))
        m.box(x0, x1, 0.50, 0.70, 0.34, 0.44, "junk_primer")
    # 側殼後段換過一塊補土色的殼，左側殼破洞露出黑色內部
    for side in (1, -1):
        x0, x1 = sorted((side * 0.188, side * 0.197))
        m.box(x0, x1, 0.57, 0.72, -0.70, -0.45, "junk_primer")
    m.box(0.186, 0.196, 0.44, 0.56, -0.24, -0.10, "junk_dark")
    # 側殼纏兩圈銀色膠帶，左側再補一塊
    m.box(-0.199, 0.199, 0.52, 0.72, -0.40, -0.36, "junk_tape")
    m.box(-0.199, 0.199, 0.52, 0.72, -0.32, -0.28, "junk_tape")
    m.box(0.188, 0.199, 0.60, 0.68, -0.24, -0.12, "junk_tape")
    # 座墊後段破洞露出海綿，上面貼一條膠帶
    m.box(-0.08, 0.06, 0.845, 0.885, -0.74, -0.60, "junk_foam")
    m.box(-0.11, 0.09, 0.86, 0.892, -0.69, -0.66, "junk_tape")
    # 大燈、尾燈破一半
    m.box(0.0, 0.085, 0.795, 0.905, 0.64, 0.662, "junk_dark")
    m.box(-0.095, 0.0, 0.635, 0.745, -1.006, -0.99, "junk_dark")
    # 鏽斑：腳踏板側邊、前土除、側殼下緣
    for side in (1, -1):
        x0, x1 = sorted((side * 0.178, side * 0.186))
        m.box(x0, x1, 0.27, 0.33, 0.04, 0.22, "junk_rust")
    m.box(-0.06, 0.06, 0.527, 0.537, 0.60, 0.70, "junk_rust")
    m.box(-0.197, -0.186, 0.45, 0.52, -0.55, -0.35, "junk_rust")
    return m


def build_scooter_wheel():
    # 輪胎、銀色輪框、深色輪框內圈，上面五支輻條（兩面都看得到）和輪轂；輪子會轉，輻條能看出速度
    m = Model("scooter_wheel")
    m.cylinder_x(0, 0, 0.24, -0.055, 0.055, 14, "tire", "tire")
    m.cylinder_x(0, 0, 0.165, -0.058, 0.058, 14, "rim", "rim")
    m.cylinder_x(0, 0, 0.135, -0.060, 0.060, 14, "tire", "tire")
    for i in range(5):
        a = 2 * math.pi * i / 5
        dz, dy = math.cos(a), math.sin(a)
        nz, ny = -dy * 0.016, dz * 0.016
        m.extrude([(dz * 0.03 + nz, dy * 0.03 + ny), (dz * 0.14 + nz, dy * 0.14 + ny),
                   (dz * 0.14 - nz, dy * 0.14 - ny), (dz * 0.03 - nz, dy * 0.03 - ny)], -0.062, 0.062, "rim")
    m.cylinder_x(0, 0, 0.045, -0.066, 0.066, 8, "rim", "chrome")
    return m


def build_scooter_handlebar():
    # 原點 = 轉向軸心（龍頭轉動的中心）
    m = Model("scooter_handlebar")
    m.box(-0.035, 0.035, 0.0, 0.08, -0.035, 0.035, "trim")
    # 龍頭蓋：前窄後寬，上面是儀表
    m.loft([section(0.08, -0.13, 0.13, 0.07, 0.15, 0.03),
            section(0.0, -0.17, 0.17, 0.06, 0.17, 0.04),
            section(-0.08, -0.15, 0.15, 0.07, 0.17, 0.04),
            section(-0.11, -0.10, 0.10, 0.09, 0.15, 0.03)], "body")
    m.box(-0.08, 0.08, 0.16, 0.172, -0.08, 0.01, "trim")
    m.box(-0.07, 0.07, 0.165, 0.178, -0.07, 0.0, "meter")
    # 右側油門線
    m.limb((-0.20, 0.13, -0.03), (-0.06, 0.09, -0.10), 0.006, "trim", 4, 3)
    for side in (1, -1):
        # 握把（位置與舊版相同，騎士的手握在這裡）與電鍍端子
        x0, x1 = sorted((side * 0.16, side * 0.30))
        m.cylinder_x(0.12, -0.02, 0.022, x0, x1, 8, "seat", "seat")
        x0, x1 = sorted((side * 0.30, side * 0.315))
        m.cylinder_x(0.12, -0.02, 0.026, x0, x1, 8, "chrome", "chrome")
        # 煞車拉桿
        m.limb((side * 0.17, 0.135, 0.0), (side * 0.28, 0.125, 0.05), 0.008, "rim", 4, 3)
        # 後照鏡：鏡桿加鏡子，鏡面朝後
        m.limb((side * 0.15, 0.16, -0.02), (side * 0.24, 0.34, -0.04), 0.01, "trim", 4, 3)
        m.ellipsoid((side * 0.26, 0.36, -0.04), (0.055, 0.035, 0.012), 8, 4, "trim")
        m.ellipsoid((side * 0.26, 0.36, -0.053), (0.045, 0.027, 0.004), 8, 4, "mirror")
    return m


def build_npc_rider():
    # 原點與 scooter_body 相同：坐姿騎士，雙手握龍頭
    m = Model("npc_rider")
    for side in (1, -1):
        # 腿：大腿、小腿、鞋子踩在腳踏板上
        hip, knee, ankle = (side * 0.10, 0.90, -0.30), (side * 0.15, 0.95, 0.12), (side * 0.14, 0.44, 0.16)
        m.limb(hip, knee, 0.075, "rider_pants")
        m.limb(knee, ankle, 0.055, "rider_pants")
        x0, x1 = sorted((side * 0.09, side * 0.19))
        m.box(x0, x1, 0.36, 0.44, 0.08, 0.28, "trim")
        # 手：肩膀、上臂、前臂，手握在握把上
        shoulder, elbow, wrist = (side * 0.21, 1.40, -0.18), (side * 0.26, 1.22, 0.16), (side * 0.23, 1.12, 0.44)
        m.ellipsoid((side * 0.19, 1.40, -0.20), (0.07, 0.07, 0.07), 6, 4, "rider_jacket")
        m.limb(shoulder, elbow, 0.055, "rider_jacket")
        m.limb(elbow, wrist, 0.045, "rider_jacket")
        m.ellipsoid((side * 0.23, 1.10, 0.49), (0.04, 0.04, 0.045), 6, 4, "rider_skin")
    # 軀幹：微微前傾、邊緣倒角
    m.extrude([(-0.42, 0.88), (-0.18, 0.88), (-0.06, 1.30), (-0.10, 1.46), (-0.30, 1.48), (-0.40, 1.30)], -0.19, 0.19, "rider_jacket")
    # 脖子、頭、安全帽、深色鏡片、口罩
    m.limb((0, 1.46, -0.22), (0, 1.55, -0.22), 0.045, "rider_skin")
    m.ellipsoid((0, 1.62, -0.20), (0.11, 0.12, 0.11), 8, 6, "rider_skin")
    m.ellipsoid((0, 1.64, -0.21), (0.14, 0.14, 0.14), 12, 12, "rider_helmet", pitch=-10, rows=7)
    m.ellipsoid((0, 1.625, -0.085), (0.10, 0.05, 0.03), 8, 4, "glass")
    m.ellipsoid((0, 1.555, -0.11), (0.07, 0.045, 0.03), 8, 4, "rider_mask")
    return m


def build_car():
    # 轎車：長 4.5 公尺，車頭朝 +Z，輪子直接做在車身上（NPC 不需要轉輪）
    # 倒角車身、玻璃車艙、A/B/C 柱、保險桿、水箱護罩、後照鏡、前後車牌、內凹輪框
    m = Model("car")
    m.loft([section(2.22, -0.80, 0.80, 0.42, 0.72, 0.10),
            section(2.05, -0.88, 0.88, 0.32, 0.86, 0.12),
            section(1.10, -0.90, 0.90, 0.30, 0.96, 0.12),
            section(-1.60, -0.90, 0.90, 0.30, 0.98, 0.12),
            section(-2.10, -0.88, 0.88, 0.34, 0.92, 0.12),
            section(-2.22, -0.80, 0.80, 0.44, 0.84, 0.10)], "car_paint")
    m.loft([section(1.10, -0.80, 0.80, 0.95, 1.00, 0.04),
            section(0.45, -0.76, 0.76, 0.95, 1.42, 0.06),
            section(-1.00, -0.76, 0.76, 0.96, 1.45, 0.06),
            section(-1.70, -0.80, 0.80, 0.97, 1.02, 0.04)], "car_glass")
    m.loft([section(0.50, -0.70, 0.70, 1.41, 1.47, 0.03),
            section(-1.05, -0.70, 0.70, 1.44, 1.48, 0.03)], "car_paint")
    for side in (1, -1):
        x0, x1 = sorted((side * 0.72, side * 0.79))
        m.extrude([(1.10, 0.96), (1.00, 0.96), (0.45, 1.43), (0.55, 1.43)], x0, x1, "car_paint")
        m.box(x0, x1, 0.96, 1.44, -0.22, -0.10, "car_paint")
        m.extrude([(-1.00, 1.45), (-1.12, 1.45), (-1.72, 0.99), (-1.60, 0.99)], x0, x1, "car_paint")
        # 後照鏡
        x0, x1 = sorted((side * 0.88, side * 0.945))
        m.box(x0, x1, 0.94, 1.04, 0.95, 1.05, "car_paint")
    for z in (1.35, -1.35):
        for side in (1, -1):
            x0, x1 = sorted((side * 0.76, side * 0.93))
            m.cylinder_x(0.32, z, 0.32, x0, x1, 10, "tire", "tire")
            x0, x1 = sorted((side * 0.90, side * 0.945))
            m.cylinder_x(0.32, z, 0.19, x0, x1, 8, "rim", "rim")
    m.box(-0.86, 0.86, 0.30, 0.48, 2.16, 2.28, "trim")
    m.box(-0.86, 0.86, 0.32, 0.50, -2.28, -2.16, "trim")
    m.box(-0.45, 0.45, 0.52, 0.66, 2.20, 2.235, "trim")
    m.box(-0.22, 0.22, 0.34, 0.45, 2.27, 2.285, "plate")
    m.box(-0.22, 0.22, 0.56, 0.67, -2.235, -2.22, "plate")
    for side in (1, -1):
        x0, x1 = sorted((side * 0.52, side * 0.80))
        m.box(x0, x1, 0.62, 0.72, 2.12, 2.235, "headlight")
        m.box(x0, x1, 0.70, 0.80, -2.235, -2.12, "taillight")
    return m


def build_car_parked():
    # 路邊停的轎車：原本的簡單版本（會被合併進世界區塊，面數要少）
    m = Model("car_parked")
    m.extrude([(-2.2, 0.35), (2.2, 0.35), (2.25, 0.6), (2.1, 0.85), (1.2, 0.95),
               (-1.6, 0.98), (-2.2, 0.9), (-2.25, 0.6)], -0.88, 0.88, "car_paint")
    m.extrude([(1.1, 0.95), (0.4, 1.42), (-1.1, 1.45), (-1.75, 0.98)], -0.78, 0.78, "car_glass")
    m.box(-0.72, 0.72, 1.42, 1.48, -1.05, 0.35, "car_paint")
    for z in (1.35, -1.35):
        for side in (1, -1):
            x0, x1 = sorted((side * 0.76, side * 0.95))
            m.cylinder_x(0.32, z, 0.32, x0, x1, 10, "tire", "rim")
    for side in (1, -1):
        x0, x1 = sorted((side * 0.55, side * 0.80))
        m.box(x0, x1, 0.62, 0.74, 2.18, 2.27, "headlight")
        m.box(x0, x1, 0.70, 0.80, -2.27, -2.18, "taillight")
    return m


def build_truck():
    # 大貨車：長 8.6 公尺、高 3.4 公尺
    # 車頭上緣倒角、側窗、大後照鏡、貨斗鋁條肋、側邊防撞桿、油箱、擋泥板
    m = Model("truck")
    m.extrude([(2.6, 0.6), (4.25, 0.6), (4.25, 2.55), (4.05, 2.75), (2.6, 2.75)], -1.2, 1.2, "truck_cab")
    m.box(-1.05, 1.05, 1.7, 2.45, 4.25, 4.29, "car_glass")
    m.box(-0.9, 0.9, 0.7, 1.3, 4.25, 4.3, "trim")
    m.box(-1.25, 1.25, 0.45, 0.7, 4.2, 4.35, "trim")
    m.box(-1.0, 1.0, 0.55, 0.85, -4.25, 2.6, "trim")
    m.box(-1.25, 1.25, 0.9, 3.4, -4.25, 2.45, "truck_cargo")
    for side in (1, -1):
        x0, x1 = sorted((side * 1.2, side * 1.21))
        m.box(x0, x1, 1.75, 2.45, 3.3, 4.0, "car_glass")
        # 後照鏡：支架加鏡子
        x0, x1 = sorted((side * 1.2, side * 1.29))
        m.box(x0, x1, 2.0, 2.05, 3.9, 3.95, "trim")
        x0, x1 = sorted((side * 1.24, side * 1.29))
        m.box(x0, x1, 1.6, 2.2, 3.85, 3.95, "trim")
        # 貨斗鋁條肋
        x0, x1 = sorted((side * 1.25, side * 1.27))
        for z in (-3.6, -2.0, -0.4, 1.2):
            m.box(x0, x1, 0.95, 3.35, z - 0.06, z + 0.06, "rim")
        # 兩軸之間的側邊防撞桿
        x0, x1 = sorted((side * 1.1, side * 1.2))
        m.box(x0, x1, 0.55, 0.62, -1.8, 2.6, "rim")
        # 後輪後方的擋泥板
        x0, x1 = sorted((side * 0.95, side * 1.25))
        m.box(x0, x1, 0.25, 0.8, -4.12, -4.08, "trim")
    m.box(0.8, 1.12, 0.45, 0.8, 1.5, 2.3, "trim")
    for z in (3.3, -2.4, -3.5):
        for side in (1, -1):
            x0, x1 = sorted((side * 0.95, side * 1.25))
            m.cylinder_x(0.5, z, 0.5, x0, x1, 10, "tire", "rim")
    for side in (1, -1):
        x0, x1 = sorted((side * 0.8, side * 1.1))
        m.box(x0, x1, 0.8, 0.95, 4.28, 4.33, "headlight")
        m.box(x0, x1, 0.9, 1.1, -4.3, -4.25, "taillight")
    return m


def build_bus():
    # 市區公車：長 10.5 公尺、高 3.2 公尺，下半部綠色、上半部白色加整排車窗
    # 加上窗柱、右側車門、車頂冷氣、前方路線 LED 板、後照鏡
    m = Model("bus")
    m.box(-1.25, 1.25, 0.45, 1.45, -5.25, 5.25, "bus_paint")
    m.box(-1.25, 1.25, 1.45, 3.0, -5.25, 5.25, "bus_white")
    m.box(-1.27, 1.27, 1.65, 2.6, -4.6, 4.2, "car_glass")
    m.box(-1.1, 1.1, 1.3, 2.55, 5.25, 5.29, "car_glass")
    m.box(-1.0, 1.0, 1.7, 2.6, -5.29, -5.25, "car_glass")
    m.box(-0.8, 0.8, 2.62, 2.82, 5.25, 5.29, "bus_led")
    m.box(-1.2, 1.2, 3.0, 3.1, -5.0, 5.0, "trim")
    m.box(-0.8, 0.8, 3.1, 3.24, -1.5, 0.8, "bus_white")
    for side in (1, -1):
        # 窗柱把整排玻璃分成一格一格
        x0, x1 = sorted((side * 1.27, side * 1.28))
        for z in (-3.4, -2.2, -1.0, 0.2, 1.4, 2.6, 3.4):
            m.box(x0, x1, 1.62, 2.63, z - 0.05, z + 0.05, "bus_white")
        # 後照鏡往前伸
        x0, x1 = sorted((side * 1.18, side * 1.28))
        m.box(x0, x1, 2.4, 2.45, 5.2, 5.3, "trim")
        m.box(x0, x1, 1.9, 2.4, 5.25, 5.3, "trim")
    # 右側車門（OBJ 的 -X 匯入 Unity 後在右側）
    m.box(-1.28, -1.265, 0.5, 2.62, 3.85, 4.75, "trim")
    m.box(-1.29, -1.275, 0.6, 2.55, 3.92, 4.68, "car_glass")
    for z in (3.6, -3.4):
        for side in (1, -1):
            x0, x1 = sorted((side * 0.95, side * 1.26))
            m.cylinder_x(0.5, z, 0.5, x0, x1, 10, "tire", "rim")
    for side in (1, -1):
        x0, x1 = sorted((side * 0.8, side * 1.1))
        m.box(x0, x1, 0.7, 0.85, 5.24, 5.3, "headlight")
        m.box(x0, x1, 0.8, 1.0, -5.3, -5.24, "taillight")
    return m


def build_tree():
    # Low-poly 行道樹：細樹幹加三團樹冠，下方的樹冠用暗綠色做出層次（會被合併進世界，面數要少）
    m = Model("tree")
    m.frustum_y([0.0, 2.4], ["tree_trunk"], lambda y: 0.16 - 0.03 * y / 2.4, 6)
    m.ellipsoid((0, 3.2, 0), (1.5, 1.1, 1.5), 6, 4, lambda d: "tree_leaf_dark" if d[1] < -0.2 else "tree_leaf")
    m.ellipsoid((0.45, 4.1, -0.25), (1.0, 0.85, 1.0), 6, 4, "tree_leaf")
    m.ellipsoid((-0.5, 3.9, 0.4), (0.85, 0.75, 0.85), 6, 4, "tree_leaf")
    return m


def build_tree_palm():
    # 大王椰子：分節的細高樹幹，頂端一圈下垂的羽狀葉
    m = Model("tree_palm")
    m.frustum_y([0.0, 2.0, 4.0, 6.0, 7.5], ["palm_trunk"] * 4, lambda y: 0.18 - 0.06 * y / 7.5, 6)
    m.ellipsoid((0, 7.6, 0), (0.28, 0.3, 0.28), 4, 3, "tree_leaf_dark")
    for i in range(6):
        a = 2 * math.pi * i / 6
        dx, dz = math.cos(a), math.sin(a)
        px, pz = -dz * 0.18, dx * 0.18
        base = [(px * 0.6, 7.65, pz * 0.6), (dx * 0.3 + px, 7.7, dz * 0.3 + pz),
                (dx * 0.3 - px, 7.7, dz * 0.3 - pz), (-px * 0.6, 7.65, -pz * 0.6)]
        m.pyramid(base, (dx * 2.3, 6.8, dz * 2.3), "palm_leaf")
    return m


def build_tree_broad():
    # 樟樹型：短樹幹、寬而扁的樹冠
    m = Model("tree_broad")
    m.frustum_y([0.0, 1.9], ["tree_trunk"], lambda y: 0.18 - 0.04 * y / 1.9, 6)
    m.ellipsoid((0, 3.0, 0), (2.3, 1.1, 2.3), 7, 4, lambda d: "tree_leaf_dark" if d[1] < -0.2 else "tree_leaf")
    m.ellipsoid((0.4, 3.8, 0.3), (1.5, 0.8, 1.5), 6, 4, "tree_leaf")
    return m


def build_street_light():
    # 單臂路燈：細桿往上收、頂端一支往 +Z 伸出的燈臂與自發光燈罩（會被合併進世界，面數要少）
    m = Model("street_light")
    m.frustum_y([0.0, 6.5], ["lamp_pole"], lambda y: 0.09 - 0.03 * y / 6.5, 6)
    m.box(-0.03, 0.03, 6.38, 6.46, 0.0, 1.55, "lamp_pole")
    m.box(-0.13, 0.13, 6.32, 6.42, 1.3, 1.85, "lamp_pole")
    m.box(-0.11, 0.11, 6.28, 6.32, 1.34, 1.81, "lamp_head")
    return m


def build_mailbox():
    # 台灣郵筒：左紅（限時）右綠（普通），各有底座與加寬的頂蓋
    m = Model("mailbox")
    for x0, x1, mat in ((0.02, 0.44, "mailbox_red"), (-0.44, -0.02, "mailbox_green")):
        m.box(x0 + 0.05, x1 - 0.05, 0.0, 0.25, -0.15, 0.15, "trim")
        m.box(x0, x1, 0.25, 1.2, -0.2, 0.2, mat)
        m.box(x0 - 0.02, x1 + 0.02, 1.2, 1.3, -0.22, 0.22, mat)
    return m


def build_hydrant():
    # 地上式消防栓：底座、柱身、頂蓋，兩側出水口
    m = Model("hydrant")
    m.frustum_y([0.0, 0.08, 0.65, 0.75], ["hydrant"] * 3,
                lambda y: 0.14 if y < 0.04 else (0.11 if y < 0.5 else (0.10 if y < 0.7 else 0.06)), 6)
    m.box(-0.18, 0.18, 0.44, 0.52, -0.04, 0.04, "hydrant")
    return m


def build_bus_shelter():
    # 公車亭：頂棚、兩根柱子、背板玻璃、長椅
    m = Model("bus_shelter")
    m.box(-1.6, 1.6, 2.5, 2.62, -0.65, 0.65, "shelter_roof")
    for x in (-1.45, 1.45):
        m.box(x - 0.05, x + 0.05, 0.0, 2.5, -0.55, -0.45, "lamp_pole")
    m.box(-1.4, 1.4, 0.3, 2.4, -0.53, -0.49, "glass")
    m.box(-1.1, 1.1, 0.42, 0.47, -0.45, -0.1, "trim")
    return m


def build_scooter_parked():
    # 路邊停的機車：參照 scooter_body 輪廓的低模，輪子直接做在車身上（會被合併進世界，面數要少）
    # 保留 body 材質，世界建置時會換成隨機車色
    m = Model("scooter_parked")
    m.loft([section(0.30, -0.21, 0.21, 0.32, 0.60, 0.05),
            section(0.48, -0.20, 0.20, 0.34, 0.98, 0.06),
            section(0.645, -0.10, 0.10, 0.72, 0.96, 0.04)], "body")
    m.box(-0.18, 0.18, 0.26, 0.36, -0.08, 0.36, "trim")
    m.loft([section(0.06, -0.17, 0.17, 0.36, 0.74, 0.04),
            section(-0.70, -0.19, 0.19, 0.49, 0.79, 0.04),
            section(-0.985, -0.08, 0.08, 0.63, 0.76, 0.02)], "body")
    m.loft([section(0.06, -0.14, 0.14, 0.73, 0.79, 0.02),
            section(-0.54, -0.17, 0.17, 0.76, 0.86, 0.03),
            section(-0.89, -0.11, 0.11, 0.78, 0.85, 0.02)], "seat")
    for z in (0.65, -0.65):
        m.cylinder_x(0.24, z, 0.24, -0.055, 0.055, 8, "tire", "tire")
    m.box(-0.30, 0.30, 1.05, 1.13, 0.47, 0.55, "trim")
    m.box(-0.08, 0.08, 0.80, 0.90, 0.62, 0.655, "headlight")
    m.box(-0.09, 0.09, 0.64, 0.74, -1.00, -0.96, "taillight")
    return m


def build_traffic_cone():
    m = Model("traffic_cone")
    m.box(-0.20, 0.20, 0.0, 0.03, -0.20, 0.20, "cone_base")
    bottom, top, r_bottom, r_top = 0.03, 0.73, 0.16, 0.035

    def radius_at(y):
        return r_bottom + (r_top - r_bottom) * (y - bottom) / (top - bottom)

    m.frustum_y([0.03, 0.28, 0.38, 0.50, 0.58, 0.73],
                ["cone_red", "cone_white", "cone_red", "cone_white", "cone_red"],
                radius_at, 10)
    return m


def build_penguin_rider():
    # 原點與 scooter_body 相同：趴在座墊上，頭從龍頭後面探出來
    m = Model("penguin_rider")
    black, white = "penguin_black", "penguin_white"
    # 身體：肚子貼著座墊，前端微微抬起
    m.ellipsoid((0, 0.97, -0.24), (0.22, 0.16, 0.35), 10, 7,
                lambda d: white if d[1] < -0.05 else black, pitch=-12)
    # 頭：大頭比較可愛，正面是白色臉
    m.ellipsoid((0, 1.18, 0.18), (0.17, 0.17, 0.17), 10, 7,
                lambda d: white if d[2] > 0.35 and d[1] < 0.75 else black)
    for side in (1, -1):
        # 眼睛 + 反光點
        m.ellipsoid((side * 0.065, 1.23, 0.325), (0.026, 0.030, 0.016), 6, 4, "penguin_eye")
        m.ellipsoid((side * 0.065 + 0.009, 1.243, 0.340), (0.008, 0.008, 0.004), 4, 3, white)
        # 腮紅
        m.ellipsoid((side * 0.11, 1.16, 0.305), (0.030, 0.016, 0.012), 6, 4, "penguin_blush")
        # 翅膀：往前伸去握龍頭
        m.ellipsoid((side * 0.21, 1.06, 0.24), (0.035, 0.05, 0.26), 8, 5, black,
                    pitch=-9, yaw=side * 8)
        # 腳：往後伸出，腳底朝上
        m.box(side * 0.09 - 0.05, side * 0.09 + 0.05, 0.87, 0.91, -0.80, -0.55, "penguin_orange")
    # 嘴巴
    m.pyramid([(-0.03, 1.16, 0.33), (0.03, 1.16, 0.33), (0.03, 1.20, 0.33), (-0.03, 1.20, 0.33)],
              (0, 1.175, 0.43), "penguin_orange")
    # 安全帽：往後傾斜的半罩帽，前緣在眼睛上方，中間一條白線
    m.ellipsoid((0, 1.18, 0.18), (0.185, 0.185, 0.185), 12, 12,
                lambda d: "helmet_stripe" if abs(d[0]) < 0.3 * math.hypot(d[0], d[2]) else "helmet",
                pitch=-15, rows=5)
    m.ellipsoid((0, 1.272, 0.36), (0.12, 0.012, 0.05), 8, 4, "helmet_visor", pitch=15)
    # 背上的外送保溫箱（配合送貨模式，不放品牌標誌）與兩條肩帶
    m.loft([section(-0.52, -0.17, 0.17, 1.07, 1.37, 0.03),
            section(-0.18, -0.17, 0.17, 1.10, 1.40, 0.03)], "delivery_box")
    for side in (1, -1):
        x0, x1 = sorted((side * 0.10, side * 0.13))
        m.box(x0, x1, 1.04, 1.12, -0.18, 0.02, "trim")
    return m


def build_bajiu_rider():
    # 原點與 scooter_body 相同：8+9 騎姿——往後靠、膝蓋大開的八字腿、右手催油門、左手搭在膝蓋上
    m = Model("bajiu_rider")
    skin = "bajiu_skin"

    def sleeve(wrist=False):
        # 包手：黑灰寫實風，墨黑與灰色交錯成不規則花紋，透出膚色當亮面；手腕以下不刺
        def mat_for(d):
            if wrist and d[2] > 0.7:
                return skin
            # 三組不同頻率的波疊加成雜訊，花紋才會成團、不規則，不會變成螺旋條紋
            x, y, z = d
            k = (math.sin(7.1 * x + 3.3 * z + 0.4) + math.sin(5.7 * y - 6.1 * z + 1.7)
                 + math.sin(4.3 * x + 4.9 * y + 9.2 * z + 0.6))
            if k > 1.0:
                return skin
            if k > 0.1:
                return "bajiu_tattoo_gray"
            return "bajiu_tattoo"
        return mat_for

    for side in (1, -1):
        hip, knee, ankle = (side * 0.11, 0.90, -0.32), (side * 0.31, 0.94, 0.08), (side * 0.13, 0.44, 0.12)
        m.limb(hip, knee, 0.085, "bajiu_shorts", 8, 5)
        m.limb(knee, ankle, 0.055, skin)
        # 藍白拖：藍色鞋底、白色中層、腳掌、藍色鞋帶
        x0, x1 = sorted((side * 0.085, side * 0.175))
        m.box(x0, x1, 0.360, 0.372, 0.06, 0.27, "bajiu_slipper_blue")
        m.box(x0, x1, 0.372, 0.385, 0.06, 0.27, "bajiu_slipper_white")
        m.box(x0 + 0.005, x1 - 0.005, 0.385, 0.42, 0.07, 0.26, skin)
        m.box(x0 - 0.003, x1 + 0.003, 0.40, 0.43, 0.16, 0.21, "bajiu_slipper_blue")
        # 吊嘎外露出的肩膀：包手一路刺到肩頭
        m.ellipsoid((side * 0.20, 1.38, -0.32), (0.075, 0.075, 0.075), 8, 6, sleeve())
    # 上半身：白吊嘎往後靠，下緣是褲頭
    m.extrude([(-0.46, 0.82), (-0.14, 0.82), (-0.20, 1.42), (-0.46, 1.45)], -0.18, 0.18, "bajiu_tank")
    m.box(-0.19, 0.19, 0.80, 0.95, -0.48, -0.12, "bajiu_shorts")
    # 粗金項鍊：從後頸沿著肩膀繞到胸前，垂成 U 字，底下掛一塊金牌
    chain = [(0, 1.458, -0.42), (0.07, 1.455, -0.39), (0.09, 1.447, -0.31), (0.07, 1.435, -0.22),
             (0.065, 1.425, -0.192), (0.055, 1.37, -0.182), (0.03, 1.30, -0.172), (0, 1.275, -0.168)]
    for side in (1, -1):
        points = [(side * x, y, z) for x, y, z in chain]
        for a, b in zip(points, points[1:]):
            m.limb(a, b, 0.013, "bajiu_gold", 4, 3)
    m.box(-0.022, 0.022, 1.215, 1.27, -0.178, -0.162, "bajiu_gold")
    # 右手（OBJ 的 -X，匯入 Unity 後在右側）：手肘外開去握油門
    shoulder, elbow, grip = (-0.22, 1.38, -0.32), (-0.33, 1.20, 0.05), (-0.23, 1.10, 0.50)
    m.limb(shoulder, elbow, 0.055, sleeve(), 12, 10)
    m.limb(elbow, grip, 0.048, sleeve(wrist=True), 12, 10)
    m.ellipsoid(grip, (0.045, 0.045, 0.05), 6, 4, skin)
    # 左手：垂下來搭在膝蓋上
    shoulder, elbow, hand = (0.22, 1.38, -0.32), (0.31, 1.10, -0.22), (0.31, 1.05, 0.04)
    m.limb(shoulder, elbow, 0.055, sleeve(), 12, 10)
    m.limb(elbow, hand, 0.048, sleeve(wrist=True), 12, 10)
    m.ellipsoid(hand, (0.045, 0.045, 0.05), 6, 4, skin)
    # 脖子、頭
    m.limb((0, 1.40, -0.33), (0, 1.52, -0.36), 0.05, skin)
    head = (0, 1.60, -0.37)
    m.ellipsoid(head, (0.115, 0.13, 0.12), 8, 6, skin)
    # 金色刺蝟頭：往後傾的頭髮蓋子 + 一根根往上翹的尖刺
    m.ellipsoid((0, 1.615, -0.38), (0.125, 0.13, 0.13), 10, 8, "bajiu_hair", pitch=-25, rows=3)
    spikes = [(0, 0.04, 0, 0.12, 0.05), (0.06, -0.01, 0.05, 0.10, 0), (-0.06, -0.01, -0.05, 0.10, 0),
              (0, -0.06, 0, 0.11, -0.06), (0.05, -0.09, 0.04, 0.07, -0.07), (-0.05, -0.09, -0.04, 0.07, -0.07)]
    for bx, bz, tx, ty, tz in spikes:
        by = head[1] + 0.13 * math.sqrt(max(0.0, 1 - (bx / 0.115) ** 2 - (bz / 0.12) ** 2)) - 0.02
        cx, cz, h = bx, head[2] + bz, 0.035
        m.pyramid([(cx - h, by, cz - h), (cx + h, by, cz - h), (cx + h, by, cz + h), (cx - h, by, cz + h)],
                  (cx + tx, by + ty, cz + tz), "bajiu_hair")
    # 墨鏡（含鏡腳）
    m.box(-0.10, 0.10, 1.605, 1.645, -0.262, -0.240, "bajiu_shades")
    for side in (1, -1):
        x0, x1 = sorted((side * 0.10, side * 0.118))
        m.box(x0, x1, 1.62, 1.632, -0.36, -0.25, "bajiu_shades")
    # 嘴角叼菸
    m.box(0.022, 0.036, 1.528, 1.542, -0.28, -0.20, "bajiu_cig")
    m.box(0.022, 0.036, 1.528, 1.542, -0.20, -0.188, "bajiu_ember")
    return m


def build_mtl():
    lines = ["# palette - generated by tools/generate_models.py"]
    for name, hex_color in MATERIALS.items():
        r, g, b = hex_to_rgb(hex_color)
        smooth, metal, emit = SURFACE.get(name, DEFAULT_SURFACE)
        lines += [f"newmtl {name}", "Ka 0 0 0", f"Kd {r:.4f} {g:.4f} {b:.4f}",
                  "Ks 0.05 0.05 0.05", f"Ns {smooth * 1000:.0f}"]
        if metal > 0:
            lines.append(f"Pm {metal:.2f}")
        if emit > 0:
            lines.append(f"Ke {r * emit:.4f} {g * emit:.4f} {b * emit:.4f}")
        lines += ["illum 2", "d 1", ""]
    return "\n".join(lines)


PREVIEW_TEMPLATE = """<!doctype html>
<html lang="zh-Hant">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Model Preview</title>
<style>
  html, body { margin: 0; height: 100%; overflow: hidden; background: #dfe6ec; font-family: sans-serif; }
  #info { position: absolute; top: 12px; left: 12px; background: rgba(255,255,255,.88); padding: 8px 12px; border-radius: 8px; font-size: 14px; line-height: 1.6; }
</style>
<script type="importmap">
{ "imports": {
  "three": "https://cdn.jsdelivr.net/npm/three@0.160.0/build/three.module.js",
  "three/addons/": "https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/"
} }
</script>
</head>
<body>
<div id="info">
  左鍵拖曳旋轉 · 滾輪縮放 · 右鍵平移<br>
  <label><input type="checkbox" id="anim" checked> 輪子轉動、龍頭擺動</label><br>
  <label><input type="checkbox" id="wire"> 顯示三角形線框</label>
</div>
<script type="module">
import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { OBJLoader } from 'three/addons/loaders/OBJLoader.js';
import { MTLLoader } from 'three/addons/loaders/MTLLoader.js';

const DATA = __DATA__;

const scene = new THREE.Scene();
scene.background = new THREE.Color(0xdfe6ec);
const camera = new THREE.PerspectiveCamera(45, innerWidth / innerHeight, 0.05, 100);
camera.position.set(2.4, 1.5, 2.4);
const renderer = new THREE.WebGLRenderer({ antialias: true });
renderer.setPixelRatio(devicePixelRatio);
renderer.setSize(innerWidth, innerHeight);
document.body.appendChild(renderer.domElement);
const controls = new OrbitControls(camera, renderer.domElement);
controls.target.set(0.4, 0.5, 0);
controls.update();

scene.add(new THREE.HemisphereLight(0xffffff, 0x667788, 1.2));
const sun = new THREE.DirectionalLight(0xffffff, 1.6);
sun.position.set(3, 5, 2);
scene.add(sun);
scene.add(new THREE.GridHelper(6, 24, 0x8899aa, 0xb8c4ce));

const materials = new MTLLoader().parse(DATA.mtl, '');
materials.preload();
const load = (name) => new OBJLoader().setMaterials(materials).parse(DATA.obj[name]);

const scooter = new THREE.Group();
scene.add(scooter);
scooter.add(load('scooter_body'));
const front = load('scooter_wheel');
front.position.set(__FRONT__);
front.rotation.order = 'YXZ';
scooter.add(front);
const rear = load('scooter_wheel');
rear.position.set(__REAR__);
scooter.add(rear);
const bar = load('scooter_handlebar');
bar.position.set(__BAR__);
scooter.add(bar);
scooter.add(load('penguin_rider'));
const cone = load('traffic_cone');
cone.position.set(1.2, 0, 0.3);
scene.add(cone);

// NPC 車輛：一般騎士、轎車、大貨車
const npc = new THREE.Group();
npc.position.set(-1.6, 0, 0);
scene.add(npc);
['scooter_body', 'npc_rider'].forEach((name) => npc.add(load(name)));
[[__FRONT__], [__REAR__]].forEach((pos) => { const w = load('scooter_wheel'); w.position.set(...pos); npc.add(w); });
const npcBar = load('scooter_handlebar');
npcBar.position.set(__BAR__);
npc.add(npcBar);
// 8+9 騎士：改裝車、破爛車
[['bajiu_scooter_tuned', 2.0], ['bajiu_scooter_junk', 3.0]].forEach(([body, x]) => {
  const bajiu = new THREE.Group();
  bajiu.position.set(x, 0, 0);
  scene.add(bajiu);
  [body, 'bajiu_rider'].forEach((name) => bajiu.add(load(name)));
  [[__FRONT__], [__REAR__]].forEach((pos) => { const w = load('scooter_wheel'); w.position.set(...pos); bajiu.add(w); });
  const bajiuBar = load('scooter_handlebar');
  bajiuBar.position.set(__BAR__);
  bajiu.add(bajiuBar);
});
const car = load('car');
car.position.set(-4.2, 0, 0);
scene.add(car);
const truck = load('truck');
truck.position.set(-8.2, 0, 0);
scene.add(truck);

// 公車、路邊停車、樹、路燈與街頭小物
[['bus', -13, 0], ['scooter_parked', 1.6, 3.5], ['car_parked', -4.2, 6], ['street_light', 4, 3.5], ['mailbox', 5.6, 3.5],
 ['hydrant', 6.6, 3.5], ['bus_shelter', 9.5, 3.5], ['tree', 4, 8], ['tree_palm', 8.5, 8], ['tree_broad', 14, 8]].forEach(([name, x, z]) => {
  const item = load(name);
  item.position.set(x, 0, z);
  scene.add(item);
});

const anim = document.getElementById('anim');
document.getElementById('wire').addEventListener('change', (e) => {
  scene.traverse((o) => { if (o.isMesh) [].concat(o.material).forEach((m) => { m.wireframe = e.target.checked; }); });
});
const clock = new THREE.Clock();
renderer.setAnimationLoop(() => {
  const t = clock.getElapsedTime();
  if (anim.checked) {
    front.rotation.x = rear.rotation.x = t * 4;
    const steer = Math.sin(t * 1.5) * 0.35;
    bar.rotation.y = steer;
    front.rotation.y = steer;
  }
  renderer.render(scene, camera);
});
addEventListener('resize', () => {
  camera.aspect = innerWidth / innerHeight;
  camera.updateProjectionMatrix();
  renderer.setSize(innerWidth, innerHeight);
});
</script>
</body>
</html>
"""

# 零件在 Unity 裡組裝時的位置（相對於 scooter_body 的原點），需與 Assets/Editor/PrototypeSceneBuilder.cs 一致
FRONT_WHEEL_POS = (0, 0.24, 0.65)
REAR_WHEEL_POS = (0, 0.24, -0.65)
HANDLEBAR_POS = (0, 0.98, 0.52)


def main():
    OUT.mkdir(exist_ok=True)
    models = [build_scooter_body(), build_scooter_wheel(), build_scooter_handlebar(),
              build_penguin_rider(), build_traffic_cone(), build_npc_rider(), build_car(), build_truck(),
              build_bus(), build_tree(), build_bajiu_rider(), build_bajiu_scooter_tuned(), build_bajiu_scooter_junk(),
              build_car_parked(), build_scooter_parked(), build_tree_palm(), build_tree_broad(), build_street_light(),
              build_mailbox(), build_hydrant(), build_bus_shelter()]
    mtl = build_mtl()
    (OUT / "palette.mtl").write_text(mtl, encoding="utf-8")
    obj_texts = {}
    for model in models:
        text = model.to_obj()
        obj_texts[model.name] = text
        (OUT / f"{model.name}.obj").write_text(text, encoding="utf-8")
        print(f"{model.name}.obj: {model.triangle_count()} triangles")

    to_js = lambda pos: ", ".join(str(v) for v in pos)
    html = (PREVIEW_TEMPLATE
            .replace("__DATA__", json.dumps({"mtl": mtl, "obj": obj_texts}))
            .replace("__FRONT__", to_js(FRONT_WHEEL_POS))
            .replace("__REAR__", to_js(REAR_WHEEL_POS))
            .replace("__BAR__", to_js(HANDLEBAR_POS)))
    PREVIEW.write_text(html, encoding="utf-8")
    print(f"preview: {PREVIEW}")


if __name__ == "__main__":
    main()
