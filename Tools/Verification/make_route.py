#!/usr/bin/env python3
"""Distance maps that let the OS-input playthrough walk the field like a player would.

Reads the same layout grid the game builds from and writes scenarios/route.json:
for each goal, every walkable cell holds its walking distance to the goal area
(-1 when blocked). The scenario presses WASD toward the neighbouring cell with
the smaller distance, so it can recover from any position, including after a
defeat sends Mira back to camp.
"""

import heapq
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
LAYOUT = ROOT / "Assets/ArcaneCasters/Data/root-boundary.txt"
OUT = Path(__file__).resolve().parent / "scenarios/route.json"
TERRAIN = "#.=~p%"
# Marker cells whose prop has a collider, and how many cells it spreads left and right.
PROP_SPREAD = {"H": 1, "F": 0, "E": 1, "L": 0, "G": 1, "C": 0, "T": 0, "N": 0, "A": 1, "I": 0, "V": 0}
GOALS = {  # marker, radius of the standing area around it, seal open
    "root": ("G", 2.0, False),
    "vine": ("V", 1.5, False),
    "brook": ("C", 1.5, False),
    "stones": ("T", 1.5, False),
    "warden": ("W", 3.0, True),
    "orin": ("O", 1.5, True),
}


def main():
    rows = [line for line in LAYOUT.read_text().splitlines() if line]
    height, width = len(rows), len(rows[0])

    def center(col, row):
        return (-width / 2 + col + 0.5, height / 2 - row - 0.5)

    markers = {}
    for r, line in enumerate(rows):
        for c, ch in enumerate(line):
            if ch not in TERRAIN:
                markers.setdefault(ch, []).append((c, r))
    blocked_base = [[rows[r][c] in "#~" for c in range(width)] for r in range(height)]
    for key, spread in PROP_SPREAD.items():
        for c, r in markers.get(key, []):
            for dc in range(-spread, spread + 1):
                if 0 <= c + dc < width:
                    blocked_base[r][c + dc] = True

    goals = {}
    for name, (key, radius, seal_open) in GOALS.items():
        blocked = [row[:] for row in blocked_base]
        if not seal_open:
            for c, r in markers.get("Z", []):
                blocked[r][c] = True
        tc, tr = markers[key][0]
        tx, ty = center(tc, tr)
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
            if d > dist[r][c] >= 0 and dist[r][c] != d:
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
        start = markers["P"][0]
        print(name, "from start:", dist[start[1]][start[0]])
    OUT.write_text(json.dumps({"width": width, "height": height, "goals": goals}))


if __name__ == "__main__":
    main()
