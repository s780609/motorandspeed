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


def build_scooter_body():
    m = Model("scooter_body")
    # 後車殼（包住引擎、座墊下方）
    m.extrude([(-0.92, 0.62), (-0.85, 0.52), (-0.35, 0.50), (-0.05, 0.34),
               (0.08, 0.36), (0.08, 0.70), (-0.88, 0.74)], -0.19, 0.19, "body")
    # 座墊
    m.extrude([(-0.82, 0.73), (0.06, 0.70), (0.04, 0.79), (-0.12, 0.82),
               (-0.45, 0.80), (-0.80, 0.86)], -0.17, 0.17, "seat")
    # 腳踏板
    m.box(-0.17, 0.17, 0.28, 0.36, -0.05, 0.32, "trim")
    # 前擋板（車頭）
    m.extrude([(0.28, 0.30), (0.40, 0.34), (0.47, 0.56), (0.58, 0.80),
               (0.62, 0.98), (0.52, 1.00), (0.36, 0.88), (0.28, 0.45)], -0.22, 0.22, "body")
    # 前土除
    m.box(-0.07, 0.07, 0.50, 0.55, 0.52, 0.80, "body")
    # 前叉（左右兩支）
    fork = [(0.63, 0.23), (0.67, 0.25), (0.52, 0.68), (0.48, 0.66)]
    m.extrude(fork, 0.06, 0.09, "trim")
    m.extrude(fork, -0.09, -0.06, "trim")
    # 引擎與後搖臂（OBJ 的 +X 匯入 Unity 後會在車身左側）
    m.box(0.06, 0.16, 0.15, 0.38, -0.70, -0.10, "trim")
    # 大燈、尾燈
    m.box(-0.09, 0.09, 0.84, 0.92, 0.58, 0.63, "headlight")
    m.box(-0.10, 0.10, 0.60, 0.67, -0.95, -0.89, "taillight")
    return m


def build_scooter_wheel():
    m = Model("scooter_wheel")
    m.cylinder_x(0, 0, 0.24, -0.055, 0.055, 14, "tire", "tire")
    m.cylinder_x(0, 0, 0.15, -0.058, 0.058, 14, "rim", "rim")
    return m


def build_scooter_handlebar():
    # 原點 = 轉向軸心（龍頭轉動的中心）
    m = Model("scooter_handlebar")
    m.box(-0.035, 0.035, 0.0, 0.08, -0.035, 0.035, "trim")
    m.box(-0.17, 0.17, 0.07, 0.17, -0.09, 0.07, "body")
    m.box(-0.08, 0.08, 0.17, 0.185, -0.05, 0.03, "trim")
    for side in (1, -1):
        x0, x1 = sorted((side * 0.16, side * 0.30))
        m.cylinder_x(0.12, -0.02, 0.022, x0, x1, 8, "seat", "seat")
    return m


def build_npc_rider():
    # 原點與 scooter_body 相同：坐姿騎士，雙手握龍頭
    m = Model("npc_rider")
    for side in (1, -1):
        x0, x1 = sorted((side * 0.05, side * 0.17))
        m.box(x0, x1, 0.80, 0.95, -0.35, 0.18, "rider_pants")
        m.box(x0, x1, 0.36, 0.90, 0.08, 0.20, "rider_pants")
        m.box(x0, x1, 0.36, 0.44, 0.08, 0.30, "seat")
        m.ellipsoid((side * 0.23, 1.25, 0.17), (0.05, 0.05, 0.36), 6, 4, "rider_jacket", pitch=23)
    m.extrude([(-0.45, 0.90), (-0.18, 0.90), (-0.08, 1.45), (-0.35, 1.47)], -0.19, 0.19, "rider_jacket")
    m.ellipsoid((0, 1.60, -0.18), (0.12, 0.12, 0.12), 8, 6, "rider_skin")
    m.ellipsoid((0, 1.62, -0.19), (0.145, 0.145, 0.145), 10, 12, "rider_helmet", pitch=-10, rows=7)
    return m


def build_car():
    # 轎車：長 4.5 公尺，車頭朝 +Z，輪子直接做在車身上（NPC 不需要轉輪）
    m = Model("car")
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
    m = Model("truck")
    m.box(-1.2, 1.2, 0.6, 2.7, 2.6, 4.25, "truck_cab")
    m.box(-1.05, 1.05, 1.7, 2.5, 4.25, 4.29, "car_glass")
    m.box(-0.9, 0.9, 0.7, 1.3, 4.25, 4.3, "trim")
    m.box(-1.25, 1.25, 0.45, 0.7, 4.2, 4.35, "trim")
    m.box(-1.0, 1.0, 0.55, 0.85, -4.25, 2.6, "trim")
    m.box(-1.25, 1.25, 0.9, 3.4, -4.25, 2.45, "truck_cargo")
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
    m = Model("bus")
    m.box(-1.25, 1.25, 0.45, 1.45, -5.25, 5.25, "bus_paint")
    m.box(-1.25, 1.25, 1.45, 3.05, -5.25, 5.25, "bus_white")
    m.box(-1.27, 1.27, 1.65, 2.6, -4.6, 4.2, "car_glass")
    m.box(-1.1, 1.1, 1.3, 2.75, 5.25, 5.29, "car_glass")
    m.box(-1.2, 1.2, 3.05, 3.2, -5.0, 5.0, "trim")
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
    # Low-poly 行道樹：細樹幹加兩團樹冠
    m = Model("tree")
    m.frustum_y([0.0, 2.4], ["tree_trunk"], lambda y: 0.16 - 0.03 * y / 2.4, 6)
    m.ellipsoid((0, 3.3, 0), (1.5, 1.2, 1.5), 7, 5, "tree_leaf")
    m.ellipsoid((0.3, 4.2, -0.2), (1.0, 0.9, 1.0), 6, 4, "tree_leaf")
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
    return m


def build_mtl():
    lines = ["# palette - generated by tools/generate_models.py"]
    for name, hex_color in MATERIALS.items():
        r, g, b = hex_to_rgb(hex_color)
        lines += [f"newmtl {name}", "Ka 0 0 0", f"Kd {r:.4f} {g:.4f} {b:.4f}",
                  "Ks 0.05 0.05 0.05", "Ns 10", "illum 2", "d 1", ""]
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
const car = load('car');
car.position.set(-4.2, 0, 0);
scene.add(car);
const truck = load('truck');
truck.position.set(-8.2, 0, 0);
scene.add(truck);

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
              build_bus(), build_tree()]
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
