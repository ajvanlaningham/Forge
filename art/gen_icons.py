"""Generate equipment icons in the house style (read from the hand-drawn dumbbell).

Shapes are drawn as shaded blocks — cream top edge (G), warm rim on the lit left side (D/F),
navy shade on the right and underneath (C), iron body (B) — then the script adds the black
outline (A) around everything and the slate cast shadow (E) outside the outline's lower edge.
Writes art/sprites/<name>.txt (30x30). Run, then `python3 art/render.py` to preview.
"""
from pathlib import Path

N = 30
OUT = Path(__file__).with_name('sprites')

class Sprite:
    def __init__(self): self.g = [['.'] * N for _ in range(N)]
    def put(self, x, y, c):
        if 0 <= x < N and 0 <= y < N: self.g[y][x] = c
    def fill(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1): self.put(x, y, c)
    def block(self, x0, y0, x1, y1, body='B', top='G', rim='D', shade='C'):
        """A lit 3/4 block: cream top, warm left rim, navy right/bottom, iron body."""
        self.fill(x0, y0, x1, y1, body)
        for y in range(y0, y1 + 1): self.put(x0, y, rim); self.put(x1, y, shade)
        for x in range(x0, x1 + 1): self.put(x, y1, shade)
        if y1 > y0:
            for x in range(x0, x1 + 1): self.put(x, y0, top)
    def rivet(self, x, y): self.put(x, y, 'H'); self.put(x, y + 1, 'I')
    def wheel(self, cx, cy, r, rim='F', body='B', hub='C'):
        """A flywheel: lit rim on the upper-left, shaded lower-right, dark hub."""
        for y in range(cy - r, cy + r + 1):
            for x in range(cx - r, cx + r + 1):
                d2 = (x - cx) ** 2 + (y - cy) ** 2
                if d2 <= r * r:
                    edge = d2 > (r - 1.3) ** 2
                    lit = (x - cx) + (y - cy) < 0
                    self.put(x, y, ('G' if lit else 'C') if edge else body)
        self.fill(cx - 1, cy - 1, cx, cy, hub)
    def finish(self, name):
        g = self.g
        filled = {(x, y) for y in range(N) for x in range(N) if g[y][x] != '.'}
        # Thin steel cable (W) and thin rope (u) are drawn bare: outlining a 1px line buries it.
        solid = {(x, y) for (x, y) in filled if g[y][x] not in 'Wu'}
        for y in range(N):
            for x in range(N):
                if (x, y) in filled: continue
                if any((x + dx, y + dy) in solid for dx in (-1, 0, 1) for dy in (-1, 0, 1) if dx or dy):
                    g[y][x] = 'A'
        outline = {(x, y) for y in range(N) for x in range(N) if g[y][x] == 'A'}
        for (x, y) in sorted(outline):
            # cast shadow: one step down-right of the lower outline, outside everything
            sx, sy = x + 1, y + 1
            if 0 <= sx < N and 0 <= sy < N and g[sy][sx] == '.' and (x, y + 1) not in filled:
                below_filled = any((x, yy) in filled for yy in range(max(0, y - 3), y))
                if below_filled: g[sy][sx] = 'E'
        (OUT / f'{name}.txt').write_text('\n'.join(''.join(r) for r in g) + '\n')
        print('wrote', name)

# --- bench: padded flat bench, 3/4 side ---
s = Sprite()
s.block(3, 10, 26, 14, body='B', top='G', rim='F')        # pad
s.fill(5, 13, 24, 13, 'C')                                 # pad underside seam
s.block(6, 15, 8, 21, body='B', top='B', rim='D')          # front leg
s.block(21, 15, 23, 21, body='B', top='B', rim='D')        # back leg
s.block(4, 21, 10, 22, body='B', top='F', rim='D')         # feet
s.block(19, 21, 25, 22, body='B', top='F', rim='D')
s.rivet(7, 17)
s.finish('bench')

# --- pull-up bar: tall wall brackets, bar across the top, foam grips ---
s = Sprite()
s.block(3, 3, 5, 24, body='B', top='G', rim='D')           # left bracket (tall: it's up high)
s.block(24, 3, 26, 24, body='B', top='G', rim='D')         # right bracket
s.block(2, 22, 6, 24, body='B', top='F', rim='D')          # wall plates
s.block(23, 22, 27, 24, body='B', top='F', rim='D')
s.fill(6, 5, 23, 5, 'G'); s.fill(6, 6, 23, 6, 'F'); s.fill(6, 7, 23, 7, 'C')   # bar
s.block(8, 4, 11, 8, body='D', top='F', rim='F', shade='C')      # grips
s.block(18, 4, 21, 8, body='D', top='F', rim='F', shade='C')
s.rivet(4, 14); s.rivet(25, 14)
s.finish('pull_up_bar')

# --- dip station: parallel handles on a stand ---
s = Sprite()
s.block(7, 9, 9, 22, body='B', top='B', rim='D')           # uprights
s.block(20, 9, 22, 22, body='B', top='B', rim='D')
s.block(3, 7, 12, 9, body='D', top='F', rim='F', shade='C')     # handles
s.block(17, 7, 26, 9, body='D', top='F', rim='F', shade='C')
s.fill(10, 15, 19, 15, 'F'); s.fill(10, 16, 19, 16, 'C')         # brace
s.block(4, 22, 25, 23, body='B', top='F', rim='D')         # base
s.rivet(8, 12); s.rivet(21, 12)
s.finish('dip_station')

# --- squat rack: two uprights with holes, J-hooks, a loaded bar ---
s = Sprite()
s.block(7, 3, 9, 24, body='B', top='G', rim='D')
s.block(20, 3, 22, 24, body='B', top='G', rim='D')
for y in range(6, 23, 3): s.put(8, y, 'C'); s.put(21, y, 'C')   # hole column
s.fill(1, 10, 28, 10, 'G'); s.fill(1, 11, 28, 11, 'F')          # bar on the hooks
s.block(2, 7, 4, 14, body='B', top='G', rim='D')            # plates
s.block(25, 7, 27, 14, body='B', top='G', rim='D')
s.block(5, 23, 11, 24, body='B', top='F', rim='D')          # feet
s.block(18, 23, 24, 24, body='B', top='F', rim='D')
s.rivet(10, 12); s.rivet(19, 12)                            # J-hooks
s.finish('squat_rack')

# --- weight vest: front view, weight pockets, buckle ---
s = Sprite()
s.block(8, 3, 11, 9, body='B', top='G', rim='D')            # shoulder straps
s.block(18, 3, 21, 9, body='B', top='G', rim='D')
s.block(6, 8, 23, 23, body='B', top='G', rim='D')           # torso
s.fill(12, 8, 17, 10, '.')                                  # neckline
for y in (12, 17):
    s.block(8, y, 13, y + 3, body='C', top='F', rim='C', shade='C')   # pockets
    s.block(16, y, 21, y + 3, body='C', top='F', rim='C', shade='C')
s.fill(14, 12, 15, 21, 'D')                                 # centre strap
s.rivet(14, 18); s.put(15, 18, 'H'); s.put(15, 19, 'I')    # buckle
s.finish('weight_vest')

# --- cable machine: frame, stack bottom-left, steel cable over two top pulleys to a handle ---
s = Sprite()
s.block(2, 2, 4, 25, body='B', top='G', rim='D')            # left upright
s.block(25, 2, 27, 25, body='B', top='G', rim='D')          # right upright
s.block(2, 2, 27, 3, body='B', top='G', rim='D')            # top beam
s.block(2, 24, 27, 25, body='B', top='F', rim='D')          # base
for i, y in enumerate(range(15, 23, 3)):
    s.block(6, y, 12, y + 1, body='B', top='G' if i == 0 else 'F', rim='D')   # weight stack
s.put(12, 18, 'H'); s.put(13, 18, 'I')                      # selector pin
s.block(8, 5, 10, 7, body='F', top='G', rim='F', shade='C')     # left pulley
s.block(19, 5, 21, 7, body='F', top='G', rim='F', shade='C')    # right pulley
for y in range(8, 15): s.put(9, y, 'W')                     # cable: stack up to the left pulley
for x in range(10, 20): s.put(x, 5, 'W')                    # across the top
for y in range(8, 16): s.put(20, y, 'W')                    # down from the right pulley
s.block(17, 16, 23, 18, body='D', top='F', rim='F', shade='C')  # D-handle
s.rivet(19, 16)
s.finish('cable_machine')

# --- machine: a 45-degree leg press — reclined seat, angled rails, loaded sled ---
s = Sprite()
s.block(2, 24, 27, 25, body='B', top='F', rim='D')          # base
for i in range(17):                                          # twin rails rising to the right
    x, y = 9 + i, 22 - i
    s.put(x, y, 'F'); s.put(x + 1, y, 'C')
    s.put(x + 2, y + 2, 'F'); s.put(x + 3, y + 2, 'C')
s.block(3, 19, 10, 21, body='B', top='G', rim='F')          # seat
s.block(2, 12, 4, 20, body='B', top='G', rim='F')           # backrest, reclined
s.block(15, 10, 21, 15, body='B', top='G', rim='D')         # sled / footplate
s.block(21, 6, 23, 12, body='B', top='G', rim='D')          # plates on the horn
s.block(24, 7, 25, 11, body='B', top='G', rim='D')
s.rivet(17, 12)
s.finish('machine')


# ================= Batch 2: conditioning gear =================

# --- bike: stationary bike, side view ---
s = Sprite()
s.block(3, 24, 26, 25, body='B', top='F', rim='D')          # base
s.wheel(20, 18, 5)                                          # flywheel
for i in range(12):                                          # frame: rear foot up to the seat post
    s.put(8 + i // 3, 23 - i, 'B'); s.put(9 + i // 3, 23 - i, 'C')
s.block(7, 9, 9, 13, body='B', top='B', rim='D')            # seat post
s.block(4, 7, 11, 8, body='D', top='F', rim='F', shade='C') # saddle
s.block(19, 7, 20, 13, body='B', top='B', rim='D')          # handlebar stem
s.block(17, 5, 24, 6, body='D', top='F', rim='F', shade='C')# bars
s.rivet(13, 17)                                             # crank
s.finish('bike')

# --- row machine: rail, sliding seat, flywheel housing, chain to the handle ---
s = Sprite()
s.block(2, 20, 27, 21, body='B', top='G', rim='D')          # rail
s.block(3, 22, 5, 24, body='B', top='B', rim='D')           # rear leg
s.block(23, 22, 26, 24, body='B', top='B', rim='D')         # front leg
s.wheel(22, 13, 5)                                          # flywheel housing
s.block(8, 16, 13, 19, body='D', top='F', rim='F', shade='C')   # seat
s.block(15, 15, 17, 19, body='B', top='G', rim='D')         # foot stretcher
for x in range(11, 18): s.put(x, 12, 'W')                   # chain
s.block(8, 10, 10, 14, body='D', top='F', rim='F', shade='C')   # handle
s.rivet(22, 12)
s.finish('row_machine')

# --- ski erg: tower, top housing, two cable handles, platform ---
s = Sprite()
s.block(11, 2, 18, 8, body='B', top='G', rim='D')           # top housing
s.wheel(14, 5, 2, rim='F', body='C', hub='C')
s.block(13, 9, 16, 23, body='B', top='B', rim='D')          # tower
s.block(5, 23, 24, 25, body='B', top='F', rim='D')          # platform
for y in range(8, 16): s.put(9, y, 'W'); s.put(20, y, 'W')  # cables
for x in (10, 19): s.put(x, 7, 'W')
s.block(7, 16, 10, 19, body='D', top='F', rim='F', shade='C')   # handles
s.block(19, 16, 22, 19, body='D', top='F', rim='F', shade='C')
s.rivet(14, 14)
s.finish('ski_erg')

# --- jump rope: two handles and a big loop of rope ---
s = Sprite()
import math
for t in range(0, 181, 3):                                   # rope arc over the top
    a = math.radians(t)
    x = round(14.5 - 10 * math.cos(a)); y = round(13 - 10 * math.sin(a))
    s.put(x, y, 'u')
s.block(3, 13, 6, 22, body='D', top='F', rim='F', shade='C')    # left handle
s.block(23, 13, 26, 22, body='D', top='F', rim='F', shade='C')  # right handle
s.rivet(4, 20); s.rivet(24, 20)                              # end caps
s.finish('jump_rope')

# --- battle rope: thick rope in waves to an anchor post ---
s = Sprite()
s.block(23, 6, 27, 24, body='B', top='G', rim='D')           # anchor post
s.rivet(25, 14)
for x in range(2, 23):                                       # 2px-thick wave, lit top / shaded bottom
    y = round(15 + 4 * math.sin((x - 2) / 3.2))
    s.put(x, y, 'u'); s.put(x, y + 1, 'v'); s.put(x, y + 2, 'v')
s.block(1, 13, 3, 19, body='D', top='F', rim='F', shade='C') # handle end
s.finish('battle_rope')

# --- sled: push sled — skids, two posts, plates on the centre horn ---
s = Sprite()
s.block(2, 22, 27, 23, body='B', top='G', rim='D')           # skids
s.put(1, 21, 'B'); s.put(2, 21, 'G')                          # upturned nose
s.block(5, 5, 7, 21, body='B', top='G', rim='D')             # left post
s.block(22, 5, 24, 21, body='B', top='G', rim='D')           # right post
s.block(11, 12, 18, 14, body='B', top='G', rim='D')          # plates on the horn
s.block(10, 15, 19, 18, body='B', top='F', rim='D')
s.block(11, 19, 18, 21, body='B', top='F', rim='D')
s.rivet(14, 9)                                               # horn top
s.fill(14, 10, 15, 11, 'F')
s.finish('sled')


# ================= Batch 3: mobility gear =================

# --- bands: two loop bands, wax red and tan ---
s = Sprite()
def ring(cx, cy, rx, ry, lit, body, shade, thick=2.2):
    for y in range(N):
        for x in range(N):
            d = ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2
            if 1 - thick / min(rx, ry) <= d <= 1:
                s.put(x, y, lit if (x - cx) + (y - cy) < -2 else body if (x - cx) + (y - cy) < 4 else shade)
ring(12, 13, 9, 7, 't', 'r', 's')
ring(18, 17, 8, 6, 'z', 'y', 'x')
s.finish('bands')

# --- mat: a rolled mat lying at an angle — the near end shows the rolled spiral ---
s = Sprite()
import math
P0, P1, R = (10.5, 18.5), (23.5, 10.5), 6.2            # near end (lower-left), far end, roll radius
ux, uy = P1[0] - P0[0], P1[1] - P0[1]; L = math.hypot(ux, uy); ux, uy = ux / L, uy / L
nx, ny = uy, -ux                                       # perpendicular, pointing up-left (toward the light)
def axial(x, y): return (x - P0[0]) * ux + (y - P0[1]) * uy, (x - P0[0]) * nx + (y - P0[1]) * ny
for y in range(N):                                     # the body: lit upper side, sage, shaded underside
    for x in range(N):
        a_, b_ = axial(x + 0.5, y + 0.5)
        if 0 <= a_ <= L and abs(b_) <= R:
            s.put(x, y, 'o' if b_ > R * 0.45 else 'e' if b_ < -R * 0.5 else 'm')
for t in (0.42, 0.74):                                 # two straps wrapping the roll
    for y in range(N):
        for x in range(N):
            a_, b_ = axial(x + 0.5, y + 0.5)
            if abs(a_ - t * L) <= 0.9 and abs(b_) <= R + 0.2:
                s.put(x, y, 'F' if b_ > R * 0.45 else 'D')
for y in range(N):                                     # near end cap: concentric rings of rolled layers
    for x in range(N):
        a_, b_ = axial(x + 0.5, y + 0.5)
        rr = math.sqrt((a_ / (R * 0.78)) ** 2 + (b_ / R) ** 2)
        if rr <= 1:
            ring = int(rr * 3.4)          # three bold rings: core, layer, outer wrap
            s.put(x, y, ('e', 'o', 'e', 'm')[min(ring, 3)])
s.rivet(round(P0[0] + 0.58 * L * ux + 2 * nx), round(P0[1] + 0.58 * L * uy + 2 * ny))
s.finish('mat')

# --- wall: a section of brick wall ---
s = Sprite()
s.fill(3, 4, 26, 25, 'w')                                         # mortar
for row, y in enumerate(range(4, 25, 3)):
    off = 0 if row % 2 == 0 else 3
    for x0 in range(3 - off, 27, 6):
        a, b = max(3, x0), min(26, x0 + 4)
        if b > a:
            s.fill(a, y, b, y + 1, 'y')
            for x in range(a, b + 1): s.put(x, y, 'z')            # lit top of each brick
s.finish('wall')

# --- box step: the padded hip-thrust box ---
s = Sprite()
s.block(4, 13, 25, 24, body='y', top='z', rim='z', shade='x')    # wooden box
s.block(3, 9, 26, 13, body='B', top='G', rim='F')                 # padded top
s.fill(5, 12, 24, 12, 'C')                                        # pad seam
s.rivet(14, 18)
s.finish('box_step')

# --- medicine ball: seams and a lit upper-left ---
s = Sprite()
s.wheel(15, 15, 10, rim='F', body='B', hub='B')
for x in range(6, 25): s.put(x, 15, 'C')                          # seams
for y in range(6, 25): s.put(15, y, 'C')
s.put(10, 10, 'G'); s.put(11, 10, 'G'); s.put(10, 11, 'G')       # shine
s.rivet(19, 19)
s.finish('medicine_ball')

# --- sandbag: rounded khaki canvas duffel, two handles, a strap ---
s = Sprite()
cx, cy, rx, ry = 14.5, 17, 12, 6
for y in range(N):
    for x in range(N):
        d = ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2
        if d <= 1:
            s.put(x, y, 'p' if y < cy - 3 else 'q' if y < cy + 3 else 'D')   # lit top, canvas, shaded belly
s.block(8, 7, 11, 12, body='D', top='F', rim='F', shade='C')     # handles
s.block(18, 7, 21, 12, body='D', top='F', rim='F', shade='C')
s.fill(9, 9, 10, 11, '.'); s.fill(19, 9, 20, 11, '.')             # handle loops
for y in range(12, 23): s.put(14, y, 'D'); s.put(15, y, 'D')      # strap
s.rivet(14, 16)
s.finish('sandbag')

# --- suspension trainer: anchor, two straps, handles ---
s = Sprite()
s.block(11, 1, 18, 4, body='B', top='G', rim='D')                 # anchor
for y in range(5, 18):                                            # straps (tan webbing, drawn bare)
    s.put(13 - (y - 5) // 3, y, 'u'); s.put(16 + (y - 5) // 3, y, 'u')
s.block(6, 18, 11, 20, body='D', top='F', rim='F', shade='C')     # handles
s.block(18, 18, 23, 20, body='D', top='F', rim='F', shade='C')
s.rivet(14, 2)
s.finish('suspension_trainer')


# --- punching bag: heavy bag hung from a mount by two chains in a V ---
s = Sprite()
s.block(13, 0, 16, 1, body='B', top='G', rim='D')                 # mount
for i in range(6):                                                # chains fanning out to the bag's corners
    s.put(14 - i, 2 + i, 'W'); s.put(15 + i, 2 + i, 'W')
s.block(8, 8, 21, 26, body='r', top='t', rim='t', shade='s')      # the bag
for (x, y) in [(8, 8), (21, 8), (8, 26), (21, 26)]: s.put(x, y, '.')   # rounded corners
s.fill(8, 9, 21, 9, 't')                                          # stitched top edge
s.fill(8, 13, 21, 13, 's'); s.fill(8, 22, 21, 22, 's')            # bands
s.fill(10, 10, 11, 20, 't')                                       # leather shine
s.rivet(18, 16)
s.finish('punching_bag')
