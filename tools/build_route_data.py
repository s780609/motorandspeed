"""把 OpenStreetMap 道路資料轉成遊戲用的五股路線（Assets/Data/wugu_route.json）。

路線：成泰路三段577巷 → 成泰路三段 → 成泰路二段 → 成泰路二段91巷
座標：x = 東、z = 北（公尺），原點為路線起點。
資料來源：tools/data/wugu_osm.json，© OpenStreetMap contributors（ODbL）。
執行：python tools/build_route_data.py
"""
import heapq
import json
import math
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "tools" / "data" / "wugu_osm.json"
OUTPUT = ROOT / "Assets" / "Data" / "wugu_route.json"

MAIN_NAMES = {"成泰路二段", "成泰路三段"}
START_LANE = "成泰路三段577巷"
END_LANE = "成泰路二段91巷"
# 起點、終點離成泰路口的距離（公尺）
START_LANE_DISTANCE = 60.0
END_LANE_DISTANCE = 50.0
# 依街景：每向一個汽車道加機車騎乘空間與路邊停車格，總寬約 14 公尺
MAIN_WIDTH = 14.0
LANE_WIDTH = 7.0
# 主線在兩端路口外多延伸一段，看起來才像 T 字路口
EXTENSION = 60.0
SAMPLE_SPACING = 4.0
WIDTH_BY_TYPE = {
    "trunk": 20.0, "primary": 20.0, "secondary": 16.0, "tertiary": 12.0,
    "residential": 7.0, "unclassified": 7.0, "living_street": 6.0,
}
LAT0, LON0 = 25.093, 121.445


def to_xy(lat, lon):
    return ((lon - LON0) * 111320.0 * math.cos(math.radians(LAT0)), (lat - LAT0) * 110540.0)


def polyline_length(points):
    return sum(math.dist(points[i], points[i + 1]) for i in range(len(points) - 1))


def cut_polyline(points, distance):
    """沿著折線走 distance 公尺，回傳走過的部分。"""
    result = [points[0]]
    walked = 0.0
    for a, b in zip(points, points[1:]):
        step = math.dist(a, b)
        if walked + step >= distance:
            t = (distance - walked) / step
            result.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
            return result
        result.append(b)
        walked += step
    return result


def resample(points, spacing):
    total = polyline_length(points)
    count = max(2, int(total / spacing) + 1)
    result = []
    for i in range(count):
        target = total * i / (count - 1)
        result.append(cut_polyline(points, target)[-1] if target > 0 else points[0])
    return result


def smooth(points, radius=2):
    result = list(points)
    for i in range(radius, len(points) - radius):
        window = points[i - radius:i + radius + 1]
        result[i] = (sum(p[0] for p in window) / len(window), sum(p[1] for p in window) / len(window))
    return result


def unit(dx, dy):
    length = math.hypot(dx, dy) or 1.0
    return (dx / length, dy / length)


def shortest_path(edges, start, goal):
    queue = [(0.0, start, None)]
    previous = {}
    while queue:
        cost, node, prev = heapq.heappop(queue)
        if node in previous:
            continue
        previous[node] = prev
        if node == goal:
            break
        for neighbor, weight in edges[node]:
            if neighbor not in previous:
                heapq.heappush(queue, (cost + weight, neighbor, node))
    path = [goal]
    while previous[path[-1]] is not None:
        path.append(previous[path[-1]])
    return path[::-1]


def start_lane_points(ways, position):
    """起點巷子（577巷）從起點到成泰路口的折線；第一個點就是遊戲座標的原點。"""
    main_nodes = {n for w in ways if w["tags"].get("name") in MAIN_NAMES for n in w["nodes"]}
    start_lane = next(w for w in ways if w["tags"].get("name") == START_LANE and set(w["nodes"]) & main_nodes)
    junction_a = next(n for n in start_lane["nodes"] if n in main_nodes)
    lane_a = [position[n] for n in start_lane["nodes"]]
    if math.dist(lane_a[0], position[junction_a]) < 1.0:
        lane_a.reverse()

    return cut_polyline(lane_a[::-1], START_LANE_DISTANCE)[::-1]


def load_osm():
    """讀取 OSM 資料，回傳 (道路清單, 節點座標, 紅綠燈座標)。"""
    data = json.loads(SOURCE.read_text(encoding="utf-8"))
    ways = [e for e in data["elements"] if e["type"] == "way"]
    signals = [to_xy(e["lat"], e["lon"]) for e in data["elements"] if e["type"] == "node"]
    position = {}
    for way in ways:
        for node, geo in zip(way["nodes"], way["geometry"]):
            position[node] = to_xy(geo["lat"], geo["lon"])

    return ways, position, signals


def main():
    ways, position, signals = load_osm()
    ways_at_node = defaultdict(list)
    for way in ways:
        for node in way["nodes"]:
            ways_at_node[node].append(way)

    main_ways = [w for w in ways if w["tags"].get("name") in MAIN_NAMES]
    main_nodes = {n for w in main_ways for n in w["nodes"]}
    start_lane = next(w for w in ways if w["tags"].get("name") == START_LANE and set(w["nodes"]) & main_nodes)
    end_lane = next(w for w in ways if w["tags"].get("name") == END_LANE and set(w["nodes"]) & main_nodes)
    junction_a = next(n for n in start_lane["nodes"] if n in main_nodes)
    junction_b = next(n for n in end_lane["nodes"] if n in main_nodes)

    # 主線：在成泰路的路網上找 A → B 最短路徑；不同路段端點相距 30 公尺內視為相連
    edges = defaultdict(list)
    for way in main_ways:
        for a, b in zip(way["nodes"], way["nodes"][1:]):
            d = math.dist(position[a], position[b])
            edges[a].append((b, d))
            edges[b].append((a, d))
    endpoints = {n for w in main_ways for n in (w["nodes"][0], w["nodes"][-1])}
    for a in endpoints:
        for b in endpoints:
            if a != b and math.dist(position[a], position[b]) < 30.0:
                edges[a].append((b, math.dist(position[a], position[b])))
    path_nodes = shortest_path(edges, junction_a, junction_b)
    core = [position[n] for n in path_nodes]

    # 兩端各直線延伸一段
    head_dir = unit(core[0][0] - core[1][0], core[0][1] - core[1][1])
    tail_dir = unit(core[-1][0] - core[-2][0], core[-1][1] - core[-2][1])
    extended = ([(core[0][0] + head_dir[0] * EXTENSION, core[0][1] + head_dir[1] * EXTENSION)]
                + core
                + [(core[-1][0] + tail_dir[0] * EXTENSION, core[-1][1] + tail_dir[1] * EXTENSION)])
    main_points = smooth(resample(extended, SAMPLE_SPACING))
    cumulative = [0.0]
    for a, b in zip(main_points, main_points[1:]):
        cumulative.append(cumulative[-1] + math.dist(a, b))

    def project(point):
        best = min(range(len(main_points)), key=lambda i: math.dist(main_points[i], point))
        return cumulative[best]

    # 巷子：起點在 577巷內往成泰路騎，終點在 91巷內
    lane_a = start_lane_points(ways, position)
    lane_b = [position[n] for n in end_lane["nodes"]]
    if math.dist(lane_b[-1], position[junction_b]) < 1.0:
        lane_b.reverse()
    lane_b = cut_polyline(lane_b, END_LANE_DISTANCE)

    # 路口：主線節點上接了其他道路的地方
    raw = []
    for node in path_nodes:
        roads = []
        for way in ways_at_node[node]:
            tags = way["tags"]
            if tags.get("name") in MAIN_NAMES or tags.get("highway") not in WIDTH_BY_TYPE:
                continue
            nodes = way["nodes"]
            index = nodes.index(node)
            coords = [position[n] for n in nodes]
            for neighbor_side in (coords[index::-1], coords[index:]):
                if len(neighbor_side) < 2:
                    continue
                tip = cut_polyline(neighbor_side, 15.0)[-1]
                roads.append({
                    "name": tags.get("name", ""),
                    "width": WIDTH_BY_TYPE[tags["highway"]],
                    "dir": unit(tip[0] - position[node][0], tip[1] - position[node][1]),
                    "route": tags.get("name") in (START_LANE, END_LANE),
                })
        if roads:
            raw.append((project(position[node]), roads))

    intersections = []
    for s, roads in sorted(raw):
        if intersections and s - intersections[-1]["s"] < 20.0:
            intersections[-1]["roads"] += roads
            continue
        intersections.append({"s": s, "roads": roads})

    for item in intersections:
        index = min(range(len(cumulative)), key=lambda i: abs(cumulative[i] - item["s"]))
        a, b = main_points[max(0, index - 1)], main_points[min(len(main_points) - 1, index + 1)]
        tangent = unit(b[0] - a[0], b[1] - a[1])
        merged = []
        for road in sorted(item["roads"], key=lambda r: -r["width"]):
            # 跟主線平行的是主線本身的延續，略過；方向相近的視為同一條路
            if abs(road["dir"][0] * tangent[0] + road["dir"][1] * tangent[1]) > 0.9:
                continue
            if any(m["dir"][0] * road["dir"][0] + m["dir"][1] * road["dir"][1] > 0.85 for m in merged):
                continue
            merged.append(road)
        item["roads"] = merged
        item["point"] = main_points[index]
        item["signal"] = any(math.dist(sig, item["point"]) < 35.0 for sig in signals)
    intersections = [i for i in intersections if i["roads"]]

    # 原點移到起點
    origin = lane_a[0]

    def local(p):
        return {"x": round(p[0] - origin[0], 2), "z": round(p[1] - origin[1], 2)}

    start_dir = unit(lane_a[1][0] - lane_a[0][0], lane_a[1][1] - lane_a[0][1])
    result = {
        "attribution": "© OpenStreetMap contributors (ODbL)",
        "main": {
            "name": "成泰路",
            "width": MAIN_WIDTH,
            "points": [local(p) for p in main_points],
            "routeStart": round(project(position[junction_a]), 2),
            "routeEnd": round(project(position[junction_b]), 2),
        },
        "lanes": [
            {"name": START_LANE, "width": LANE_WIDTH, "points": [local(p) for p in resample(lane_a, SAMPLE_SPACING)]},
            {"name": END_LANE, "width": LANE_WIDTH, "points": [local(p) for p in resample(lane_b, SAMPLE_SPACING)]},
        ],
        "intersections": [
            {
                "name": "、".join(r["name"] for r in i["roads"] if r["name"]) or "無名路口",
                "s": round(i["s"], 2),
                "x": local(i["point"])["x"],
                "z": local(i["point"])["z"],
                "signal": i["signal"],
                "roads": [
                    {"name": r["name"], "width": r["width"], "dirX": round(r["dir"][0], 4), "dirZ": round(r["dir"][1], 4), "route": r["route"]}
                    for r in i["roads"]
                ],
            }
            for i in intersections
        ],
        "start": {"x": 0.0, "z": 0.0, "dirX": round(start_dir[0], 4), "dirZ": round(start_dir[1], 4)},
    }

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(result, ensure_ascii=False, indent=1), encoding="utf-8")
    route_length = START_LANE_DISTANCE + result["main"]["routeEnd"] - result["main"]["routeStart"] + END_LANE_DISTANCE
    print(f"main: {len(main_points)} points, {cumulative[-1]:.0f} m (route on main {result['main']['routeStart']:.0f} → {result['main']['routeEnd']:.0f})")
    print(f"route total ≈ {route_length:.0f} m, intersections: {len(intersections)}, signals: {sum(i['signal'] for i in intersections)}")
    for i in result["intersections"]:
        print(f"  s={i['s']:7.1f} {'[燈]' if i['signal'] else '    '} {i['name']}  roads={len(i['roads'])}")


if __name__ == "__main__":
    main()
