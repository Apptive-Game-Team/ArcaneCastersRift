#!/usr/bin/env python3
"""Rasterise chapter 2 (Stone Village, Fin's field study) into its layout grid.

Same rules as make_layout.py: one character per 1x1 unit cell, written to
`Assets/ArcaneCasters/Data/stone-village.txt`; the game builds ground, collision,
decoration and every marked position from it.

Legend (terrain)
  #  rock and pine (blocked)          .  meadow grass
  =  gravel path                      ~  water (blocked)
  c  cave floor (dark)                r  mountain rock around the cave (blocked)
  Z  gate held by the big stone golem until it steps aside
"""

import math
from pathlib import Path

WIDTH, HEIGHT = 48, 32
OUT = Path(__file__).resolve().parents[2] / "Assets/ArcaneCasters/Data/stone-village.txt"


def world(col, row):
    return (-WIDTH / 2 + col + 0.5, HEIGHT / 2 - row - 0.5)


def segment_distance(p, a, b):
    (ax, ay), (bx, by), (px, py) = a, b, p
    dx, dy = bx - ax, by - ay
    t = 0.0 if dx == dy == 0 else max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy)))
    return math.hypot(px - (ax + dx * t), py - (ay + dy * t))


def polyline_distance(p, points):
    return min(segment_distance(p, points[i], points[i + 1]) for i in range(len(points) - 1))


def wobble(x, y, seed):
    return (math.sin(x * 0.9 + seed) * 0.5 + math.sin(y * 1.3 + seed * 2.1) * 0.35 + math.sin((x + y) * 0.55 + seed * 0.7) * 0.15)


ENTRY = (-2.0, -13.5)
GATE_Y = -8.5
PLAZA = (-2.0, -2.5)
QUARRY = (-14.0, 0.5)
SPRING = (12.0, -2.0)
MOUTH = (-2.0, 8.0)
CHAMBER = (10.0, 12.0)

CLEARINGS = [(PLAZA, 5.6), (QUARRY, 4.2), (SPRING, 3.8), ((-2.0, 4.5), 2.6)]
PATHS = [
    [(-2.0, -16.5), ENTRY, (-2.0, GATE_Y), PLAZA],
    [PLAZA, (-8.0, -1.0), QUARRY],
    [PLAZA, (5.0, -2.5), SPRING],
    [PLAZA, (-2.0, 4.5), MOUTH],
]
CAVE = [MOUTH, (-2.0, 10.0), (-9.0, 11.0), (-9.5, 13.5), (-2.0, 14.0), (3.5, 12.0), CHAMBER]
CAVE_WIDTH = 1.45
CHAMBER_RADIUS = 3.6
SPRING_POOL = [((14.0, -1.0), 1.5)]
STREAM = [(19.0, 16.5), (17.5, 8.0), (16.0, 2.0), (14.0, -1.0)]

MARKERS = {
    "P": ("Fin start", ENTRY),
    "B": ("big stone golem at the gate", (-2.0, GATE_Y + 0.9)),
    "K": ("Mok", (-1.0, -4.0)),
    "m": ("mini-rock", [(-5.5, -1.5), (0.8, -0.8), (-4.6, -5.3)]),
    "o": ("play stone", [(-4.4, -3.4), (-2.6, -1.0), (-0.4, -3.0)]),
    "H": ("stone hut", [(-6.0, 0.6), (2.8, 0.8), (-6.4, -4.4)]),
    "A": ("cairn", [(1.8, -5.6), (-3.4, 1.6)]),
    "Q": ("quarry golem / observation", (-14.0, 1.6)),
    "I": ("quarry blocks", [(-16.4, 0.0), (-11.6, 2.6)]),
    "T": ("tool rack", (-16.0, 2.8)),
    "S": ("spring golem / observation", (11.0, -1.2)),
    "M": ("cave mouth", (-2.0, MOUTH[1] + 0.2)),
    "D": ("ancient door", (CHAMBER[0], CHAMBER[1] + 2.4)),
    "k": ("Mok at the door", (CHAMBER[0] - 1.2, CHAMBER[1] + 1.0)),
    "w": ("wave spawn", [(CHAMBER[0] + 2.0, CHAMBER[1] + 0.9), (CHAMBER[0] - 2.2, CHAMBER[1] - 0.4), (CHAMBER[0] + 1.2, CHAMBER[1] - 2.0)]),
    "1": ("path-1", (-3.0, 3.6)), "2": ("path-2", (-0.8, 5.0)),
    "3": ("cave-1", (-7.0, 11.0)), "4": ("cave-2", (-6.0, 13.8)), "5": ("cave-3", (1.0, 13.2)),
}


def classify(x, y):
    for (cx, cy), radius in SPRING_POOL:
        if math.hypot(x - cx, y - cy) < radius:
            return "~"
    if polyline_distance((x, y), STREAM) < 0.8 + 0.12 * wobble(x, y, 2):
        return "~"
    if y > MOUTH[1] - 0.5:
        # Inside the cliff: only the carved cave passage and chamber are open.
        if polyline_distance((x, y), CAVE) < CAVE_WIDTH + 0.3 * wobble(x, y, 5) or math.hypot(x - CHAMBER[0], y - CHAMBER[1]) < CHAMBER_RADIUS:
            return "c"
        return "r"
    on_path = any(polyline_distance((x, y), p) < 0.62 for p in PATHS)
    if on_path:
        return "="
    for (cx, cy), radius in CLEARINGS:
        if math.hypot(x - cx, y - cy) < radius + 0.35 * wobble(x, y, cx):
            return "."
    if any(polyline_distance((x, y), p) < 1.9 + 0.3 * wobble(x, y, 7) for p in PATHS):
        return "."
    return "#"


def main():
    grid = [[classify(*world(c, r)) for c in range(WIDTH)] for r in range(HEIGHT)]
    gate_row = int(math.floor(HEIGHT / 2 - GATE_Y))
    for col in range(WIDTH):
        x, _ = world(col, gate_row)
        if abs(x - ENTRY[0]) < 3 and grid[gate_row][col] in ".=":
            grid[gate_row][col] = "Z"
    for char, (name, places) in MARKERS.items():
        for x, y in places if isinstance(places, list) else [places]:
            col, row = int(math.floor(x + WIDTH / 2)), int(math.floor(HEIGHT / 2 - y))
            under = grid[row][col]
            if under in "#~Zr" or not ("." == under or "=" == under or "c" == under):
                raise SystemExit(f"marker {char} ({name}) at {x},{y} lands on '{under}'")
            grid[row][col] = char
    text = "\n".join("".join(row) for row in grid) + "\n"
    OUT.write_text(text)
    print(text)


if __name__ == "__main__":
    main()
