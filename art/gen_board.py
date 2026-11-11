"""Generate the quest-board wood texture as a text grid (deterministic). Writes art/sprites/board_wood.txt."""
import random
from pathlib import Path

W, H = 90, 190          # grid cells; rendered x12 -> 1080 x 2280, about a phone screen
PLANK = 19              # plank height in cells (vertical planks would read as a fence; boards are horizontal)
rng = random.Random(1171)  # fixed seed: the board never changes between renders

g = [['y'] * W for _ in range(H)]
for top in range(0, H, PLANK):
    # gap line between planks, then a lit top edge and a shaded bottom edge
    for x in range(W):
        g[top][x] = 'w'
        if top + 1 < H: g[top + 1][x] = 'z'
        if top + PLANK - 1 < H: g[top + PLANK - 1][x] = 'x'
    # plank joints: offset each row so seams don't line up
    seam = rng.randrange(20, W - 20)
    for y in range(top + 1, min(top + PLANK, H)):
        g[y][seam] = 'w'
        if seam + 1 < W: g[y][seam + 1] = 'x'
    # grain: long, slightly wavy dark lines along the plank
    for _ in range(4):
        y = rng.randrange(top + 3, min(top + PLANK - 2, H))
        x = rng.randrange(0, W // 2)
        length = rng.randrange(18, 48)
        for i in range(length):
            if x + i >= W: break
            if g[y][x + i] == 'y': g[y][x + i] = 'k' if i % 7 else 'x'
            if rng.random() < 0.08: y = max(top + 3, min(top + PLANK - 3, y + rng.choice((-1, 1))))
    # a knot or two
    for _ in range(rng.randrange(0, 2)):
        kx, ky = rng.randrange(6, W - 6), rng.randrange(top + 6, min(top + PLANK - 5, H - 1))
        for dx, dy in [(0,0),(1,0),(-1,0),(0,1),(0,-1)]:
            g[ky + dy][kx + dx] = 'k'
        for dx, dy in [(2,0),(-2,0),(0,2),(0,-2),(1,1),(-1,-1),(1,-1),(-1,1)]:
            if g[ky + dy][kx + dx] == 'y': g[ky + dy][kx + dx] = 'x'

Path(__file__).with_name('sprites').joinpath('board_wood.txt').write_text('\n'.join(''.join(r) for r in g) + '\n')
print(f'board_wood: {W}x{H}')
