"""Quest-map art (plan phase 4, 2026-11-13): the stones of the path, the campfire and the chest.

    python3 art/gen_map.py && python3 art/render.py --install

Writes art/sprites/map_*.txt in the house style (black outline, slate cast shadow, cream top-lit
rim). Stones are chunky "buttons" with a darker side, Duolingo-style, one colour per week of the
quest (Kindling ember, Tempering moss, Quenching river, Honing gold) plus grey for weeks ahead and
passed. The app overlays the day's icon as a glyph, so one stone serves every day type.
"""
from pathlib import Path

ART = Path(__file__).resolve().parent
OUT = ART / 'sprites'

# name: (face, light rim, dark side). Faces pass 4.5:1 against the cream glyph (#FFF8E7).
STONES = {
    'map_stone_w1': ('#B8462A', '#E07A4E', '#7A2C18'),   # Kindling: embers
    'map_stone_w2': ('#3F7A3A', '#6FA85F', '#28501F'),   # Tempering: moss
    'map_stone_w3': ('#2A6496', '#5A93C4', '#1A4266'),   # Quenching: river
    'map_stone_w4': ('#8A6A12', '#C9A43A', '#5C470B'),   # Honing: gold
}
# Days not yet reached (or passed by) are sketched in ink on the map rather than painted: a pale
# face, an ink pen line, hatching on the side and a parchment shadow (2026-11-13).
INKED = {
    'map_stone_ahead': ('#EDE3C8', '#F7F1E0', '#CBB78E'),
    'map_stone_passed': ('#DDD3BC', '#E8E0CC', '#BBAD90'),
}

W, H = 34, 30
CX, CY, RX, RY, DEPTH = 16.5, 11.5, 15.0, 10.5, 6


def inside_top(x, y, cy=CY):
    return ((x + .5 - CX) / RX) ** 2 + ((y + .5 - cy) / RY) ** 2 <= 1.0


def stone():
    grid = [['.'] * W for _ in range(H)]
    body = {(x, y) for y in range(H) for x in range(W)
            if inside_top(x, y) or inside_top(x, y, CY + DEPTH) or
            (abs(x + .5 - CX) <= RX and CY <= y + .5 <= CY + DEPTH)}
    for (x, y) in body:
        if inside_top(x, y):
            # Top face, with a lit rim on the upper-left and a cream glint.
            nx, ny = (x + .5 - CX) / RX, (y + .5 - CY) / RY
            rim = nx * nx + ny * ny > 0.62 and (nx + ny) < -0.2
            grid[y][x] = '2' if rim else '1'
        else:
            grid[y][x] = '3'
    for (x, y) in [(9, 5), (10, 5), (8, 6)]:
        grid[y][x] = 'G'
    # Outline: transparent pixels touching the body.
    for y in range(H):
        for x in range(W):
            if (x, y) in body:
                continue
            if any((x + dx, y + dy) in body for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                grid[y][x] = 'A'
    # Cast shadow under the outline.
    for x in range(W):
        col = [y for y in range(H) if grid[y][x] == 'A']
        if col and max(col) + 1 < H and 4 <= x < W - 4:
            grid[max(col) + 1][x] = 'E'
    return [''.join(r) for r in grid]


CAMPFIRE = """\
..........H.........
.........HGH........
........HIGIH.......
.......HIIGIIH......
......HIIIHIIIH.....
......HIIHHHIIH.....
.....AHIHHGHHIHA....
.....AIIHGGGHIIA....
......AIHHGHHIA.....
.......AAIIIAA......
...AAAAzzAAAzzAAAA..
..AzzyyyyAyyyyyzzA..
..AyyxxxxAxxxxxyyA..
...AAAAkkAAAkkAAAA..
....EEEEEEEEEEEEEE..
"""

CHEST_SHUT = """\
....AAAAAAAAAAAAAA....
...AzzzzzzzzzzzzzzA...
..AzyyyyyyyyyyyyyyzA..
..AyyyyyyyyyyyyyyyyA..
..ALLLLLLLLLLLLLLLLA..
..AxBBBBBAHHABBBBBxA..
..AyyyyyyAHIAyyyyyyA..
..AyyyyyyyAAyyyyyyyA..
..AxxxxxxxxxxxxxxxxA..
..AyyyyyyyyyyyyyyyyA..
..ALLLLLLLLLLLLLLLLA..
..AxxxxxxxxxxxxxxxxA..
..AAAAAAAAAAAAAAAAAA..
...EEEEEEEEEEEEEEEE...
"""

CHEST_OPEN = """\
...AAAAAAAAAAAAAAAA...
..AzzzzzzzzzzzzzzzzA..
..AyyyyyyyyyyyyyyyyA..
..AxxxxxxxxxxxxxxxxA..
..AAAAAAAAAAAAAAAAAA..
..AGHGHHGHHGHHHGHGHA..
..AHHIHHHIHHIHHHIHHA..
..ALLLLLLLLLLLLLLLLA..
..AxBBBBBAHHABBBBBxA..
..AyyyyyyAHIAyyyyyyA..
..AxxxxxxxAAxxxxxxxA..
..ALLLLLLLLLLLLLLLLA..
..AxxxxxxxxxxxxxxxxA..
..AAAAAAAAAAAAAAAAAA..
...EEEEEEEEEEEEEEEE...
"""


# --- Scenery beside the path, one set per week (fantasy-map dressing, 2026-11-13) ---------------

PINE = """\
.........AA.........
........AmoA........
.......AmmoeA.......
......AmmmmeeA......
.......AmmmeA.......
......AmmomeeA......
.....AmmmmmeeeA.....
....AmmmmmmmeeeA....
......AmmmmeeA......
.....AmmmmmmeeA.....
....AmmmmmmmmeeA....
...AmmmmmmmmmmeeA...
..AAAAAAAxxAAAAAAA..
.........xx.........
........AxxA........
.......EEEEEE.......
"""

PEAK = """\
...........AA.............
..........AGGA............
.........AGGGFA...........
........AGGFFDDA..........
.......ADFFDDDBBA.........
......ADDDDDBBBBBA...AA...
.....ADDDDBBBBBBBBA.AFDA..
....ADDDDBBBBBBBBBBADDDBA.
...ADDDBBBBBBBBBBBBBBBBBBA
..ADDBBBBBBBBBBBBBBBBBBBBA
.AAAAAAAAAAAAAAAAAAAAAAAAA
..EEEEEEEEEEEEEEEEEEEEEEE.
"""

WAVES = """\
....AAAA........AAAA....
..AA3322AA....AA3322AA..
.A2211..11A..A2211..11A.
A11......11AA11......11A
.AA........AA........AA.
........................
..AAAA........AAAA......
AA3322AA....AA3322AA....
211..11A..A2211..11A....
.......11AA11......11A..
"""

EMBERS = """\
.......H............
......HIH.....I.....
.......I...........H
....AAAAA...........
...ABBBBBA..AAAA....
..ABDBBBIBA.ABBBA...
..ABBBIIBBAABBDBBA..
.ABBBBBBBBBBBBBBBBA.
.AAAAAAAAAAAAAAAAAA.
..EEEEEEEEEEEEEEEE..
"""

# Scroll rod end (the quest board's unrolled scrolls): a turned wooden knob, mirrored for the right.
KNOB = """\
..AAAA..
.AzzyxA.
AzGzyxxA
AzzzyxxA
AzzzyxxA
AzzzyxxA
AzzzyxxA
AzzzyxxA
.AzzyxA.
..AAAA..
"""


def compass():
    """A small compass rose: gold north arm, iron arms, in a ring."""
    n, c = 23, 11
    g = [['.'] * n for _ in range(n)]
    for y in range(n):
        for x in range(n):
            dx, dy = x - c, y - c
            r2 = dx * dx + dy * dy
            if 49 <= r2 <= 64:
                g[y][x] = 'B'
            vert = abs(dy) <= 10 and abs(dx) <= (10 - abs(dy)) * 0.28
            horiz = abs(dx) <= 10 and abs(dy) <= (10 - abs(dx)) * 0.28
            if vert or horiz:
                north = vert and dy < 0
                g[y][x] = ('H' if dx <= 0 else 'I') if north else ('W' if (dx <= 0 if vert else dy <= 0) else 'B')
    for y in range(n):
        for x in range(n):
            if g[y][x] == '.' and any(0 <= x + a < n and 0 <= y + b < n and g[y + b][x + a] not in '.A'
                                      for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                g[y][x] = 'A'
    return [''.join(r) for r in g]


def write(name, rows, colours=None):
    head = ''.join(f'#! {k} {v}\n' for k, v in (colours or {}).items())
    (OUT / f'{name}.txt').write_text(head + '\n'.join(rows) + '\n')


def inked(rows):
    """A stone redrawn as a sketch: ink outline, hatched side, a soft parchment shadow."""
    out = []
    for y, row in enumerate(rows):
        line = ''
        for x, ch in enumerate(row):
            if ch == 'A':
                ch = 'w'
            elif ch == 'E':
                ch = 'q'
            elif ch == '3' and (x + y) % 3 == 0:
                ch = 'w'
            line += ch
        out.append(line)
    return out


def main():
    rows = stone()
    for name, (face, light, dark) in STONES.items():
        write(name, rows, {'1': face, '2': light, '3': dark})
    for name, (face, light, dark) in INKED.items():
        write(name, inked(rows), {'1': face, '2': light, '3': dark})
    write('map_campfire', CAMPFIRE.splitlines())
    write('map_chest', CHEST_SHUT.splitlines())
    write('map_chest_open', CHEST_OPEN.splitlines())
    write('map_compass', compass())
    write('scroll_knob', KNOB.splitlines())
    write('scroll_knob_r', [r[::-1] for r in KNOB.splitlines()])
    print('wrote map sprites')


if __name__ == '__main__':
    main()
