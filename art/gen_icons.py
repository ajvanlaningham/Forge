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
    def finish(self, name):
        g = self.g
        filled = {(x, y) for y in range(N) for x in range(N) if g[y][x] != '.'}
        # Thin steel cable (W) is drawn bare: outlining a 1px line would bury it in black.
        solid = {(x, y) for (x, y) in filled if g[y][x] != 'W'}
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
