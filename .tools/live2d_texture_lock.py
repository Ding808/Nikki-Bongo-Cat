#!/usr/bin/env python3
"""Prepare and validate locked Live2D texture atlas edits."""

from __future__ import annotations

import argparse
import csv
import sys
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter

try:
    from scipy import ndimage
except Exception:  # pragma: no cover - optional speedup
    ndimage = None


DEFAULT_SOURCE = Path("img/standard/live2d_models/current_nuannuan/nuannuan.2048/texture_00.png")


def load_rgba(path: Path) -> Image.Image:
    if not path.exists():
        raise FileNotFoundError(path)
    return Image.open(path).convert("RGBA")


def checkerboard(size: tuple[int, int], cell: int = 24) -> Image.Image:
    w, h = size
    yy, xx = np.indices((h, w))
    cells = ((xx // cell) + (yy // cell)) % 2
    arr = np.where(cells[..., None] == 0, 236, 210).astype(np.uint8)
    return Image.fromarray(np.repeat(arr, 3, axis=2), "RGB").convert("RGBA")


def alpha_edges(alpha: Image.Image) -> Image.Image:
    edges = alpha.filter(ImageFilter.FIND_EDGES)
    return edges.point(lambda p: 255 if p > 0 else 0)


def label_components(mask: np.ndarray) -> list[dict[str, int]]:
    if ndimage is not None:
        structure = np.array([[0, 1, 0], [1, 1, 1], [0, 1, 0]], dtype=np.uint8)
        labels, count = ndimage.label(mask, structure=structure)
        boxes = ndimage.find_objects(labels)
        areas = np.bincount(labels.ravel())
        result: list[dict[str, int]] = []
        for index in range(1, count + 1):
            sl = boxes[index - 1]
            if sl is None:
                continue
            y0, y1 = sl[0].start, sl[0].stop
            x0, x1 = sl[1].start, sl[1].stop
            result.append(
                {
                    "id": index,
                    "x": x0,
                    "y": y0,
                    "w": x1 - x0,
                    "h": y1 - y0,
                    "area": int(areas[index]),
                }
            )
        return result

    h, w = mask.shape
    visited = np.zeros_like(mask, dtype=bool)
    result = []
    cid = 0
    for y in range(h):
        for x in range(w):
            if not mask[y, x] or visited[y, x]:
                continue
            cid += 1
            q: deque[tuple[int, int]] = deque([(x, y)])
            visited[y, x] = True
            min_x = max_x = x
            min_y = max_y = y
            area = 0
            while q:
                cx, cy = q.popleft()
                area += 1
                min_x = min(min_x, cx)
                max_x = max(max_x, cx)
                min_y = min(min_y, cy)
                max_y = max(max_y, cy)
                for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
                    if 0 <= nx < w and 0 <= ny < h and mask[ny, nx] and not visited[ny, nx]:
                        visited[ny, nx] = True
                        q.append((nx, ny))
            result.append({"id": cid, "x": min_x, "y": min_y, "w": max_x - min_x + 1, "h": max_y - min_y + 1, "area": area})
    return result


def write_islands_csv(path: Path, islands: list[dict[str, int]]) -> None:
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=["id", "x", "y", "w", "h", "area"])
        writer.writeheader()
        writer.writerows(islands)


def draw_guide(source: Image.Image, islands: list[dict[str, int]], min_label_area: int) -> Image.Image:
    bg = checkerboard(source.size)
    bg.alpha_composite(source)

    alpha = source.getchannel("A")
    red = Image.new("RGBA", source.size, (255, 40, 80, 0))
    red.putalpha(alpha_edges(alpha))
    bg.alpha_composite(red)

    draw = ImageDraw.Draw(bg)
    for island in islands:
        if island["area"] < min_label_area:
            continue
        x, y, w, h = island["x"], island["y"], island["w"], island["h"]
        draw.rectangle((x, y, x + w - 1, y + h - 1), outline=(255, 40, 80, 220), width=2)
        draw.rectangle((x, y, x + 48, y + 22), fill=(255, 40, 80, 220))
        draw.text((x + 4, y + 4), str(island["id"]), fill=(255, 255, 255, 255))
    return bg


def prepare(args: argparse.Namespace) -> None:
    source = load_rgba(args.source)
    args.outdir.mkdir(parents=True, exist_ok=True)

    alpha = source.getchannel("A")
    mask = np.array(alpha) > args.alpha_threshold
    islands = label_components(mask)
    islands.sort(key=lambda item: item["area"], reverse=True)
    for index, island in enumerate(islands, start=1):
        island["id"] = index

    source.save(args.outdir / "locked_source_texture.png")
    alpha.save(args.outdir / "locked_alpha_mask.png")
    alpha_edges(alpha).save(args.outdir / "locked_alpha_edges.png")
    draw_guide(source, islands, args.min_label_area).save(args.outdir / "locked_layout_guide.png")
    write_islands_csv(args.outdir / "locked_islands.csv", islands)

    print(f"source: {args.source}")
    print(f"canvas: {source.width}x{source.height}")
    print(f"islands: {len(islands)}")
    print(f"outdir: {args.outdir}")


def apply_bleed(rgb: np.ndarray, visible: np.ndarray, radius: int) -> np.ndarray:
    if radius <= 0 or ndimage is None:
        return rgb
    dilated = ndimage.binary_dilation(visible, iterations=radius)
    fill_area = dilated & ~visible
    if not np.any(fill_area):
        return rgb
    _, indices = ndimage.distance_transform_edt(~visible, return_indices=True)
    out = rgb.copy()
    nearest_y, nearest_x = indices
    out[fill_area] = rgb[nearest_y[fill_area], nearest_x[fill_area]]
    return out


def make_preview(source: Image.Image, candidate: Image.Image, locked: Image.Image) -> Image.Image:
    panels = []
    for img in (source, candidate, locked):
        bg = checkerboard(img.size)
        bg.alpha_composite(img)
        panels.append(bg.convert("RGB"))
    w, h = source.size
    preview = Image.new("RGB", (w * 3, h), (245, 245, 245))
    for index, panel in enumerate(panels):
        preview.paste(panel, (index * w, 0))
    return preview


def lock(args: argparse.Namespace) -> None:
    source = load_rgba(args.source)
    candidate = load_rgba(args.candidate)
    if candidate.size != source.size:
        raise ValueError(f"candidate size {candidate.size} does not match source size {source.size}")

    src = np.array(source)
    cand = np.array(candidate)
    src_alpha = src[..., 3]
    visible = src_alpha > args.alpha_threshold

    out_rgb = cand[..., :3].copy()
    out_rgb = apply_bleed(out_rgb, visible, args.bleed)
    out = np.dstack([out_rgb, src_alpha]).astype(np.uint8)
    out[~(src_alpha > 0), 3] = 0

    locked = Image.fromarray(out, "RGBA")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    locked.save(args.output)

    cand_alpha = cand[..., 3]
    outside_pollution = int(np.count_nonzero((cand_alpha > args.alpha_threshold) & ~visible))
    alpha_diff = ImageChops.difference(candidate.getchannel("A"), source.getchannel("A"))
    alpha_mismatch = int(np.count_nonzero(np.array(alpha_diff)))

    report_lines = [
        f"source={args.source}",
        f"candidate={args.candidate}",
        f"output={args.output}",
        f"canvas={source.width}x{source.height}",
        f"visible_pixels={int(np.count_nonzero(visible))}",
        f"candidate_visible_pixels={int(np.count_nonzero(cand_alpha > args.alpha_threshold))}",
        f"candidate_outside_source_alpha_pixels={outside_pollution}",
        f"candidate_alpha_mismatch_pixels={alpha_mismatch}",
        f"source_alpha_forced=true",
        f"bleed_radius={args.bleed if ndimage is not None else 0}",
    ]

    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text("\n".join(report_lines) + "\n", encoding="utf-8")
    if args.preview:
        args.preview.parent.mkdir(parents=True, exist_ok=True)
        make_preview(source, candidate, locked).save(args.preview)

    print("\n".join(report_lines))


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)

    prep = sub.add_parser("prepare", help="Create alpha masks, edge guides, and island CSV from the original texture.")
    prep.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    prep.add_argument("--outdir", type=Path, default=Path(".tmp/live2d_texture_lock"))
    prep.add_argument("--alpha-threshold", type=int, default=0)
    prep.add_argument("--min-label-area", type=int, default=100)
    prep.set_defaults(func=prepare)

    lock_cmd = sub.add_parser("lock", help="Force an AI-generated atlas back onto the original canvas and alpha mask.")
    lock_cmd.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    lock_cmd.add_argument("--candidate", type=Path, required=True)
    lock_cmd.add_argument("--output", type=Path, required=True)
    lock_cmd.add_argument("--report", type=Path)
    lock_cmd.add_argument("--preview", type=Path)
    lock_cmd.add_argument("--alpha-threshold", type=int, default=0)
    lock_cmd.add_argument("--bleed", type=int, default=6)
    lock_cmd.set_defaults(func=lock)

    args = parser.parse_args(argv)
    args.func(args)
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
