import math, json
from PIL import Image, ImageDraw
from collections import deque
from freemask import W, H, im, comp, SEED, PPU, WORLD_W, WORLD_H

# Plug the speckles the colour test leaves inside the ribbon, or they show up
# as false walls in the distance field.
road = bytearray(comp)
seen = bytearray(W * H)
holes = 0
for i0 in range(W * H):
    if road[i0] or seen[i0]:
        continue
    q = deque([i0]); seen[i0] = 1; cells = []; border = False
    while q:
        i = q.popleft(); cells.append(i); x, y = i % W, i // W
        if x in (0, W-1) or y in (0, H-1): border = True
        for nx, ny in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)):
            if 0 <= nx < W and 0 <= ny < H:
                j = ny * W + nx
                if not road[j] and not seen[j]:
                    seen[j] = 1; q.append(j)
    if not border and len(cells) < 40000:
        holes += 1
        for i in cells: road[i] = 1
print("holes plugged:", holes, "road px:", sum(road))

INF = 10 ** 6
D = [0 if not road[i] else INF for i in range(W * H)]
for y in range(H):
    row = y * W
    for x in range(W):
        i = row + x
        if D[i] == 0: continue
        b = D[i]
        if x > 0: b = min(b, D[i-1] + 3)
        if y > 0:
            b = min(b, D[i-W] + 3)
            if x > 0: b = min(b, D[i-W-1] + 4)
            if x < W-1: b = min(b, D[i-W+1] + 4)
        D[i] = b
for y in range(H-1, -1, -1):
    row = y * W
    for x in range(W-1, -1, -1):
        i = row + x
        if D[i] == 0: continue
        b = D[i]
        if x < W-1: b = min(b, D[i+1] + 3)
        if y < H-1:
            b = min(b, D[i+W] + 3)
            if x < W-1: b = min(b, D[i+W+1] + 4)
            if x > 0: b = min(b, D[i+W-1] + 4)
        D[i] = b
print("distance field built")

# In two places the art runs two roads edge to edge with only their white
# edge lines between them, so the surface there is twice as wide as a road.
# Capping what extra clearance is worth to the walker stops it drifting into
# the middle of that pair and hopping onto the neighbouring road.
RIDGE_CAP = 38.0

def ridge(x, y):
    return min(dt(x, y), RIDGE_CAP)

def dt(x, y):
    ix, iy = int(x), int(y)
    if ix < 0 or iy < 0 or ix >= W or iy >= H: return 0.0
    return D[iy * W + ix] / 3.0

STEP = 7.0
FAN, FAN_STEP = 75, 5
SEARCH = 26.0

start = (float(SEED[0]), float(SEED[1]))
pos = [start[0], start[1]]
heading = 0.0
pts = []
closed = False

for step_i in range(2000):
    rad = math.radians(heading)
    dirv = (math.cos(rad), math.sin(rad))
    perp = (-dirv[1], dirv[0])
    best_t, best_d = 0.0, ridge(*pos)
    t = -SEARCH
    while t <= SEARCH:
        c = ridge(pos[0] + perp[0]*t, pos[1] + perp[1]*t)
        if c > best_d + 1e-9 or (abs(c - best_d) < 1e-9 and abs(t) < abs(best_t)):
            best_d, best_t = c, t
        t += 1.0
    pos[0] += perp[0] * best_t
    pos[1] += perp[1] * best_t
    pts.append((pos[0], pos[1], dt(*pos)))

    if step_i > 300 and math.hypot(pos[0]-start[0], pos[1]-start[1]) < 30.0:
        closed = True
        print("loop closed after", step_i, "steps")
        break

    best_score, best_h = -1e9, None
    for off in range(-FAN, FAN + 1, FAN_STEP):
        h = heading + off
        r = math.radians(h)
        dx, dy = math.cos(r), math.sin(r)
        ok = True
        for f in (0.5, 1.0, 1.5):
            ix, iy = int(pos[0] + dx*STEP*f), int(pos[1] + dy*STEP*f)
            if ix < 0 or iy < 0 or ix >= W or iy >= H or not road[iy*W + ix]:
                ok = False; break
        if not ok: continue
        score = ridge(pos[0] + dx*STEP*1.5, pos[1] + dy*STEP*1.5) - 0.10 * abs(off)
        if score > best_score:
            best_score, best_h = score, h
    if best_h is None:
        print("dead end at step", step_i, pos); break
    heading = best_h
    r = math.radians(heading)
    pos[0] += math.cos(r) * STEP
    pos[1] += math.sin(r) * STEP

print("points:", len(pts), "closed:", closed)
hw = sorted(p[2] for p in pts)
print("half-width px: min %.1f p10 %.1f median %.1f p90 %.1f max %.1f" %
      (hw[0], hw[len(hw)//10], hw[len(hw)//2], hw[9*len(hw)//10], hw[-1]))
print("half-width world: min %.2f median %.2f max %.2f" % (hw[0]/PPU, hw[len(hw)//2]/PPU, hw[-1]/PPU))

dbg = im.copy(); dr = ImageDraw.Draw(dbg)
for i in range(1, len(pts)):
    dr.line([pts[i-1][0], pts[i-1][1], pts[i][0], pts[i][1]], fill=(0, 255, 0), width=3)
for i in range(0, len(pts), 20):
    dr.ellipse([pts[i][0]-4, pts[i][1]-4, pts[i][0]+4, pts[i][1]+4], fill=(255, 255, 0))
dr.ellipse([start[0]-9, start[1]-9, start[0]+9, start[1]+9], outline=(0, 128, 255), width=4)
dbg.save("trace3_overlay.png")
json.dump(pts, open("trace3_px.json", "w"))
print("wrote trace3_overlay.png")
