#!/usr/bin/env python3
"""Cut generated sprite sheets into frames that line up.

The generator paints a flat key colour instead of transparency, and it does not
honour a grid exactly: figures drift inside their cells, differ in size, and a
glow can spill toward a neighbour. Slicing such a sheet on a uniform grid is
what produced the chopped player sprite in the first prototype. This tool never
trusts the grid. It removes the key colour, labels the real opaque regions,
checks that every region sits inside exactly one expected cell, and only then
re-packs the frames onto a uniform strip with one shared scale and one shared
anchor (ground point) so frames can be swapped at runtime without jumping.

Commands
  key    <src> <dst> [--key magenta|green] [--deadzone N]
  frames <keyed.png> <out-prefix> --grid COLSxROWS --names a,b,c,... [--anchor head|center]
         [--height PX] [--cell WxH]
  props  <keyed.png> <out-dir> --grid COLSxROWS --names a,b,... [--max PX]

The key removal follows WordOnlineClient `.art/tools/key-out-background.py`
(same coverage maths, same thresholds), reimplemented here so this repository
does not depend on that one.
"""

import argparse
import json
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image

BACKGROUND_ALPHA = 0.10
SUBJECT_ALPHA = 0.94
MIN_REGION_PIXELS = 60  # at quarter resolution; smaller specks are glow dust


def coverage_for(rgb, key, deadzone):
    rgb = rgb.astype(np.int16)
    if key == "magenta":
        excess = np.minimum(rgb[:, :, 0], rgb[:, :, 2]) - rgb[:, :, 1]
        headroom = np.maximum(255 - rgb[:, :, 1] - deadzone, 1)
    else:
        excess = rgb[:, :, 1] - np.maximum(rgb[:, :, 0], rgb[:, :, 2])
        headroom = np.maximum(255 - np.maximum(rgb[:, :, 0], rgb[:, :, 2]) - deadzone, 1)
    return (1.0 - np.clip(np.maximum(excess - deadzone, 0) / headroom, 0.0, 1.0)).astype(np.float32)


def key_out(image, key, deadzone):
    rgb = np.asarray(image.convert("RGB"))
    coverage = coverage_for(rgb, key, deadzone)
    background = coverage < SUBJECT_ALPHA
    alpha = np.ones(coverage.shape, dtype=np.float32)
    alpha[background] = coverage[background]
    alpha[coverage < BACKGROUND_ALPHA] = 0.0
    key_colour = np.array([255, 0, 255] if key == "magenta" else [0, 255, 0], dtype=np.float32)
    out = rgb.astype(np.float32)
    boundary = background & (alpha > 0.0)
    if boundary.any():
        share = alpha[boundary][:, None]
        out[boundary] = np.clip((out[boundary] - (1.0 - share) * key_colour) / share, 0, 255)
    return Image.fromarray(np.dstack([out.astype(np.uint8), (alpha * 255).astype(np.uint8)]), "RGBA")


def dilate(mask, radius):
    out = mask.copy()
    for _ in range(radius):
        grown = out.copy()
        grown[1:, :] |= out[:-1, :]
        grown[:-1, :] |= out[1:, :]
        grown[:, 1:] |= out[:, :-1]
        grown[:, :-1] |= out[:, 1:]
        out = grown
    return out


def label_regions(alpha, scale=4, merge=3):
    """Connected opaque regions at 1/scale resolution, merged across small gaps."""
    small = alpha[::scale, ::scale] > 128
    grown = dilate(small, merge)
    labels = np.zeros(grown.shape, dtype=np.int32)
    regions = []
    height, width = grown.shape
    for y in range(height):
        for x in range(width):
            if not grown[y, x] or labels[y, x]:
                continue
            index = len(regions) + 1
            queue = deque([(y, x)])
            labels[y, x] = index
            pixels = []
            while queue:
                cy, cx = queue.popleft()
                pixels.append((cy, cx))
                for ny, nx in ((cy - 1, cx), (cy + 1, cx), (cy, cx - 1), (cy, cx + 1)):
                    if 0 <= ny < height and 0 <= nx < width and grown[ny, nx] and not labels[ny, nx]:
                        labels[ny, nx] = index
                        queue.append((ny, nx))
            ys = [p[0] for p in pixels]
            xs = [p[1] for p in pixels]
            real = int(small[labels == index].sum())
            regions.append({"index": index, "pixels": real,
                            "box": (min(xs) * scale, min(ys) * scale, (max(xs) + 1) * scale, (max(ys) + 1) * scale)})
    regions = [r for r in regions if r["pixels"] >= MIN_REGION_PIXELS]
    return labels, regions, scale


def assign_to_cells(image, grid, names):
    cols, rows = grid
    alpha = np.asarray(image)[:, :, 3]
    labels, regions, scale = label_regions(alpha)
    width, height = image.size
    cell_w, cell_h = width / cols, height / rows
    cells = {}
    problems = []
    for region in regions:
        x0, y0, x1, y1 = region["box"]
        col0, col1 = int(x0 // cell_w), int(min(x1 - 1, width - 1) // cell_w)
        row0, row1 = int(y0 // cell_h), int(min(y1 - 1, height - 1) // cell_h)
        if col0 != col1 or row0 != row1:
            problems.append(f"region {region['box']} crosses cells ({col0},{row0})-({col1},{row1})")
            continue
        cells.setdefault(row0 * cols + col0, []).append(region)
    frames = {}
    for i, name in enumerate(names):
        if name in ("", "-"):
            continue
        members = cells.get(i, [])
        if not members:
            problems.append(f"cell {i} ({name}) is empty")
            continue
        # keep regions of this cell only, so a neighbour's spill can never leak in
        mask_small = np.isin(labels, [r["index"] for r in members])
        mask = np.kron(mask_small, np.ones((scale, scale), dtype=bool))[:height, :width]
        mask = dilate(mask, 2)
        pixels = np.asarray(image).copy()
        pixels[:, :, 3] = np.where(mask, pixels[:, :, 3], 0)
        frame = Image.fromarray(pixels, "RGBA")
        box = frame.getbbox()
        frames[name] = frame.crop(box)
        frames[name].info["source_box"] = box
        margin = min(box[0] - (i % cols) * cell_w, box[1] - (i // cols) * cell_h,
                     ((i % cols) + 1) * cell_w - box[2], ((i // cols) + 1) * cell_h - box[3])
        if margin < 6:
            problems.append(f"cell {i} ({name}) touches its cell border (margin {margin:.0f}px)")
    return frames, problems


def head_anchor_x(frame):
    """Horizontal centre of the top fifth of the figure: the head barely moves between poses."""
    alpha = np.asarray(frame)[:, :, 3] > 128
    top = alpha[: max(1, alpha.shape[0] // 5)]
    xs = np.nonzero(top)[1]
    return float(xs.mean()) if len(xs) else frame.width / 2


def body_anchor_x(frame):
    alpha = np.asarray(frame)[:, :, 3] > 128
    xs = np.nonzero(alpha)[1]
    return float(xs.mean()) if len(xs) else frame.width / 2


def upper_body_offset(reference, frame, search=48):
    """Translation (dx, dy) that best overlays `frame` on `reference` by their upper bodies.

    Head and torso stay put between idle, walk and cast poses while legs, staff
    swings and casting arms move, so matching only the top 55% of the reference
    keeps the head from jumping when frames swap. Returned in pixels of the
    given images: placing `frame` at reference_origin + (dx, dy) overlays it.
    """
    step = 2
    ref = np.asarray(reference)[::step, ::step, 3] > 128
    cur = np.asarray(frame)[::step, ::step, 3] > 128
    rows = int(ref.shape[0] * 0.55)
    ref_top = ref[:rows]
    best, best_score = (0, 0), -1.0
    pad = search // step
    canvas_h, canvas_w = ref.shape[0] + 2 * pad, ref.shape[1] + 2 * pad
    for dy in range(-pad, pad + 1):
        for dx in range(-pad, pad + 1):
            placed = np.zeros((canvas_h, canvas_w), dtype=bool)
            y0, x0 = pad + dy, pad + dx
            h = min(cur.shape[0], canvas_h - y0)
            w = min(cur.shape[1], canvas_w - x0)
            if h <= 0 or w <= 0:
                continue
            placed[y0:y0 + h, x0:x0 + w] = cur[:h, :w]
            window = placed[pad:pad + rows, pad:pad + ref.shape[1]]
            union = (window | ref_top).sum()
            score = (window & ref_top).sum() / union if union else 0.0
            if score > best_score:
                best, best_score = (dx * step, dy * step), score
    return best, best_score


def command_key(args):
    result = key_out(Image.open(args.src), args.key, args.deadzone)
    result.save(args.dst, optimize=True)
    alpha = np.asarray(result)[:, :, 3]
    print(f"{args.dst} transparent={(alpha == 0).mean():.1%}")


def command_frames(args):
    image = Image.open(args.src).convert("RGBA")
    cols, rows = (int(v) for v in args.grid.lower().split("x"))
    names = args.names.split(",")
    frames, problems = assign_to_cells(image, (cols, rows), names)
    for problem in problems:
        print("PROBLEM", problem)
    if problems and not args.force:
        raise SystemExit(1)
    ordered = [n for n in names if n in frames]
    reference = frames[ordered[0]]
    scale = args.height / reference.height
    scaled = {}
    for name in ordered:
        frame = frames[name]
        size = (max(1, round(frame.width * scale)), max(1, round(frame.height * scale)))
        scaled[name] = frame.resize(size, Image.LANCZOS)
    # Placement of every frame relative to the reference frame's top-left corner.
    # The reference stands on the ground line; the others follow its upper body.
    offsets = {ordered[0]: (0, 0)}
    report_scores = {ordered[0]: 1.0}
    for name in ordered[1:]:
        if args.align == "upper":
            offsets[name], report_scores[name] = upper_body_offset(scaled[ordered[0]], scaled[name])
        else:
            ref_anchor = body_anchor_x(scaled[ordered[0]])
            offsets[name] = (round(ref_anchor - body_anchor_x(scaled[name])),
                             scaled[ordered[0]].height - scaled[name].height)
            report_scores[name] = None
    pad = 8
    ref_anchor_x = body_anchor_x(scaled[ordered[0]]) if args.anchor == "center" else head_anchor_x(scaled[ordered[0]])
    if args.align == "upper":
        ref_anchor_x = body_anchor_x(scaled[ordered[0]])
    min_x = min(dx for dx, _ in offsets.values())
    max_x = max(dx + scaled[n].width for n, (dx, _) in offsets.items())
    min_y = min(dy for _, dy in offsets.values())
    max_y = max(dy + scaled[n].height for n, (_, dy) in offsets.items())
    ground = scaled[ordered[0]].height  # reference soles, in reference coordinates
    half = max(ref_anchor_x - min_x, max_x - ref_anchor_x)
    cell_w = int(np.ceil(half * 2)) + pad * 2
    below = max(max_y - ground, 0)
    cell_h = int(np.ceil(ground - min_y + below)) + pad * 2
    if args.cell:
        want_w, want_h = (int(v) for v in args.cell.lower().split("x"))
        if want_w < cell_w or want_h < cell_h:
            raise SystemExit(f"requested cell {args.cell} smaller than content {cell_w}x{cell_h}")
        cell_w, cell_h = want_w, want_h
    ground_y = cell_h - pad - below  # canvas row of the ground line, from the top
    strip = Image.new("RGBA", (cell_w * len(ordered), cell_h), (0, 0, 0, 0))
    report = {"cell": [cell_w, cell_h], "pivot": [0.5, round((cell_h - ground_y) / cell_h, 4)], "names": ordered, "frames": {}}
    for i, name in enumerate(ordered):
        dx, dy = offsets[name]
        x = int(round(i * cell_w + cell_w / 2 - ref_anchor_x + dx))
        y = int(round(ground_y - ground + dy))
        strip.alpha_composite(scaled[name], (x, y))
        report["frames"][name] = {"height": scaled[name].height, "width": scaled[name].width,
                                  "offset": [dx, dy], "overlap": report_scores[name] and round(report_scores[name], 3),
                                  "height_ratio": round(scaled[name].height / scaled[ordered[0]].height, 3),
                                  "source_box": frames[name].info["source_box"]}
    strip.save(args.out + ".png", optimize=True)
    Path(args.out + ".json").write_text(json.dumps(report, indent=1))
    print(json.dumps(report))


def free_regions(image, names):
    """Name separate objects in reading order when the generator ignored the grid.

    Objects may overflow their intended cell as long as they stay apart from
    each other; the count must match exactly, so a merged pair or a stray
    fragment still fails loudly.
    """
    alpha = np.asarray(image)[:, :, 3]
    labels, regions, scale = label_regions(alpha)
    problems = []
    if len(regions) < len(names):
        problems.append(f"found {len(regions)} objects, expected {len(names)}: {[r['box'] for r in regions]}")
        return {}, problems

    def centre(region):
        x0, y0, x1, y1 = region["box"]
        return (x0 + x1) / 2, (y0 + y1) / 2

    # Sparkles and chips detach from their emblem; give each to the nearest main object.
    regions.sort(key=lambda r: -r["pixels"])
    main, fragments = regions[:len(names)], regions[len(names):]
    members = {r["index"]: [r["index"]] for r in main}
    for fragment in fragments:
        fx, fy = centre(fragment)
        owner = min(main, key=lambda r: (centre(r)[0] - fx) ** 2 + (centre(r)[1] - fy) ** 2)
        members[owner["index"]].append(fragment["index"])
    band = image.height / 6
    main.sort(key=lambda r: (round(centre(r)[1] / band), r["box"][0]))
    frames = {}
    height, width = alpha.shape
    for region, name in zip(main, names):
        mask = np.kron(np.isin(labels, members[region["index"]]), np.ones((scale, scale), dtype=bool))[:height, :width]
        pixels = np.asarray(image).copy()
        pixels[:, :, 3] = np.where(dilate(mask, 2), pixels[:, :, 3], 0)
        frame = Image.fromarray(pixels, "RGBA")
        frames[name] = frame.crop(frame.getbbox())
    return frames, problems


def command_props(args):
    image = Image.open(args.src).convert("RGBA")
    cols, rows = (int(v) for v in args.grid.lower().split("x"))
    names = args.names.split(",")
    if args.free:
        frames, problems = free_regions(image, names)
    else:
        frames, problems = assign_to_cells(image, (cols, rows), names)
    for problem in problems:
        print("PROBLEM", problem)
    if problems and not args.force:
        raise SystemExit(1)
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    for name, frame in frames.items():
        if args.max and max(frame.size) > args.max:
            ratio = args.max / max(frame.size)
            frame = frame.resize((round(frame.width * ratio), round(frame.height * ratio)), Image.LANCZOS)
        frame.save(out / f"{name}.png", optimize=True)
        print(name, frame.size)


def main():
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest="command", required=True)
    key = sub.add_parser("key")
    key.add_argument("src")
    key.add_argument("dst")
    key.add_argument("--key", default="magenta", choices=("magenta", "green"))
    key.add_argument("--deadzone", type=int, default=0)
    frames = sub.add_parser("frames")
    frames.add_argument("src")
    frames.add_argument("out")
    frames.add_argument("--grid", required=True)
    frames.add_argument("--names", required=True)
    frames.add_argument("--anchor", default="center", choices=("head", "center"))
    frames.add_argument("--align", default="upper", choices=("upper", "feet"),
                        help="upper: overlay frames by head and torso; feet: share the ground line")
    frames.add_argument("--height", type=int, default=192)
    frames.add_argument("--cell")
    frames.add_argument("--force", action="store_true")
    props = sub.add_parser("props")
    props.add_argument("src")
    props.add_argument("out")
    props.add_argument("--grid", required=True)
    props.add_argument("--names", required=True)
    props.add_argument("--max", type=int, default=0)
    props.add_argument("--free", action="store_true", help="ignore the grid; name objects in reading order")
    props.add_argument("--force", action="store_true")
    ground = sub.add_parser("ground")
    ground.add_argument("src")
    ground.add_argument("out")
    ground.add_argument("--names", required=True, help="four names, row-major over the 2x2 quadrants")
    ground.add_argument("--size", type=int, default=512)
    ground.add_argument("--inset", type=int, default=12, help="pixels trimmed from each quadrant edge")
    args = parser.parse_args()
    {"key": command_key, "frames": command_frames, "props": command_props, "ground": command_ground}[args.command](args)


def make_seamless(tile):
    """Blend a texture with its half-offset copy so opposite edges match.

    The centre keeps the original pixels; near the edges the copy rolled by
    half a tile takes over, and that copy's own edges are the original's
    continuous middle, so the result wraps without a visible seam.
    """
    pixels = np.asarray(tile.convert("RGB")).astype(np.float32)
    height, width = pixels.shape[:2]
    rolled = np.roll(np.roll(pixels, height // 2, axis=0), width // 2, axis=1)
    ys = np.abs(np.linspace(-1, 1, height))[:, None]
    xs = np.abs(np.linspace(-1, 1, width))[None, :]
    edge = np.clip((np.maximum(xs, ys) - 0.55) / 0.4, 0, 1)
    weight = (edge * edge * (3 - 2 * edge))[:, :, None]
    blended = pixels * (1 - weight) + rolled * weight
    return Image.fromarray(blended.clip(0, 255).astype(np.uint8), "RGB")


def command_ground(args):
    image = Image.open(args.src).convert("RGB")
    names = args.names.split(",")
    half_w, half_h = image.width // 2, image.height // 2
    inset = args.inset
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    for i, name in enumerate(names):
        x0, y0 = (i % 2) * half_w + inset, (i // 2) * half_h + inset
        tile = image.crop((x0, y0, x0 + half_w - 2 * inset, y0 + half_h - 2 * inset)).resize((args.size, args.size), Image.LANCZOS)
        make_seamless(tile).save(out / f"{name}.png", optimize=True)
        print(name, args.size)


if __name__ == "__main__":
    main()
