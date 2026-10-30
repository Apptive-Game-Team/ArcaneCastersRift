#!/usr/bin/env python3
"""Rasterise the first field (Root Boundary) into the layout grid the game reads.

The grid is the single source of truth for the field: the runtime paints the
ground, places colliders and scatters trees from it, so the visible path and
the walkable area cannot drift apart the way a painted backdrop and a separate
collision list did. Shapes are described here in world units and written to
`Assets/ArcaneCasters/Data/root-boundary.txt`, one character per 1x1 unit cell.

Legend (terrain)
  #  forest (blocked, trees)        .  grass
  =  dirt path                      ~  stream water (blocked)
  p  ruin flagstone                 %  stone bridge over water
  Z  ruin seal (walkable path, blocked until the seal opens)
Markers (walkable, drawn as the terrain they stand on; see MARKERS below).
"""

import math
from pathlib import Path

WIDTH, HEIGHT = 48, 32          # cells; world x in [-24, 24), y in [-16, 16)
OUT = Path(__file__).resolve().parents[2] / "Assets/ArcaneCasters/Data/root-boundary.txt"


def world(col, row):
    return (-WIDTH / 2 + col + 0.5, HEIGHT / 2 - row - 0.5)


def segment_distance(p, a, b):
    ax, ay = a
    bx, by = b
    px, py = p
    dx, dy = bx - ax, by - ay
    t = 0.0 if dx == dy == 0 else max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy)))
    return math.hypot(px - (ax + dx * t), py - (ay + dy * t))


def polyline_distance(p, points):
    return min(segment_distance(p, points[i], points[i + 1]) for i in range(len(points) - 1))


def wobble(x, y, seed):
    """Smooth deterministic edge noise in [-1, 1] so clearings are not perfect circles."""
    return (math.sin(x * 0.9 + seed) * 0.5 + math.sin(y * 1.3 + seed * 2.1) * 0.35
            + math.sin((x + y) * 0.55 + seed * 0.7) * 0.15)


# Key places, world units.
CAMP = (-16.0, -10.0)
ROOT = (-2.0, -1.0)
GROVE = (-12.0, 2.0)
BROOK = (-12.5, 9.5)
STONES = (12.0, -2.0)
RUIN = (15.0, 10.0)

CLEARINGS = [  # centre, radius, ground ('.' grass or 'p' flagstone)
    (CAMP, 4.6, "."),
    (ROOT, 5.2, "."),
    (GROVE, 3.4, "."),
    (BROOK, 3.6, "."),
    (STONES, 3.8, "."),
    (RUIN, 5.4, "p"),
]
# Walkable corridors (grass margin) with a dirt path down the middle.
ROUTES = [
    [(-15.0, -9.0), (-11.5, -7.0), (-7.5, -4.5), (-4.0, -2.0), ROOT],          # camp -> root
    [ROOT, (-6.0, 0.5), (-9.5, 1.5), GROVE],                                    # root -> grove
    [GROVE, (-12.5, 5.0), BROOK],                                               # grove -> brook
    [ROOT, (2.5, -1.5), (6.5, -2.0), STONES],                                   # root -> bridge -> stones
    [STONES, (13.5, 2.0), (14.0, 5.5), RUIN],                                   # stones -> ruin
]
STREAM = [(4.0, 16.5), (4.5, 11.0), (3.0, 6.0), (4.5, 2.0), (4.8, -2.0), (3.5, -7.0), (5.0, -12.0), (6.0, -16.5)]
BROOK_POOL = [((-15.5, 11.0), 1.7), ((-14.0, 12.8), 1.4)]
BRIDGE_ROUTE = 3  # the route that crosses the stream on a bridge

MARKERS = {
    # people
    "P": ("player start", (-14.3, -9.2)),
    "O": ("orin", (-17.4, -8.9)),
    # research targets (the prop itself is the target; F works within 1.8 units)
    "G": ("world root / research: root", (-2.0, 1.6)),
    "C": ("brook crystals / research: brook", (-13.2, 10.8)),
    "T": ("rune stone / research: stones", (12.6, -0.6)),
    "V": ("vine thicket / research: vine", (-13.0, 3.6)),
    # camp and ruin props
    "H": ("tent", (-18.6, -7.4)),
    "F": ("campfire", (-15.6, -11.0)),
    "E": ("bench", (-13.6, -11.9)),
    "L": ("signpost", (-12.6, -6.4)),
    "A": ("ruin arch", (15.0, 14.2)),
    "I": ("ruin pillar", [(10.8, 9.4), (19.4, 10.6), (11.6, 13.0), (18.4, 13.2)]),
    "N": ("rune stone, decorative", [(10.2, -1.6), (14.4, -3.4)]),
    # fixed encounters (ids are save keys; see ArcaneSession.Encounters)
    "1": ("root-1", (-3.5, -2.2)), "2": ("root-2", (0.0, -2.4)), "3": ("root-3", (0.6, 0.6)),
    "4": ("brook-1", (-11.0, 8.4)), "5": ("brook-2", (-10.6, 10.6)),
    "6": ("bridge-1", (9.0, -2.4)), "7": ("bridge-2", (12.0, -4.6)),
    "8": ("ruin-1", (12.6, 9.0)), "9": ("ruin-2", (17.4, 8.4)), "W": ("warden", (15.0, 11.0)),
    "x": ("grove-1", (-10.6, 1.0)), "y": ("grove-2", (-13.6, 0.8)),
}
SEAL_ROW_Y = 5.5          # the ruin seal spans every walkable cell of this row between SEAL_X
SEAL_X = (10.0, 18.0)


def classify(x, y):
    for (cx, cy), radius in BROOK_POOL:
        if math.hypot(x - cx, y - cy) < radius:
            return "~"
    in_stream = polyline_distance((x, y), STREAM) < 0.95 + 0.15 * wobble(x, y, 3)
    path = any(polyline_distance((x, y), r) < 0.62 for r in ROUTES)
    if in_stream:
        bridge = polyline_distance((x, y), ROUTES[BRIDGE_ROUTE]) < 0.95
        return "%" if bridge else "~"
    for (cx, cy), radius, ground in CLEARINGS:
        if math.hypot(x - cx, y - cy) < radius + 0.35 * wobble(x, y, cx):
            if ground == "p":
                return "p"
            return "=" if path or (cx, cy) == CAMP and math.hypot(x - cx, y - cy) < 2.2 else "."
    if path:
        return "="
    if any(polyline_distance((x, y), r) < 2.1 + 0.3 * wobble(x, y, 7) for r in ROUTES):
        return "."
    return "#"


def main():
    grid = [[classify(*world(c, r)) for c in range(WIDTH)] for r in range(HEIGHT)]
    seal_row = int(math.floor(HEIGHT / 2 - SEAL_ROW_Y))
    for col in range(WIDTH):
        x, _ = world(col, seal_row)
        if SEAL_X[0] <= x <= SEAL_X[1] and grid[seal_row][col] in ".=p":
            grid[seal_row][col] = "Z"
    for char, (name, places) in MARKERS.items():
        for x, y in places if isinstance(places, list) else [places]:
            col, row = int(math.floor(x + WIDTH / 2)), int(math.floor(HEIGHT / 2 - y))
            under = grid[row][col]
            if under in "#~Z" or under in MARKERS:
                raise SystemExit(f"marker {char} ({name}) at {x},{y} lands on '{under}'")
            grid[row][col] = char
    text = "\n".join("".join(row) for row in grid) + "\n"
    OUT.write_text(text)
    print(text)


if __name__ == "__main__":
    main()
