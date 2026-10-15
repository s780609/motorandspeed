"""把 OpenStreetMap 道路轉成五股世界路網（Assets/Data/wugu_world.json）。

內容：路段與路口（含紅綠燈）、高架橋（成蘆橋前半段）、洲子洋重劃區範圍、送貨地點、衝刺賽與繞圈賽路線。
座標與 wugu_route.json 相同：x = 東、z = 北（公尺），原點為 577巷 起點。
資料來源：tools/data/wugu_osm.json，© OpenStreetMap contributors（ODbL）。
執行：python tools/build_world_data.py（需先執行 build_route_data.py）
"""
import heapq
import json
import math
import random
from collections import defaultdict
from pathlib import Path

import build_route_data as route

ROOT = Path(__file__).resolve().parent.parent
ROUTE_JSON = ROOT / "Assets" / "Data" / "wugu_route.json"
OUTPUT = ROOT / "Assets" / "Data" / "wugu_world.json"

# 可騎的道路與寬度；高架快速道路（trunk、motorway）不做
# 五股很多巷弄在 OSM 標成 service，一起收進來；只排除停車場走道和私人車道
WIDTH = {"primary": 16.0, "secondary": 14.0, "tertiary": 12.0, "residential": 7.0, "unclassified": 7.0, "living_street": 6.0, "service": 5.0}
SERVICE_SKIP = {"driveway", "parking_aisle", "drive-through"}
# 高架橋：不進平面路網，另外輸出成可騎的橋面；只做前半段
BRIDGE_NAMES = {"成蘆橋"}
BRIDGE_FRACTION = 0.5
BRIDGE_LANES_WIDTH = 9.0
MAIN_TYPES = {"primary", "secondary", "tertiary"}
MAIN_ROAD_NAMES = {"成泰路二段", "成泰路三段"}
SAMPLE_SPACING = 4.0
SIGNAL_RADIUS = 25.0
NEW_TOWN_RANGE = (420.0, 1700.0)
NEW_TOWN_DEPTH = 400.0
DESTINATION_COUNT = 40
CIRCUIT_TARGET = 3000.0
# 世界範圍（緯度、經度），約 2.2 × 3 公里；超出的道路會被裁掉
BOUNDS = (25.080, 121.434, 25.107, 121.456)


def dijkstra(graph, start, goal, banned=frozenset()):
    """graph[node] = [(neighbor, length, edge_id)]；回傳 (總長, 路段清單)，找不到回傳 (inf, [])。"""
    queue = [(0.0, start, None, None)]
    previous = {}
    while queue:
        cost, node, prev, edge = heapq.heappop(queue)
        if node in previous:
            continue
        previous[node] = (prev, edge, cost)
        if node == goal:
            break
        for neighbor, length, edge_id in graph[node]:
            if neighbor not in previous and edge_id not in banned:
                heapq.heappush(queue, (cost + length, neighbor, node, edge_id))
    if goal not in previous:
        return math.inf, []
    edges = []
    node = goal
    while previous[node][0] is not None:
        edges.append(previous[node][1])
        node = previous[node][0]
    return previous[goal][2], edges[::-1]


def chain(edge_ids, edges, start_junction):
    """把一串路段依行進方向接成一條折線。"""
    points = []
    junction = start_junction
    for edge_id in edge_ids:
        edge = edges[edge_id]
        pts = edge["points"] if edge["start"] == junction else edge["points"][::-1]
        junction = edge["end"] if edge["start"] == junction else edge["start"]
        points.extend(pts if not points else pts[1:])
    return points


def build_bridge(name, bridge_ways, position, origin):
    """雙向分開的兩條橋取中線合成一座橋面，從五股端起只取前 BRIDGE_FRACTION。"""
    lines = [[position[n] for n in w["nodes"]] for w in bridge_ways if w["tags"].get("name") == name]
    # 以最西邊的端點當五股端，兩條線都整理成從五股端出發
    west = min((line[0] if line[0][0] < line[-1][0] else line[-1]) for line in lines)
    lines = [line if math.dist(line[0], west) < math.dist(line[-1], west) else line[::-1] for line in lines]
    a = route.resample(lines[0], SAMPLE_SPACING)
    b = route.resample(lines[1], SAMPLE_SPACING)
    middle = []
    gaps = []
    for p in a:
        q = min(b, key=lambda c: math.dist(p, c))
        gaps.append(math.dist(p, q))
        middle.append(((p[0] + q[0]) / 2.0, (p[1] + q[1]) / 2.0))
    middle = route.smooth(middle)
    full = route.polyline_length(middle)
    half = route.cut_polyline(middle, full * BRIDGE_FRACTION)
    gaps.sort()
    width = round(gaps[len(gaps) // 2] + BRIDGE_LANES_WIDTH, 1)

    return {
        "name": name,
        "width": width,
        "full": full,
        "points": [(p[0] - origin[0], p[1] - origin[1]) for p in route.resample(half, SAMPLE_SPACING)],
    }


def main():
    ways, position, signals = route.load_osm()
    origin = route.start_lane_points(ways, position)[0]
    # 只留範圍內的道路：跨出邊界的道路切成範圍內的幾段
    min_x, min_z = route.to_xy(BOUNDS[0], BOUNDS[1])
    max_x, max_z = route.to_xy(BOUNDS[2], BOUNDS[3])

    def inside(node):
        x, z = position[node]
        return min_x <= x <= max_x and min_z <= z <= max_z

    kept = []
    bridge_ways = []
    for way in ways:
        tags = way["tags"]
        if tags.get("highway") not in WIDTH:
            continue
        if tags.get("name") in BRIDGE_NAMES and tags.get("bridge"):
            bridge_ways.append(way)
            continue
        if tags["highway"] == "service" and tags.get("service") in SERVICE_SKIP:
            continue
        run = []
        for node in way["nodes"] + [None]:
            if node is not None and inside(node):
                run.append(node)
                continue
            if len(run) >= 2:
                kept.append({"tags": way["tags"], "nodes": run})
            run = []

    # 在路口（被兩條以上道路共用的節點）把道路切成路段
    usage = defaultdict(int)
    for way in kept:
        for node in set(way["nodes"]):
            usage[node] += 1
    raw_edges = []
    for way in kept:
        nodes = way["nodes"]
        start = 0
        for i in range(1, len(nodes)):
            if usage[nodes[i]] > 1 or i == len(nodes) - 1:
                if i > start:
                    raw_edges.append((way, nodes[start:i + 1]))
                start = i

    # 只保留連通的最大路網
    parent = {}

    def find(n):
        parent.setdefault(n, n)
        while parent[n] != n:
            parent[n] = parent[parent[n]]
            n = parent[n]
        return n

    for _, nodes in raw_edges:
        parent[find(nodes[0])] = find(nodes[-1])
    sizes = defaultdict(int)
    for _, nodes in raw_edges:
        sizes[find(nodes[0])] += 1
    biggest = max(sizes, key=sizes.get)
    raw_edges = [(w, n) for w, n in raw_edges if find(n[0]) == biggest and n[0] != n[-1]]

    junction_ids = {}
    junctions = []
    edges = []
    service_edges = set()

    def junction_of(node):
        if node not in junction_ids:
            junction_ids[node] = len(junctions)
            x, z = position[node]
            junctions.append({"id": len(junctions), "x": x - origin[0], "z": z - origin[1], "signal": False, "edges": []})
        return junction_ids[node]

    for way, nodes in raw_edges:
        tags = way["tags"]
        pts = [position[n] for n in nodes]
        length = route.polyline_length(pts)
        if length < 1.0:
            continue
        a, b = junction_of(nodes[0]), junction_of(nodes[-1])
        oneway = {"yes": 1, "1": 1, "-1": -1}.get(tags.get("oneway", ""), 0)
        sampled = route.resample(pts, SAMPLE_SPACING) if length > SAMPLE_SPACING else pts
        edge = {
            "id": len(edges),
            "start": a,
            "end": b,
            "name": tags.get("name", ""),
            "main": tags["highway"] in MAIN_TYPES,
            "width": WIDTH[tags["highway"]],
            "oneway": oneway,
            "length": round(length, 2),
            "points": [(p[0] - origin[0], p[1] - origin[1]) for p in sampled],
        }
        edges.append(edge)
        if tags["highway"] == "service":
            service_edges.add(edge["id"])
        junctions[a]["edges"].append(edge["id"])
        junctions[b]["edges"].append(edge["id"])

    # 紅綠燈：附近有號誌、至少三岔、而且接到主要道路的路口（service 巷弄不算一岔，避免巷口多出號誌）
    local_signals = [(s[0] - origin[0], s[1] - origin[1]) for s in signals]
    for junction in junctions:
        incident = [edges[e] for e in junction["edges"] if e not in service_edges]
        if len(incident) >= 3 and any(e["main"] for e in incident):
            point = (junction["x"], junction["z"])
            junction["signal"] = any(math.dist(point, s) < SIGNAL_RADIUS for s in local_signals)

    bridges = [build_bridge(name, bridge_ways, position, origin) for name in BRIDGE_NAMES]

    # 洲子洋重劃區：成泰路東側（路線右手邊的反方向）往外 400 公尺
    route_data = json.loads(ROUTE_JSON.read_text(encoding="utf-8"))
    main_points = [(p["x"], p["z"]) for p in route_data["main"]["points"]]
    cumulative = [0.0]
    for a, b in zip(main_points, main_points[1:]):
        cumulative.append(cumulative[-1] + math.dist(a, b))
    near, far = [], []
    for i, s in enumerate(cumulative):
        if NEW_TOWN_RANGE[0] <= s <= NEW_TOWN_RANGE[1]:
            a, b = main_points[max(0, i - 1)], main_points[min(len(main_points) - 1, i + 1)]
            tx, tz = route.unit(b[0] - a[0], b[1] - a[1])
            east = (-tz, tx)
            near.append((main_points[i][0] + east[0] * 7.0, main_points[i][1] + east[1] * 7.0))
            far.append((main_points[i][0] + east[0] * NEW_TOWN_DEPTH, main_points[i][1] + east[1] * NEW_TOWN_DEPTH))
    new_town = near[::5] + far[::-5]

    # 送貨地點：長度 40 公尺以上的路段中點，用最遠點取樣讓地點平均分散
    rng = random.Random(15)
    candidates = []
    for edge in edges:
        if edge["length"] >= 40.0:
            pts = edge["points"]
            mid = route.cut_polyline(pts, edge["length"] / 2.0)[-1]
            candidates.append({"name": edge["name"] or "巷弄", "x": mid[0], "z": mid[1], "edge": edge["id"]})
    chosen = [rng.choice(candidates)]
    while len(chosen) < min(DESTINATION_COUNT, len(candidates)):
        best = max(candidates, key=lambda c: min(math.dist((c["x"], c["z"]), (d["x"], d["z"])) for d in chosen))
        chosen.append(best)

    # 衝刺賽：沿用 577巷 → 成泰路 → 91巷 路線
    lanes = route_data["lanes"]
    sprint = [(p["x"], p["z"]) for p in lanes[0]["points"]]
    for i, s in enumerate(cumulative):
        if route_data["main"]["routeStart"] < s < route_data["main"]["routeEnd"]:
            sprint.append(main_points[i])
    sprint += [(p["x"], p["z"]) for p in lanes[1]["points"]]

    # 繞圈賽：成泰路上取兩個路口，另一半用其他主要道路繞回來，挑總長最接近 3 公里的一圈
    graph = defaultdict(list)
    main_graph = defaultdict(list)
    for edge in edges:
        for u, v in ((edge["start"], edge["end"]), (edge["end"], edge["start"])):
            graph[u].append((v, edge["length"], edge["id"]))
            if edge["main"]:
                main_graph[u].append((v, edge["length"], edge["id"]))
    corridor_edges = {e["id"] for e in edges if e["name"] in MAIN_ROAD_NAMES}
    corridor_graph = defaultdict(list)
    for edge in edges:
        if edge["id"] in corridor_edges:
            corridor_graph[edge["start"]].append((edge["end"], edge["length"], edge["id"]))
            corridor_graph[edge["end"]].append((edge["start"], edge["length"], edge["id"]))
    stops = [j["id"] for j in junctions if j["id"] in corridor_graph and len(main_graph[j["id"]]) >= 3]
    best_loop = None
    for a in stops:
        for b in stops:
            if a >= b:
                continue
            along, along_edges = dijkstra(corridor_graph, a, b)
            if not 600.0 <= along <= 1600.0:
                continue
            back, back_edges = dijkstra(main_graph, b, a, frozenset(corridor_edges))
            total = along + back
            if back_edges and 1800.0 <= total <= 4500.0:
                score = abs(total - CIRCUIT_TARGET)
                if best_loop is None or score < best_loop[0]:
                    best_loop = (score, a, along_edges + back_edges, total)
    if best_loop is None:
        raise SystemExit("找不到合適的繞圈賽路線")
    circuit = chain(best_loop[2], edges, best_loop[1])

    def pts(points):
        return [{"x": round(p[0], 2), "z": round(p[1], 2)} for p in points]

    start_lane = route.start_lane_points(ways, position)
    start_dir = route.unit(start_lane[1][0] - start_lane[0][0], start_lane[1][1] - start_lane[0][1])
    result = {
        "attribution": "© OpenStreetMap contributors (ODbL)",
        "junctions": [{**j, "x": round(j["x"], 2), "z": round(j["z"], 2)} for j in junctions],
        "edges": [{**e, "points": pts(e["points"])} for e in edges],
        "bridges": [{"name": b["name"], "width": b["width"], "points": pts(b["points"])} for b in bridges],
        "newTown": pts(new_town),
        "destinations": [{**d, "x": round(d["x"], 2), "z": round(d["z"], 2)} for d in chosen],
        "sprint": {"points": pts(route.resample(sprint, SAMPLE_SPACING))},
        "circuit": {"points": pts(route.resample(circuit, SAMPLE_SPACING))},
        "start": {"x": 0.0, "z": 0.0, "dirX": round(start_dir[0], 4), "dirZ": round(start_dir[1], 4)},
    }
    OUTPUT.write_text(json.dumps(result, ensure_ascii=False), encoding="utf-8")

    total_length = sum(e["length"] for e in edges)
    main_length = sum(e["length"] for e in edges if e["main"])
    xs = [p[0] for e in edges for p in e["points"]]
    zs = [p[1] for e in edges for p in e["points"]]
    print(f"路段 {len(edges)} 條，總長 {total_length / 1000:.1f} 公里（主要道路 {main_length / 1000:.1f} 公里）")
    print(f"路口 {len(junctions)} 個，紅綠燈 {sum(j['signal'] for j in junctions)} 個")
    print(f"範圍 {max(xs) - min(xs):.0f} × {max(zs) - min(zs):.0f} 公尺")
    print(f"送貨地點 {len(chosen)} 個，例如：{'、'.join(d['name'] for d in chosen[:6])}")
    print(f"衝刺賽 {route.polyline_length(sprint):.0f} 公尺；繞圈賽一圈 {best_loop[3]:.0f} 公尺，經過：{'、'.join(dict.fromkeys(edges[e]['name'] for e in best_loop[2] if edges[e]['name']))}")
    for b in bridges:
        print(f"{b['name']}：做 {route.polyline_length(b['points']):.0f} 公尺（全長 {b['full']:.0f} 公尺），橋面寬 {b['width']} 公尺")
    print(f"檔案 {OUTPUT.stat().st_size / 1024:.0f} KB")


if __name__ == "__main__":
    main()
