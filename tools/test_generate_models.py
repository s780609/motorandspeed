"""generate_models.py 的測試：掛載點、換色材質、面數預算、尺寸符合碰撞體、材質 PBR 參數。
執行：python -B -m unittest discover -s tools -p "test_*.py"
"""
import math
import re
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import generate_models as g  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent

# 模型 → (三角面上限, 材質數上限)；會被合併進世界區塊的模型（停放車、樹、路燈、街頭小物）最嚴格
BUDGETS = {
    "build_scooter_body": (1500, 12),
    "build_scooter_wheel": (320, 4),
    "build_scooter_handlebar": (560, 8),
    "build_penguin_rider": (1200, 11),
    "build_npc_rider": (900, 8),
    "build_bajiu_rider": (2100, 13),
    "build_bajiu_scooter_tuned": (1900, 17),
    "build_bajiu_scooter_junk": (1900, 17),
    "build_car": (800, 10),
    "build_truck": (900, 10),
    "build_bus": (900, 10),
    "build_traffic_cone": (160, 3),
    "build_scooter_parked": (320, 6),
    "build_car_parked": (260, 6),
    "build_tree": (150, 3),
    "build_tree_palm": (150, 3),
    "build_tree_broad": (150, 3),
    "build_street_light": (60, 3),
    "build_mailbox": (80, 4),
    "build_hydrant": (80, 3),
    "build_bus_shelter": (80, 4),
}


def build(name):
    return getattr(g, name)()


def points(model):
    return [p for pts, _ in model.faces for p in pts]


def bounds(model):
    pts = points(model)
    return [min(p[i] for p in pts) for i in range(3)], [max(p[i] for p in pts) for i in range(3)]


def materials(model):
    return {mat for _, mat in model.faces}


def parse_mtl(text):
    """newmtl 名稱 → {欄位: 數值列表}"""
    table, current = {}, None
    for line in text.splitlines():
        parts = line.split()
        if not parts:
            continue
        if parts[0] == "newmtl":
            current = table.setdefault(parts[1], {})
        elif current is not None:
            current[parts[0]] = [float(v) for v in parts[1:]]
    return table


class AnchorTests(unittest.TestCase):
    def test_anchor_constants_match_scene_builder(self):
        self.assertEqual(g.FRONT_WHEEL_POS, (0, 0.24, 0.65))
        self.assertEqual(g.REAR_WHEEL_POS, (0, 0.24, -0.65))
        self.assertEqual(g.HANDLEBAR_POS, (0, 0.98, 0.52))
        source = (ROOT / "Assets" / "Editor" / "PrototypeSceneBuilder.cs").read_text(encoding="utf-8")
        for text in ("new Vector3(0f, 0.24f, 0.65f)", "new Vector3(0f, 0.24f, -0.65f)", "new Vector3(0f, 0.98f, 0.52f)"):
            self.assertIn(text, source)


class MaterialNameTests(unittest.TestCase):
    def test_recolor_materials_present(self):
        # NPC 依這些材質名稱換色，名稱不能改
        expected = {
            "build_scooter_body": {"body"},
            "build_bajiu_scooter_tuned": {"body"},
            "build_bajiu_scooter_junk": {"body"},
            "build_scooter_parked": {"body"},
            "build_npc_rider": {"rider_jacket", "rider_helmet"},
            "build_car": {"car_paint"},
            "build_car_parked": {"car_paint"},
            "build_truck": {"truck_cab"},
            "build_bus": {"bus_paint"},
        }
        for name, mats in expected.items():
            with self.subTest(name):
                self.assertTrue(mats <= materials(build(name)), f"{name} 缺少 {mats - materials(build(name))}")

    def test_all_materials_defined(self):
        for name in BUDGETS:
            with self.subTest(name):
                self.assertTrue(materials(build(name)) <= set(g.MATERIALS), f"{name} 用到沒定義的材質")


class BudgetTests(unittest.TestCase):
    def test_triangle_and_material_budgets(self):
        for name, (tris, mats) in BUDGETS.items():
            with self.subTest(name):
                model = build(name)
                self.assertLessEqual(model.triangle_count(), tris, f"{name} 三角面 {model.triangle_count()}")
                self.assertLessEqual(len(materials(model)), mats, f"{name} 材質 {len(materials(model))}")

    def test_no_degenerate_faces(self):
        for name in BUDGETS:
            with self.subTest(name):
                for pts, _ in build(name).faces:
                    nx = ny = nz = 0.0
                    for i, (x1, y1, z1) in enumerate(pts):
                        x2, y2, z2 = pts[(i + 1) % len(pts)]
                        nx += (y1 - y2) * (z1 + z2)
                        ny += (z1 - z2) * (x1 + x2)
                        nz += (x1 - x2) * (y1 + y2)
                    self.assertGreater(math.sqrt(nx * nx + ny * ny + nz * nz), 1e-9)


class GeometryTests(unittest.TestCase):
    def test_wheel_touches_ground_at_axle_radius(self):
        low, _ = bounds(build("build_scooter_wheel"))
        self.assertAlmostEqual(low[1], -0.24, places=3)

    def test_scooter_fits_hull(self):
        # 車身不能碰地，寬長不能超出碰撞體：玩家機車寬 0.5，8+9 的 NPC 機車碰撞體寬 0.6（改裝車的大砲管會外凸）
        for name, half_width in (("build_scooter_body", 0.25), ("build_bajiu_scooter_tuned", 0.3), ("build_bajiu_scooter_junk", 0.3)):
            with self.subTest(name):
                low, high = bounds(build(name))
                self.assertGreaterEqual(low[1], 0.14)
                self.assertLessEqual(max(-low[0], high[0]), half_width)
                self.assertGreaterEqual(low[2], -1.05)
                self.assertLessEqual(high[2], 0.98)

    def test_riders_stand_on_floorboard(self):
        for name in ("build_npc_rider", "build_bajiu_rider", "build_penguin_rider"):
            with self.subTest(name):
                low, _ = bounds(build(name))
                self.assertGreaterEqual(low[1], 0.355)

    def test_npc_rider_hands_reach_grips(self):
        pts = points(build("build_npc_rider"))
        hx, hy, hz = g.HANDLEBAR_POS
        for side in (1, -1):
            grip = (side * 0.23, hy + 0.12, hz - 0.02)
            nearest = min(math.dist(p, grip) for p in pts)
            self.assertLess(nearest, 0.06, f"手離握把 {nearest:.3f} 公尺")

    def test_vehicle_dimensions_match_colliders(self):
        # (模型, 最大寬, 最大高, 長度下限, 長度上限)
        limits = [("build_car", 1.9, 1.55, 4.4, 4.6), ("build_car_parked", 1.9, 1.55, 4.4, 4.6),
                  ("build_truck", 2.6, 3.45, 8.4, 8.7), ("build_bus", 2.6, 3.25, 10.3, 10.65)]
        for name, width, height, short, long in limits:
            with self.subTest(name):
                low, high = bounds(build(name))
                self.assertLessEqual(high[0] - low[0], width)
                self.assertLessEqual(high[1], height)
                self.assertGreaterEqual(low[1], 0.0 - 1e-6)
                self.assertTrue(short <= high[2] - low[2] <= long, f"長 {high[2] - low[2]:.2f}")

    def test_trees_stand_on_ground_with_thin_trunk(self):
        # 樹的碰撞體是半徑 0.2 的膠囊
        for name in ("build_tree", "build_tree_palm", "build_tree_broad"):
            with self.subTest(name):
                model = build(name)
                low, _ = bounds(model)
                self.assertAlmostEqual(low[1], 0.0, places=3)
                trunk = [p for p in points(model) if p[1] < 0.5]
                self.assertTrue(trunk)
                self.assertLessEqual(max(math.hypot(p[0], p[2]) for p in trunk), 0.2)

    def test_street_furniture_is_small(self):
        for name, height in (("build_street_light", 8.0), ("build_mailbox", 1.6), ("build_hydrant", 1.0), ("build_bus_shelter", 3.0)):
            with self.subTest(name):
                low, high = bounds(build(name))
                self.assertAlmostEqual(low[1], 0.0, places=3)
                self.assertLessEqual(high[1], height)


class MtlTests(unittest.TestCase):
    def setUp(self):
        self.table = parse_mtl(g.build_mtl())

    def test_lights_are_emissive(self):
        for name in ("headlight", "taillight", "blinker", "tuned_neon", "bus_led", "meter", "lamp_head"):
            with self.subTest(name):
                self.assertIn("Ke", self.table[name])
                self.assertGreater(max(self.table[name]["Ke"]), 0.0)

    def test_metals_are_metallic(self):
        for name in ("rim", "tuned_chrome", "bajiu_gold", "chrome"):
            with self.subTest(name):
                self.assertGreaterEqual(self.table[name]["Pm"][0], 0.6)

    def test_paint_is_shinier_than_tire(self):
        self.assertGreater(self.table["body"]["Ns"][0], self.table["tire"]["Ns"][0])
        self.assertGreater(self.table["car_paint"]["Ns"][0], self.table["tire"]["Ns"][0])


if __name__ == "__main__":
    unittest.main()
