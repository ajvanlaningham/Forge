"""The quest map's landscape (2026-11-13, second pass): paper, scenery, and the week banner's tails.

    python3 art/gen_mapscape.py && python3 art/render.py --install

The map is a sheet of parchment, so its scenery is drawn the way an old map would draw it: dark ink
outlines over muted watercolour fills, and ink hatching on the shaded side of every hill and
mountain. Most pieces are procedural so a region can scatter several different trees or peaks
instead of repeating one sprite. Writes art/sprites/map_paper.txt, map_s_*.txt and map_ribbon_*.txt.
"""
import math
import random
from pathlib import Path

ART = Path(__file__).resolve().parent
OUT = ART / 'sprites'
APP_SIZES = ART.parent / 'Forge' / 'Forge' / 'Views' / 'MapScenery.g.cs'
INK = 'w'  # #2B1D14 in the house palette

# Watercolour fills, shared by the scenery. Keys are local to these sprites ('#!' headers).
COLOURS = {
    # greens: pine (cool) and broadleaf (warm)
    '1': '#4E6B45', '2': '#6F8C5C', '3': '#3A5236',
    '7': '#6E8240', '8': '#93A55A', '9': '#4F6230',
    # stone and rock
    '4': '#C2B497', '5': '#9C8F74', '6': '#7C705A',
    # snow (lit, shaded) and water
    'G': '#EEE8D6', 'S': '#C9CCC8', 'b': '#7FA3B8', 'c': '#A9C6D4', 'd': '#55798F',
    # wood, thatch, ember, smoke, dragon
    'y': '#6E4E33', 'x': '#4A3222', 'z': '#8C6844', 'T': '#B99A5B', 'U': '#8E7442',
    'I': '#E09145', 'H': '#E0C045', 'L': '#9A958A', 'K': '#BDB8AC',
    'B': '#7A5A44', 'D': '#A8805C',
    'r': '#8E2A2A', 's': '#5E1A1A', 't': '#B84A3A', 'k': '#3A271B',
}


class Grid:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.g = [['.'] * w for _ in range(h)]

    def put(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.g[y][x] = c

    def get(self, x, y):
        return self.g[y][x] if 0 <= x < self.w and 0 <= y < self.h else '.'

    def outline(self):
        """Ink every empty cell touching the drawing (4-neighbour): the pen line around a shape."""
        marks = [(x, y) for y in range(self.h) for x in range(self.w) if self.g[y][x] == '.'
                 and any(self.get(x + a, y + b) not in '.' + INK for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1)))]
        for x, y in marks:
            self.g[y][x] = INK
        return self

    def paste(self, other, ox, oy):
        for y in range(other.h):
            for x in range(other.w):
                if other.g[y][x] != '.':
                    self.put(ox + x, oy + y, other.g[y][x])
        return self

    def rows(self):
        return [''.join(r) for r in self.g]


def pad(g, l=1, t=1, r=1, b=1):
    out = Grid(g.w + l + r, g.h + t + b)
    return out.paste(g, l, t)


# --- trees -----------------------------------------------------------------------------------------

def pine(h, w, seed):
    """A fir in saw-toothed tiers: lit on the left, shaded right, on a short trunk."""
    rnd = random.Random(seed)
    g = Grid(w, h + 2)
    cx = (w - 1) / 2
    tier = 3 + rnd.randint(0, 1)
    for y in range(h):
        grow = (y + 1) / h
        saw = 0.55 + 0.45 * ((y % tier) / (tier - 1))
        half = max(0.6, grow * (w / 2) * saw)
        for x in range(w):
            d = x - cx
            if abs(d) <= half:
                g.put(x, y, '2' if d < -half * 0.35 else '3' if d > half * 0.4 else '1')
    for y in range(h, h + 2):
        g.put(int(cx), y, 'y')
        if w % 2 == 0:
            g.put(int(cx) + 1, y, 'x')
    return pad(g).outline()


def oak(r, seed):
    """A broadleaf: a lumpy round crown, lit from the upper left, on a forked trunk."""
    rnd = random.Random(seed)
    lumps, phase = rnd.randint(5, 7), rnd.random() * 6
    size = 2 * r + 3
    g = Grid(size, size + 3)
    c = r + 1
    for y in range(size):
        for x in range(size):
            dx, dy = x - c, y - c
            a = math.atan2(dy, dx)
            edge = r + 0.9 * math.sin(a * lumps + phase)
            d = math.hypot(dx, dy)
            if d <= edge:
                lit = dx + dy < -r * 0.5
                shade = dx + dy > r * 0.6
                g.put(x, y, '8' if lit else '9' if shade else '7')
    # a few highlight dabs inside the crown
    for _ in range(r):
        x, y = c + rnd.randint(-r + 1, 0), c + rnd.randint(-r + 1, 0)
        if g.get(x, y) == '7':
            g.put(x, y, '8')
    base = max(y for y in range(size) for x in range(size) if g.get(x, y) != '.')
    for y in range(base - 1, base + 3):
        g.put(c, y, 'y')
    g.put(c - 1, base + 2, 'x')
    g.put(c + 1, base + 2, 'x')
    return pad(g).outline()


EMBERS = """\
.......H............
......HIH.....I.....
.......I...........H
....BBBBB...........
...BDDBBBB..BBBB....
..BDDBBBIBB.BDBBB...
..BBBBIIBBBBBBDBBB..
.BBBBBBBBBBBBBBBBBB.
"""

DEAD_TREE = """\
.......y.....
..y....y..y..
...y..yy.y...
....y.y.yy...
.y...yyyy....
..yy..yy...y.
....y.yy..y..
.....yyyyy...
......yy.....
......yy.....
.....yyyy....
"""


def from_text(text):
    rows = text.splitlines()
    g = Grid(max(len(r) for r in rows), len(rows))
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch != '.':
                g.put(x, y, ch)
    return g


# --- hills and mountains ---------------------------------------------------------------------------

def mountain(w, h, seed, snow=True, rock=('4', '5', '6')):
    """An inked peak: a jagged silhouette, lit left face, hatched right face, optional snowcap."""
    rnd = random.Random(seed)
    lit, mid, shade = rock
    g = Grid(w, h)
    px = int(w * rnd.uniform(0.38, 0.55))
    left = [0] * h
    right = [0] * h
    jag = 0
    for y in range(h):
        t = y / (h - 1)
        if y % 2 == 0:
            jag = rnd.choice((-1, 0, 0, 1))
        left[y] = round(px - t * px + (jag if 0 < y < h - 1 else 0))
        right[y] = round(px + t * (w - 1 - px) - (jag if 0 < y < h - 1 else 0))
    snow_line = int(h * rnd.uniform(0.22, 0.32))
    for y in range(h):
        ridge = px + y * 0.35  # the ridge from the summit, between the lit and shaded faces
        for x in range(max(0, left[y]), min(w, right[y] + 1)):
            if snow and y <= snow_line + (1 if (x * 7 + y) % 3 == 0 else 0):
                c = 'G' if x <= ridge else 'S'
            elif x <= ridge:
                c = mid if (x * 3 + y * 7) % 13 == 0 else lit
            else:
                c = INK if (x - y) % 3 == 0 and x > ridge + 1 else shade
            g.put(x, y, c)
    return g


def range_of(peaks, seed, snow=True):
    """Several peaks overlapping: the back ones first, each with its own pen line."""
    rnd = random.Random(seed)
    w = sum(p[0] for p in peaks) * 2 // 3 + 4
    h = max(p[1] for p in peaks) + 2
    g = Grid(w, h)
    x = 1
    order = []
    for i, (pw, ph) in enumerate(peaks):
        order.append((ph, x, pw, ph, i))
        x += pw * 2 // 3 - rnd.randint(0, 2)
    for ph, ox, pw, _, i in sorted(order, reverse=True):  # tallest first: shorter peaks sit in front
        m = pad(mountain(pw, ph, seed * 10 + i, snow), 1, 1, 1, 0).outline()
        g.paste(m, ox - 1, h - m.h)
    return pad(g, 0, 0, 0, 1).outline()


def hill(w, h, seed):
    """A rolling green down, hatched on its shaded flank, with a couple of grass tufts."""
    rnd = random.Random(seed)
    g = Grid(w, h)
    cx = w / 2
    for y in range(h):
        for x in range(w):
            nx, ny = (x + .5 - cx) / (w / 2), (h - y - .5) / h
            if nx * nx + ny * ny <= 1:
                if nx > 0.25 and (x - y) % 3 == 0:
                    c = INK
                else:
                    c = '8' if nx < -0.2 and ny > 0.35 else '9' if nx > 0.3 else '7'
                g.put(x, y, c)
    for _ in range(2):
        tx = rnd.randint(2, w - 3)
        ty = next((y for y in range(h) if g.get(tx, y) != '.'), h - 1) + 2
        g.put(tx, ty, '9')
    return pad(g, 1, 1, 1, 0).outline()


def volcano(seed):
    """A smoking cone with a glowing crater and a lava runnel."""
    rnd = random.Random(seed)
    w, h = 26, 15
    g = Grid(w, h + 6)
    top = 7
    for y in range(h):
        t = y / (h - 1)
        half = top / 2 + t * (w / 2 - top / 2)
        for x in range(w):
            d = x + .5 - w / 2
            if abs(d) <= half:
                if y == 0:
                    c = 'I' if abs(d) < top / 2 - 1 else 'H'
                elif d > half * 0.2 and (x - y) % 3 == 0:
                    c = INK
                else:
                    c = '5' if d < -half * 0.3 else '6'
                g.put(x, y + 6, c)
    # lava runnel down the lit face
    x = w // 2 - 2
    for y in range(1, 8):
        g.put(x, y + 6, 'I' if y % 3 else 'H')
        x -= rnd.choice((0, 1))
    # smoke puffs
    for (sx, sy) in ((13, 4), (15, 2), (17, 0)):  # three puffs drifting off to the right
        for xx, yy in ((0, 0), (1, 0), (-1, 0), (0, -1), (1, -1)):
            g.put(sx + xx, sy + yy, 'L' if yy == 0 and xx >= 0 else 'K')
    return pad(g).outline()


# --- buildings and odds and ends -------------------------------------------------------------------

def tower(seed):
    """A ruined watchtower: coursed stone, a broken crown, an arched door and one dark window."""
    rnd = random.Random(seed)
    w, h = 9, 20
    g = Grid(w, h)
    crown = [rnd.randint(0, 4) for _ in range(w)]
    crown[0] = crown[-1] = max(crown[0], 2)
    for x in range(w):
        for y in range(crown[x], h):
            c = '4' if x < 3 else '6' if x >= w - 2 else '5'
            if y % 4 == 3 and (x + y // 4) % 3 == 0:
                c = '6'
            g.put(x, y, c)
    g.put(4, 8, 'k'); g.put(4, 9, 'k')
    for y in range(h - 4, h):
        for x in (3, 4, 5):
            if not (y == h - 4 and x != 4):
                g.put(x, y, 'k')
    # fallen stones at the foot
    out = Grid(w + 6, h + 1)
    out.paste(g, 2, 0)
    for x, y in ((0, h), (1, h), (w + 3, h), (w + 4, h), (w + 4, h - 1)):
        out.put(x, y, '5')
    return pad(out).outline()


def cottage(seed):
    """A thatched cottage with a lit window and a curl of chimney smoke."""
    w = 16
    g = Grid(w, 17)
    # smoke
    for x, y in ((12, 0), (13, 1), (12, 2), (11, 3)):
        g.put(x, y, 'K')
    for x, y in ((13, 0), (12, 1), (12, 3)):
        g.put(x, y, 'L')
    # chimney
    for y in range(4, 8):
        g.put(11, y, '6'); g.put(12, y, '5')
    # thatch roof
    for y in range(6):
        for x in range(1 + 5 - y, w - 1 - 5 + y):
            g.put(x, y + 5, 'T' if (x + y) % 4 else 'U')
    for x in range(0, w):
        g.put(x, 11, 'U')
    # walls, door, window
    for y in range(12, 17):
        for x in range(1, w - 1):
            g.put(x, y, 'G' if x < w - 4 else 'K')
    for y in range(13, 17):
        g.put(4, y, 'x'); g.put(5, y, 'x')
    for x in (9, 10):
        for y in (13, 14):
            g.put(x, y, 'H')
    return pad(g).outline()


def bridge():
    """A wooden bridge, seen side-on: a railed deck over an arch the river runs under."""
    w, h = 26, 10
    g = Grid(w, h)
    for x in range(w):
        if x % 5 == 0:
            g.put(x, 0, 'x')
        g.put(x, 1, 'x')
        g.put(x, 3, 'z')
        g.put(x, 4, 'y')
        if x % 5 == 0:
            g.put(x, 2, 'x')
    for y in range(5, h):
        for x in range(w):
            nx = (x + .5 - w / 2) / (w / 2 - 3)
            ny = (y - 4.5) / (h - 4)
            if nx * nx + (1 - ny) ** 2 > 1 or abs(nx) > 1:
                g.put(x, y, '5' if x < w / 2 else '6')
    return pad(g).outline()


def standing_stones(seed):
    """A ring of old standing stones."""
    rnd = random.Random(seed)
    g = Grid(24, 11)
    for i, sx in enumerate((1, 6, 12, 18)):
        hgt = rnd.randint(5, 9) if i != 2 else 10
        for y in range(11 - hgt, 11):
            for x in range(sx, sx + 3):
                g.put(x, y, '4' if x == sx else '6' if x == sx + 2 else '5')
        g.put(sx + 1, 11 - hgt, '.') if hgt < 9 else None
    # a lintel over the middle pair
    for x in range(11, 16):
        g.put(x, 0, '5')
    return pad(g).outline()


def reeds(seed):
    rnd = random.Random(seed)
    g = Grid(12, 9)
    for x in range(0, 12, 2):
        top = rnd.randint(0, 4)
        for y in range(top, 9):
            g.put(x + rnd.choice((0, 0, 1)) if y < 3 else x, y, '9' if y > 5 else '7')
        if rnd.random() < 0.6:
            g.put(x, top, 'y')
            g.put(x, top + 1, 'y')
    return pad(g).outline()


SERPENT = """\
......................ww...
.....................w77w..
....................w7H77w.
..........www......w77ww9w.
...www...w777w....w779w....
..w777w.w77997w..w7799w....
.w77997ww7999.9w.w799w.....
.w79..9ww79....9ww799w.....
"""

DRAGON = """\
......w..............w........
.....wtw............wtw.......
.....wttw..........wttw.......
....wttstw........wttstw......
....wtsstw.......wtsstw.......
...wtssstw......wtssstw.......
...wtsssstw....wtssssw........
..wtssrsssw...wtssrssw...www..
..wsswrsssww.wtssrssw...wrrrw.
..ww.wwrsssrwwsssrsww..wrrHrw.
......wwrrrrrrrrrrrrwwwrrrrww.
....wwwrrrrrrrrrrrrrrrrrw.....
...wrrrrwrrrrrrrrrrrrrww......
..wrrwwwwrrwwwwwwrrwww........
.wrrw...wsw.....wsw...........
.wrw....ww......ww............
..wrw.........................
...ww.........................
"""


def dwarf_gate(seed):
    """A mountain with a carved gate and a lit doorway: the end of the road."""
    m = mountain(30, 20, seed, snow=True)
    # carve the gate into the foot: a pillared arch, door glowing gold
    cx = 14
    for y in range(12, 20):
        for x in range(cx - 4, cx + 5):
            edge = abs(x - cx) == 4
            inside = abs(x - cx) <= 2 and (y > 13 or abs(x - cx) <= 1)
            if inside:
                m.put(x, y, 'H' if y > 14 else 'I')
            elif edge or y == 12:
                m.put(x, y, '4')
    return pad(m).outline()


# --- the paper -------------------------------------------------------------------------------------

PAPER = {'p': '#D9C9A3', 'P': '#DFD0AD', 'q': '#D1BF97', 'Q': '#C9B68C', 'f': '#C2AE83', 'g': '#AD9A70'}


def paper(n=64, seed=11):
    """A seamless tile of laid parchment: blotchy tone, horizontal fibres and the odd speck."""
    rnd = random.Random(seed)

    def lattice(step):
        cells = n // step
        v = [[rnd.random() for _ in range(cells)] for _ in range(cells)]

        def at(x, y):
            gx, gy = x / step, y / step
            x0, y0 = int(gx) % cells, int(gy) % cells
            x1, y1 = (x0 + 1) % cells, (y0 + 1) % cells
            fx, fy = gx - int(gx), gy - int(gy)
            fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
            top = v[y0][x0] * (1 - fx) + v[y0][x1] * fx
            bot = v[y1][x0] * (1 - fx) + v[y1][x1] * fx
            return top * (1 - fy) + bot * fy
        return at

    big, small = lattice(16), lattice(4)
    g = Grid(n, n)
    for y in range(n):
        for x in range(n):
            v = big(x, y) * 0.7 + small(x, y) * 0.3
            g.put(x, y, 'P' if v > 0.68 else 'p' if v > 0.47 else 'q' if v > 0.33 else 'Q')
    # fibres: short horizontal strokes, wrapping around the tile edge
    for _ in range(9):
        x, y, ln = rnd.randrange(n), rnd.randrange(n), rnd.randint(2, 3)
        for i in range(ln):
            if g.get((x + i) % n, y) in 'pq':
                g.put((x + i) % n, y, 'Q')
    for _ in range(10):
        g.put(rnd.randrange(n), rnd.randrange(n), 'f' if rnd.random() < 0.7 else 'g')
    return g


# --- the week banner's tails -----------------------------------------------------------------------

WEEKS = {  # face, light, dark, fold (the ribbon's underside)
    1: ('#B8462A', '#E07A4E', '#7A2C18', '#4E1A0E'),
    2: ('#3F7A3A', '#6FA85F', '#28501F', '#183314'),
    3: ('#2A6496', '#5A93C4', '#1A4266', '#0F2A42'),
    4: ('#8A6A12', '#C9A43A', '#5C470B', '#3A2C06'),
}


BAND_ROWS = 20  # the app draws the band 80 dp tall (4 dp per pixel); the tails are drawn to match


def ribbon_tail():
    """The left tail of the week banner, 14 x 26. The band (drawn in the app) overlaps its inner
    two columns for the top 20 rows; the tail hangs 6 rows lower, behind the band, joined to it by
    the dark underside of the fold, and ends in a swallowtail notch."""
    w, drop = 14, 6
    h = BAND_ROWS + drop
    g = Grid(w, h)
    for y in range(drop, h - 1):  # the last row is left for the pen line
        k = y - drop
        rows = h - 1 - drop
        notch = 1 + 5 - abs(k - (rows - 1) / 2) * 5 / ((rows - 1) / 2)
        for x in range(w):
            if x < round(notch):
                continue
            c = '2' if k == 0 else '3' if k >= rows - 3 else '1'
            if x >= w - 3:
                c = '3'  # the tail darkens as it runs behind the band
            g.put(x, y, c)
    # the fold: the ribbon's underside showing where the tail turns up behind the band
    # The fold. Below the band the tail's inner corner is cut on the slant, from the band's bottom
    # corner down to the tail's lower edge: cloth turning back on itself, not a box. The dark
    # triangle inside the slant is the ribbon's underside showing where it turns.
    fold = 5
    rows = (h - 2) - BAND_ROWS + 1
    for y in range(BAND_ROWS, h - 1):
        r = y - BAND_ROWS
        slant = (w - 1) - round((r + 1) * fold / rows)
        for x in range(w):
            if x > slant:
                g.put(x, y, '.')
            elif x >= (w - 1) - fold:
                g.put(x, y, '4')
    out = Grid(w, h)
    out.paste(g, 0, 0)
    for y in range(h):
        for x in range(w):
            if out.get(x, y) == '.' and any(out.get(x + a, y + b) not in '.A' for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                out.g[y][x] = 'A'
    return out


# --- writing ---------------------------------------------------------------------------------------

def write(name, grid, colours):
    used = {ch for row in grid.rows() for ch in row}
    head = ''.join(f'#! {k} {v}\n' for k, v in colours.items() if k in used)
    (OUT / f'{name}.txt').write_text(head + '\n'.join(grid.rows()) + '\n')


def main():
    for old in OUT.glob('map_s_*.txt'):
        old.unlink()
    c = COLOURS
    pieces = {
        'pine_a': pine(14, 9, 1), 'pine_b': pine(10, 7, 2), 'pine_c': pine(17, 11, 3),
        'oak_a': oak(6, 4), 'oak_b': oak(5, 5), 'oak_c': oak(7, 6),
        'dead': pad(from_text(DEAD_TREE)).outline(),
        'embers': pad(from_text(EMBERS)).outline(),
        'peak_a': pad(mountain(24, 16, 7), 1, 1, 1, 0).outline(),
        'peak_b': pad(mountain(18, 13, 8), 1, 1, 1, 0).outline(),
        'range_a': range_of([(20, 13), (26, 18), (18, 11)], 9),
        'range_b': range_of([(16, 10), (22, 15), (14, 9), (18, 12)], 10),
        'range_s': range_of([(14, 10), (18, 13), (12, 8)], 22),
        'crags': range_of([(14, 9), (18, 12), (12, 8)], 12, snow=False),
        'hill_a': hill(24, 9, 13), 'hill_b': hill(18, 7, 14),
        'volcano': volcano(15),
        'tower': tower(16),
        'cottage': cottage(17),
        'bridge': bridge(),
        'stones': standing_stones(18),
        'reeds': reeds(19),
        'serpent': from_text(SERPENT),
        'dragon': from_text(DRAGON),
        'gate': dwarf_gate(21),
    }
    for name, grid in pieces.items():
        write(f'map_s_{name}', grid, c)
    # The app lays scenery out by size (3 dp per pixel), so it gets the table generated here.
    sizes = ',\n'.join(f'        ["{n}"] = ({g.w}, {g.h})' for n, g in pieces.items())
    APP_SIZES.write_text(
        '// <auto-generated> by art/gen_mapscape.py; do not edit. </auto-generated>\n'
        'namespace Forge.Views;\n\n'
        'internal static class MapScenery\n{\n'
        '    /// <summary>Each scenery sprite\'s size in art pixels (map_s_&lt;name&gt;.png).</summary>\n'
        '    public static readonly IReadOnlyDictionary<string, (int W, int H)> Size = new Dictionary<string, (int W, int H)>\n'
        '    {\n' + sizes + '\n    };\n}\n')
    write('map_paper', paper(), PAPER)
    tail = ribbon_tail()
    for w, (face, light, dark, fold) in WEEKS.items():
        cols = {'1': face, '2': light, '3': dark, '4': fold}
        write(f'map_ribbon_w{w}_l', tail, cols)
        flipped = Grid(tail.w, tail.h)
        flipped.g = [row[::-1] for row in tail.g]
        write(f'map_ribbon_w{w}_r', flipped, cols)
    print(f'wrote {len(pieces)} scenery pieces, the paper and 8 ribbon tails')


if __name__ == '__main__':
    main()
