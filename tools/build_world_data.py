"""把 OpenStreetMap 道路轉成五股世界路網（Assets/Data/wugu_world.json）。

內容：路段與路口（含紅綠燈）、雙向分隔道路的分隔島、公園綠地、高架橋（成蘆橋前半段）、
洲子洋重劃區範圍、送貨地點、衝刺賽與繞圈賽路線。
座標與 wugu_route.json 相同：x = 東、z = 北（公尺），原點為 577巷 起點。
資料來源：tools/data/wugu_osm.json、tools/data/wugu_parks.json，© OpenStreetMap contributors（ODbL）。
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
PARKS_JSON = ROOT / "tools" / "data" / "wugu_parks.json"
OUTPUT = ROOT / "Assets" / "Data" / "wugu_world.json"

# 可騎的道路與寬度；高架快速道路（trunk、motorway）不做
# 五股很多巷弄在 OSM 標成 service，一起收進來；只排除停車場走道和私人車道
WIDTH = {"primary": 16.0, "secondary": 14.0, "tertiary": 12.0, "residential": 7.0, "unclassified": 7.0, "living_street": 6.0, "service": 5.0}
SERVICE_SKIP = {"driveway", "parking_aisle", "drive-through"}
# 高架橋：不進平面路網，另外輸出成可騎的橋面；只做前半段
BRIDGE_NAMES = {"成蘆橋"}
BRIDGE_FRACTION = 0.5
BRIDGE_LANES_WIDTH = 9.0
# 雙向分隔道路：OSM 把兩個方向畫成兩條單行線，中心線距離在這個範圍內、方向相反就當成一對
PAIR_GAP = (3.0, 30.0)
MEDIAN_MIN = 2.0
CARRIAGEWAY_MIN = 5.0
# 公園綠地太小的不算
PARK_MIN_AREA = 80.0
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


def nearest_on(points, p):
    """折線上離 p 最近的點與該處的方向。"""
    best = (math.inf, None, None)
    for a, b in zip(points, points[1:]):
        dx, dz = b[0] - a[0], b[1] - a[1]
        length2 = dx * dx + dz * dz
        t = 0.0 if length2 == 0 else max(0.0, min(1.0, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dz) / length2))
        q = (a[0] + dx * t, a[1] + dz * t)
        d = math.dist(p, q)
        if d < best[0]:
            best = (d, q, route.unit(dx, dz))
    return best


def travel_points(edge):
    """依行車方向排好的點（oneway = -1 的路段要反過來）。"""
    return edge["points"] if edge["oneway"] >= 0 else edge["points"][::-1]


def pair_carriageways(edges, junctions):
    """找出成對的單行線（雙向分隔道路），把車道縮窄到不重疊，回傳中間的分隔島。"""
    oneway = [e for e in edges if e["oneway"] != 0]
    matches = {}
    for e in oneway:
        pts = travel_points(e)
        found = []
        for i, p in enumerate(pts):
            a, b = pts[max(0, i - 1)], pts[min(len(pts) - 1, i + 1)]
            tangent = route.unit(b[0] - a[0], b[1] - a[1])
            best = None
            for f in oneway:
                if f is e:
                    continue
                d, q, direction = nearest_on(travel_points(f), p)
                # 方向要相反（另一個方向的車道）
                if PAIR_GAP[0] <= d <= PAIR_GAP[1] and tangent[0] * direction[0] + tangent[1] * direction[1] < -0.8:
                    if best is None or d < best[0]:
                        best = (d, q, f)
            found.append((p, tangent, best))
        matches[e["id"]] = found

    # 有六成以上的點找得到對向車道才算一對；兩邊依中心線距離縮窄，中間留分隔島
    paired = {}
    for e in oneway:
        found = matches[e["id"]]
        gaps = sorted(m[2][0] for m in found if m[2])
        if len(gaps) < 0.6 * len(found):
            continue
        # 取偏窄的距離（第 20 百分位），中心線距離變窄的地方才不會疊在一起
        gap = gaps[len(gaps) // 5]
        e["width"] = round(min(e["width"], max(CARRIAGEWAY_MIN, gap - MEDIAN_MIN)), 1)
        side_votes = 0.0
        for p, tangent, best in found:
            if best:
                right = (tangent[1], -tangent[0])
                side_votes += (best[1][0] - p[0]) * right[0] + (best[1][1] - p[1]) * right[1]
        # 輸出的點是原始順序；oneway = -1 時行車方向相反，左右也要對調
        side = 1 if side_votes > 0 else -1
        e["median"] = side if e["oneway"] > 0 else -side
        paired[e["id"]] = found

    # 路口附近不放分隔島，橫向的車才過得去
    radius = {}
    for j in junctions:
        radius[j["id"]] = max(edges[k]["width"] / 2.0 + 1.0 for k in j["edges"])
    crossings = [((j["x"], j["z"]), radius[j["id"]] + 3.0) for j in junctions if len(j["edges"]) >= 3]

    medians = []
    for e_id, found in paired.items():
        e = edges[e_id]
        run_points, run_widths = [], []
        for p, tangent, best in found + [(None, None, None)]:
            ok = False
            if best and best[2]["id"] in paired and e_id < best[2]["id"]:
                d, q, f = best
                width = d - e["width"] / 2.0 - f["width"] / 2.0
                middle = ((p[0] + q[0]) / 2.0, (p[1] + q[1]) / 2.0)
                if width >= 1.0 and all(math.dist(middle, c) > r for c, r in crossings):
                    run_points.append(middle)
                    run_widths.append(width)
                    ok = True
            if not ok:
                if len(run_points) >= 3 and route.polyline_length(run_points) >= 8.0:
                    medians.append({"points": run_points, "widths": run_widths})
                run_points, run_widths = [], []
    return medians


def polygon_area(points):
    return abs(sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(points, points[1:] + points[:1]))) / 2.0


def clip_polygon(points, min_x, min_z, max_x, max_z):
    """Sutherland–Hodgman：把多邊形裁到矩形範圍內。"""
    def clip(poly, inside, cross):
        out = []
        for i, cur in enumerate(poly):
            prev = poly[i - 1]
            if inside(cur):
                if not inside(prev):
                    out.append(cross(prev, cur))
                out.append(cur)
            elif inside(prev):
                out.append(cross(prev, cur))
        return out

    def at_x(x):
        return lambda a, b: (x, a[1] + (b[1] - a[1]) * (x - a[0]) / (b[0] - a[0]))

    def at_z(z):
        return lambda a, b: (a[0] + (b[0] - a[0]) * (z - a[1]) / (b[1] - a[1]), z)

    poly = list(points)
    for inside, cross in ((lambda p: p[0] >= min_x, at_x(min_x)), (lambda p: p[0] <= max_x, at_x(max_x)),
                          (lambda p: p[1] >= min_z, at_z(min_z)), (lambda p: p[1] <= max_z, at_z(max_z))):
        if not poly:
            break
        poly = clip(poly, inside, cross)
    return poly


def build_parks(origin, edges):
    """公園、運動場、草地：範圍內不蓋房子，畫成綠地。"""
    data = json.loads(PARKS_JSON.read_text(encoding="utf-8"))
    xs = [p[0] for e in edges for p in e["points"]]
    zs = [p[1] for e in edges for p in e["points"]]
    rect = (min(xs) - 80.0, min(zs) - 80.0, max(xs) + 80.0, max(zs) + 80.0)
    parks = []
    for element in data["elements"]:
        if element["type"] != "way":
            continue
        ring = [route.to_xy(g["lat"], g["lon"]) for g in element["geometry"]]
        if len(ring) < 4 or math.dist(ring[0], ring[-1]) > 0.5:
            continue
        ring = [(x - origin[0], z - origin[1]) for x, z in ring[:-1]]
        ring = clip_polygon(ring, *rect)
        if len(ring) >= 3 and polygon_area(ring) >= PARK_MIN_AREA:
            tags = element["tags"]
            kind = tags.get("leisure") or tags.get("landuse")
            parks.append({"name": tags.get("name", kind), "kind": kind, "points": ring})
    return parks


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
            # 分隔島在哪一側：1 右手邊、-1 左手邊、0 沒有（pair_carriageways 填入）
            "median": 0,
            "points": [(p[0] - origin[0], p[1] - origin[1]) for p in sampled],
        }
        edges.append(edge)
        if tags["highway"] == "service":
            service_edges.add(edge["id"])
        junctions[a]["edges"].append(edge["id"])
        junctions[b]["edges"].append(edge["id"])

    medians = pair_carriageways(edges, junctions)

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
        "medians": [{"points": pts(m["points"]), "widths": [round(w, 2) for w in m["widths"]]} for m in medians],
        "parks": [{"name": park["name"], "kind": park["kind"], "points": pts(park["points"])} for park in build_parks(origin, edges)],
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
    print(f"分隔島 {len(medians)} 段，共 {sum(route.polyline_length(m['points']) for m in medians) / 1000:.1f} 公里；"
          f"公園綠地 {len(result['parks'])} 塊")
    for b in bridges:
        print(f"{b['name']}：做 {route.polyline_length(b['points']):.0f} 公尺（全長 {b['full']:.0f} 公尺），橋面寬 {b['width']} 公尺")
    print(f"檔案 {OUTPUT.stat().st_size / 1024:.0f} KB")


if __name__ == "__main__":
    main()
