#!/usr/bin/env python3
"""Distance maps that let the OS-input playthroughs walk a field like a player would.

Reads the same layout grid the game builds from and writes scenarios/<route>.json:
for each goal, every walkable cell holds its walking distance to the goal area
(-1 when blocked). A scenario presses WASD toward the neighbouring cell with the
smaller distance, so it can recover from any position, including after a defeat.

    python3 Tools/Verification/make_route.py root-boundary
    python3 Tools/Verification/make_route.py stone-village
"""

import heapq
import json
import math
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TERRAIN = "#.=~p%cr"
# layout, output, prop spread (marker -> cells blocked left and right of the marker),
# goals: name -> (marker, (marker, index) or (x, y) world point; standing radius; gate/seal open)
CHAPTERS = {
    "root-boundary": (
        "root-boundary.txt", "route.json",
        {"H": 1, "F": 0, "E": 1, "L": 0, "G": 1, "C": 0, "T": 0, "N": 0, "A": 1, "I": 0, "V": 0},
        {"root": ("G", 2.0, False), "vine": ("V", 1.5, False), "brook": ("C", 1.5, False), "stones": ("T", 1.5, False),
         "warden": ("W", 3.0, True), "orin": ("O", 1.5, True)},
    ),
    "stone-village": (
        "stone-village.txt", "route-stone-village.json",
        {"H": 1, "A": 0, "I": 0, "T": 0, "D": 1},
        {"gate": ("B", 2.0, False), "stone-0": (("o", 0), 1.1, True), "stone-1": (("o", 1), 1.1, True),
         "stone-2": (("o", 2), 1.1, True), "quarry": ("Q", 1.6, True), "spring": ("S", 1.6, True),
         "cave": ((-6.0, 11.0), 1.5, True), "door": ("D", 3.0, True)},
    ),
}


def main():
    chapter = sys.argv[1] if len(sys.argv) > 1 else "root-boundary"
    layout, output, prop_spread, goals_spec = CHAPTERS[chapter]
    rows = [line for line in (ROOT / "Assets/ArcaneCasters/Data" / layout).read_text().splitlines() if line]
    height, width = len(rows), len(rows[0])

    def center(col, row):
        return (-width / 2 + col + 0.5, height / 2 - row - 0.5)

    markers = {}
    for r, line in enumerate(rows):
        for c, ch in enumerate(line):
            if ch not in TERRAIN:
                markers.setdefault(ch, []).append((c, r))
    blocked_base = [[rows[r][c] in "#~r" for c in range(width)] for r in range(height)]
    for key, spread in prop_spread.items():
        for c, r in markers.get(key, []):
            for dc in range(-spread, spread + 1):
                if 0 <= c + dc < width:
                    blocked_base[r][c + dc] = True

    goals = {}
    start = markers["P"][0]
    for name, (key, radius, open_gate) in goals_spec.items():
        blocked = [row[:] for row in blocked_base]
        if not open_gate:
            for c, r in markers.get("Z", []):
                blocked[r][c] = True
        if isinstance(key, tuple) and isinstance(key[0], float):
            tx, ty = key
        else:
            char, index = key if isinstance(key, tuple) else (key, 0)
            tx, ty = center(*markers[char][index])
        dist = [[-1] * width for _ in range(height)]
        heap = []
        for r in range(height):
            for c in range(width):
                x, y = center(c, r)
                if not blocked[r][c] and math.hypot(x - tx, y - ty) <= radius:
                    dist[r][c] = 0
                    heapq.heappush(heap, (0, c, r))
        while heap:
            d, c, r = heapq.heappop(heap)
            if d > dist[r][c]:
                continue
            for dc, dr, cost in ((1, 0, 10), (-1, 0, 10), (0, 1, 10), (0, -1, 10), (1, 1, 14), (1, -1, 14), (-1, 1, 14), (-1, -1, 14)):
                nc, nr = c + dc, r + dr
                if not (0 <= nc < width and 0 <= nr < height) or blocked[nr][nc]:
                    continue
                if dc and dr and (blocked[r][nc] or blocked[nr][c]):
                    continue
                nd = d + cost
                if dist[nr][nc] < 0 or nd < dist[nr][nc]:
                    dist[nr][nc] = nd
                    heapq.heappush(heap, (nd, nc, nr))
        goals[name] = {"target": [tx, ty], "distance": [v for row in dist for v in row]}
        print(name, "from start:", dist[start[1]][start[0]])
    (Path(__file__).resolve().parent / "scenarios" / output).write_text(json.dumps({"width": width, "height": height, "goals": goals}))


if __name__ == "__main__":
    main()
