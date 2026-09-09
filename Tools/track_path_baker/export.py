import math, json
from PIL import Image, ImageDraw
import tracer as trace3
from tracer import dt, road, im, W, H, PPU

WORLD_W, WORLD_H = 230.4, 153.6
SPACING_PX = 30.0          # ~4.5 world units between stored points
SMOOTH = 2                 # circular moving average half-window
MERGED_PX = 90.0           # wider than a road: the two that run edge to edge
MAX_SHIFT_PX = 10.0        # how far one re-centring pass may move a point
RECENTRE_PASSES = 3

raw = [(p[0], p[1]) for p in trace3.pts]
if math.hypot(raw[-1][0] - raw[0][0], raw[-1][1] - raw[0][1]) < 1.0:
    raw.pop()
print("raw lap points:", len(raw))

def resample(loop, spacing):
    """Lays points back down at an even spacing along the loop. The points move along
    the line, never off it, so the shape is untouched and only the spacing changes."""
    count = len(loop)
    seg = [math.hypot(loop[(i + 1) % count][0] - loop[i][0],
                      loop[(i + 1) % count][1] - loop[i][1]) for i in range(count)]
    length = sum(seg)
    wanted = max(24, int(round(length / spacing)))
    stride = length / wanted

    out = []
    walked, idx = 0.0, 0
    for k in range(wanted):
        target = k * stride
        while idx < count - 1 and walked + seg[idx] < target:
            walked += seg[idx]
            idx += 1
        a, b = loop[idx], loop[(idx + 1) % count]
        t = (target - walked) / seg[idx] if seg[idx] > 1e-9 else 0.0
        out.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
    return out, length, stride


even, total, step = resample(raw, SPACING_PX)
print("lap length: %.0f px = %.1f world units" % (total, total / PPU))
print("resampled to", len(even), "points, spacing %.1f px = %.2f world" % (step, step / PPU))

# Circular smoothing: the walker wobbles by a pixel or two per step.
sm = []
n = len(even)
for i in range(n):
    sx = sy = 0.0; w = 0
    for o in range(-SMOOTH, SMOOTH + 1):
        p = even[(i + o) % n]
        sx += p[0]; sy += p[1]; w += 1
    sm.append((sx / w, sy / w))

def edges(points, i):
    """Distance from a point out to the road edge either side of it, measured
    across the line rather than along one segment, which matters on a corner."""
    count = len(points)
    here = points[i]
    before, after = points[(i - 1) % count], points[(i + 1) % count]
    tx, ty = after[0] - before[0], after[1] - before[1]
    length = math.hypot(tx, ty) or 1.0
    nx, ny = -ty / length, tx / length
    return cast(here, -nx, -ny), cast(here, nx, ny), (nx, ny)


def cast(point, dx, dy, limit=140.0):
    walked = 0.0
    while walked < limit:
        walked += 1.0
        ix, iy = int(point[0] + dx * walked), int(point[1] + dy * walked)
        if ix < 0 or iy < 0 or ix >= W or iy >= H or not road[iy * W + ix]:
            return walked
    return limit


# The road is one width the whole way round, so the line is nudged until it sits
# in the middle of it: one stored width is then honest everywhere, where before
# the line ran up to a metre and a half wide of centre and each point had to
# carry its own. Where the art merges two roads into one wide surface the line
# stays put, or it would drift into the gap between them.
for _ in range(RECENTRE_PASSES):
    shifted = []
    for i in range(n):
        left, right, (nx, ny) = edges(sm, i)
        if left + right > MERGED_PX:
            shifted.append(sm[i])
            continue
        move = max(-MAX_SHIFT_PX, min(MAX_SHIFT_PX, (right - left) * 0.5))
        shifted.append((sm[i][0] + nx * move, sm[i][1] + ny * move))
    sm = []
    for i in range(n):
        sx = sy = 0.0
        for o in range(-1, 2):
            p = shifted[(i + o) % n]
            sx += p[0]; sy += p[1]
        sm.append((sx / 3.0, sy / 3.0))

# Smoothing drags points together wherever the line turns hard - at the seam of this
# circuit it collapsed four of them into six units of road, which reads in the editor as
# two points sitting on top of each other. Re-spacing last leaves one point every stride.
sm, _, stride = resample(sm, SPACING_PX)
n = len(sm)
print("re-spaced to", n, "points, spacing %.2f world units" % (stride / PPU))

margins = []
for i in range(n):
    left, right, _ = edges(sm, i)
    margins.append(min(left, right))
ordered = sorted(margins)
half_px = ordered[0]
print("clearance to the nearest edge, world units: min %.2f p10 %.2f median %.2f max %.2f" %
      (ordered[0] / PPU, ordered[n // 10] / PPU, ordered[n // 2] / PPU, ordered[-1] / PPU))
print("single half width: %.2f world units (road %.2f wide)" % (half_px / PPU, 2 * half_px / PPU))

world = [((p[0] / W - 0.5) * WORLD_W, (p[1] / H - 0.5) * WORLD_H) for p in sm]

# Measured along the points that actually get stored, not along the raw walk, so the
# editor agrees with the bake instead of correcting it the moment the asset is opened.
polyline = sum(math.hypot(world[(i + 1) % n][0] - world[i][0],
                          world[(i + 1) % n][1] - world[i][1]) for i in range(n))
print("stored lap length: %.1f world units (raw walk was %.1f)" % (polyline, total / PPU))
json.dump({"points": world, "halfWidth": half_px / PPU,
           "lapLength": polyline}, open("track_path.json", "w"), indent=1)
print("start point world:", "(%.2f, %.2f)" % world[0])
print("bike sits at world (-0.50, 62.60)")

dbg = im.copy(); dr = ImageDraw.Draw(dbg)
for i in range(n):
    a, b = sm[i], sm[(i + 1) % n]
    dr.line([a[0], a[1], b[0], b[1]], fill=(0, 255, 0), width=3)
    ax, ay = b[0] - a[0], b[1] - a[1]
    L = math.hypot(ax, ay) or 1.0
    nx, ny = -ay / L, ax / L
    dr.line([a[0] + nx*half_px, a[1] + ny*half_px, b[0] + nx*half_px, b[1] + ny*half_px],
            fill=(255, 0, 255), width=2)
    dr.line([a[0] - nx*half_px, a[1] - ny*half_px, b[0] - nx*half_px, b[1] - ny*half_px],
            fill=(255, 0, 255), width=2)
for i, p in enumerate(sm):
    dr.ellipse([p[0]-3, p[1]-3, p[0]+3, p[1]+3], fill=(255, 255, 0))
dbg.save("path_overlay.png")
print("wrote path_overlay.png and track_path.json")
