from PIL import Image
from collections import deque

SRC = r"C:\_DarkMatter\UnityProjects\Bajaj Racing Game\Assets\Darkmatter\Art\atlas\UI\1.png"
WORLD_W, WORLD_H = 230.4, 153.6
im = Image.open(SRC).convert("RGB")
W, H = im.size
px = im.load()
PPU = W / WORLD_W

def hue_of(r, g, b, v, sat):
    if sat == 0: return 0.0
    if v == r:   return (60 * ((g - b) / sat)) % 360
    if v == g:   return 60 * ((b - r) / sat) + 120
    return 60 * ((r - g) / sat) + 240

def free_at(x, y):
    """The racing surface: asphalt reads distinctly blue for its brightness,
    and the paint on it - white lines, cyan and orange chevrons - counts as
    surface too. Kerbs, the grey shoulder and the vignette do not."""
    r, g, b = px[x, y]
    v = max(r, g, b); sat = v - min(r, g, b)
    if v >= 165 and sat <= 50:                 # white lane and edge paint
        return True
    if (b - r) >= 0.26 * v and v >= 12:         # asphalt, including in shadow
        return True
    # The chevrons and the orange boost pads are deliberately NOT surface: a
    # pad can sit across the gap between two arms of a hairpin, and counting it
    # in bridges the two into a shortcut. They are plugged back as holes below.
    return False

FREE = bytearray(W * H)
for y in range(H):
    row = y * W
    for x in range(W):
        FREE[row + x] = 1 if free_at(x, y) else 0
print("free px:", sum(FREE))


SEED = (int((-0.5 / WORLD_W + 0.5) * W + 105), int((62.6 / WORLD_H + 0.5) * H))
comp = bytearray(W * H)
i0 = SEED[1] * W + SEED[0]
assert FREE[i0], "seed not on the surface"
q = deque([i0]); comp[i0] = 1; n = 1
while q:
    i = q.popleft(); x, y = i % W, i // W
    for nx, ny in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)):
        if 0 <= nx < W and 0 <= ny < H:
            j = ny * W + nx
            if FREE[j] and not comp[j]:
                comp[j] = 1; n += 1; q.append(j)
print("component:", n)

if __name__ == "__main__":
    out = im.copy(); op = out.load()
    for y in range(H):
        for x in range(W):
            if comp[y*W + x]:
                r, g, b = op[x, y]
                op[x, y] = (min(255, r + 110), g, b // 3)
    out.save("free_overlay.png")
    print("wrote free_overlay.png")
