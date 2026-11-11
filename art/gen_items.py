"""Generate the forgeable items: Helm (Hearth), Hammer (Anvil), Boots (Bellows) in every metal tier.

    python3 art/gen_items.py        # writes art/sprites/item_<item>_<metal>.txt + art/out/items.png

Tiers (Decision 17): Copper -> Bronze -> Iron -> Steel -> Damascus -> Mithril -> Starmetal. Each
metal has its own ramp, and higher tiers earn flourishes, so progress is visible at a glance:
bronze rivets, an iron band, steel gold rivets and a plume, a damascus watered pattern, a mithril
gem and glow, starmetal gold trim and stars. 30x30 like the equipment icons, same lighting (lit
top-left, shaded bottom-right), black outlines between parts, slate cast shadow.
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import gen_characters as gc

N = 30
ART = Path(__file__).resolve().parent

METALS = {   # highlight, light, mid, dark
    'copper':    ('#FFD2AE', '#F0A070', '#C8663A', '#8A3F22'),
    'bronze':    ('#FFE8B0', '#E8C070', '#B8893A', '#7A5520'),
    'iron':      ('#E2E5E9', '#B0B4BA', '#7C8088', '#4A4E55'),
    'steel':     ('#FFFFFF', '#E0E8F0', '#A8B4C0', '#66727E'),
    'damascus':  ('#F4F4F8', '#C8C8D2', '#8A8A96', '#50505C'),
    'mithril':   ('#FFFFFF', '#D8F2FF', '#9CD0E8', '#5A8FB0'),
    'starmetal': ('#FFF4C8', '#C8B8FF', '#8A70D8', '#4A3890'),
}
TIER = {m: i for i, m in enumerate(METALS)}       # 0 copper ... 6 starmetal
LABEL = {m: m.capitalize() for m in METALS}

GOLD = ('#F2D468', '#E0C045', '#A88420')
WOOD = ('#B07E52', '#80583A', '#523622')
LEATHER = ('#A47450', '#7D5839', '#4A3222')
PLUME = ('#E07A6A', '#B83A2E', '#7A1E18')
GEM = '#4AA8E8'
SLOT = '#161B22'


class Icon(gc.Canvas):
    """A 30x30 canvas using the character canvas's part drawing, with no headroom offset."""
    def __init__(self):
        self.px = [[None] * N for _ in range(N)]

    def get(self, x, y):
        return self.px[y][x] if 0 <= x < N and 0 <= y < N else None

    def set(self, x, y, c):
        if 0 <= x < N and 0 <= y < N:
            self.px[y][x] = c

    def metal(self, mask, metal, outline=True):
        hi, light, mid, dark = METALS[metal]
        self.part(mask, (light, mid, dark), outline)
        # a polished glint on the upper-left of each part
        top = min(mask, key=lambda p: (p[0] + p[1], p[1]))
        self.set(top[0] + 1, top[1] + 1, hi) if (top[0] + 1, top[1] + 1) in mask else None

    def finish_icon(self):
        filled = {(x, y) for y in range(N) for x in range(N) if self.px[y][x] is not None}
        for y in range(N):
            for x in range(N):
                if (x, y) not in filled and any((x + dx, y + dy) in filled for dx in (-1, 0, 1) for dy in (-1, 0, 1) if dx or dy):
                    self.px[y][x] = gc.OUTLINE
        outline = {(x, y) for y in range(N) for x in range(N) if self.px[y][x] == gc.OUTLINE}
        for (x, y) in sorted(outline):       # cast shadow one step down-right, like the gear icons
            sx, sy = x + 1, y + 1
            if 0 <= sx < N and 0 <= sy < N and self.px[sy][sx] is None and any((x, yy) in filled for yy in range(max(0, y - 3), y)):
                self.px[sy][sx] = gc.SHADOW


def flourish_dots(ic, pts, metal):
    t = TIER[metal]
    if t == 1:                                   # bronze: plain rivets
        ic.dots(pts, METALS[metal][3])
    elif t >= 3:                                 # steel and up: gold rivets
        ic.dots(pts, GOLD[1])

def damascus(ic, mask, metal):
    if metal != 'damascus':
        return
    for (x, y) in mask:                          # watered steel: wavy dark bands
        if (x + 2 * ((y // 2) % 2) + y) % 5 == 0 and ic.get(x, y) not in (gc.OUTLINE, None):
            ic.set(x, y, METALS[metal][3])

def sparkle(ic, metal, spots):
    if metal == 'mithril':
        for (x, y) in spots:
            for p in ((x, y), (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                if ic.get(*p) is None:
                    ic.set(*p, '#BFEFFF')
            ic.set(x, y, '#FFFFFF')
    elif metal == 'starmetal':
        for (x, y) in spots:
            for p in ((x, y), (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                if ic.get(*p) is None:
                    ic.set(*p, GOLD[0])
            ic.set(x, y, '#FFFFFF')


def helm(metal):
    ic = Icon()
    t = TIER[metal]
    if t >= 3:                                   # plume (steel), metal crest beyond
        crest = gc.rows('''
  ####
 ######
########
 ######''', 11, 1)
        if t == 3:
            ic.part(crest, PLUME)
        else:
            ic.metal(crest, metal)
            if t == 6:
                ic.part(gc.rect(13, 2, 16, 2), GOLD, outline=False)
    dome = gc.ellipse(15, 13, 9, 8) - {(x, y) for x in range(N) for y in range(19, N)}
    cheeks = gc.rect(6, 13, 10, 22) | gc.rect(20, 13, 24, 22)
    body = dome | cheeks
    ic.metal(body, metal)
    damascus(ic, body, metal)
    # brow band
    band = gc.rect(6, 11, 24, 12)
    if t >= 2:
        ic.metal(band, metal)
        if t == 6:
            ic.part(band, GOLD, outline=True)
    # face: eye slot and the gap between the cheek guards, with a nose guard
    ic.dots([(x, y) for x in range(9, 22) for y in (14, 15)], SLOT)
    ic.dots([(x, y) for x in range(11, 20) for y in range(16, 22)], SLOT)
    nose = gc.rect(14, 13, 16, 20)
    ic.metal(nose, metal)
    flourish_dots(ic, [(8, 17), (22, 17), (8, 20), (22, 20)], metal)
    if t == 5:
        ic.dots([(15, 9), (15, 10)], GEM); ic.dots([(15, 9)], '#BFEFFF')
    if t == 6:
        ic.dots([(14, 9), (15, 9), (16, 9), (15, 8), (15, 10)], GOLD[0])
    ic.finish_icon()
    sparkle(ic, metal, [(4, 6), (26, 9), (25, 25)] if t >= 5 else [])
    return ic


def hammer(metal):
    ic = Icon()
    t = TIER[metal]
    handle = gc.rect(14, 10, 15, 25)
    ic.part(handle, WOOD)
    ic.part(gc.rect(13, 19, 16, 25), LEATHER)                    # grip wrap
    for y in (20, 22, 24):
        ic.dots([(13, y), (14, y), (15, y), (16, y)], LEATHER[2])
    ic.metal(gc.rect(13, 26, 16, 27), metal)                     # pommel
    # the head: a heavy block, wider at the striking faces
    face_l = gc.rect(4, 2, 10, 11)
    face_r = gc.rect(20, 2, 26, 11)
    collar = gc.rect(10, 4, 20, 9)
    ic.metal(collar, metal)
    ic.metal(face_l, metal)
    ic.metal(face_r, metal)
    damascus(ic, face_l | face_r | collar, metal)
    if t >= 2:                                                   # iron and up: reinforcing bands
        ic.part(gc.rect(11, 4, 11, 9), METALS[metal][1:], outline=True)
        ic.part(gc.rect(19, 4, 19, 9), METALS[metal][1:], outline=True)
    if t == 6:
        ic.part(gc.rect(12, 4, 18, 4), GOLD, outline=False)
        ic.part(gc.rect(12, 9, 18, 9), GOLD, outline=False)
    flourish_dots(ic, [(7, 4), (7, 9), (23, 4), (23, 9)], metal)
    if t == 5:
        ic.dots([(15, 6), (15, 7)], GEM); ic.dots([(15, 6)], '#BFEFFF')
    if t == 6:
        ic.dots([(15, 6), (14, 7), (15, 7), (16, 7), (15, 8)], GOLD[0])
    ic.finish_icon()
    sparkle(ic, metal, [(2, 15), (27, 16), (20, 26)] if t >= 5 else [])
    return ic


def boots(metal):
    """One tall armoured boot in profile, toe to the right: cuff, shin greave, toe cap."""
    ic = Icon()
    t = TIER[metal]
    hi, light, mid, dark = METALS[metal]
    boot = gc.rows('''
##########
##########
##########
##########
##########
##########
##########
##########
##########
##########
##########
###########
#############
###############
#################
##################
###################
###################
###################
 ##################''', 5, 4)
    ic.part(boot, LEATHER)
    ic.part(gc.rect(5, 24, 23, 25) - {(23, 25)}, ('#3A271B', '#2B1D14', '#1A120C'))   # sole and heel
    ic.metal(gc.rect(4, 2, 15, 5), metal)                                             # cuff
    greave = gc.rect(7, 8, 13, 19) - {(7, 19), (13, 19)}
    ic.metal(greave, metal)                                                           # shin greave
    toe = gc.rect(17, 18, 23, 23) - {(23, 18), (22, 18), (23, 19)}
    ic.metal(toe, metal)                                                              # toe cap
    damascus(ic, greave | toe, metal)
    ic.dots([(15, 20), (16, 21), (15, 22)], LEATHER[2])                               # laces
    flourish_dots(ic, [(10, 10), (10, 17), (6, 3), (14, 3)], metal)
    if t >= 2:
        ic.metal(gc.rect(5, 21, 16, 21), metal, outline=False)                        # ankle strap
    if t == 5:
        ic.dots([(10, 13), (10, 14)], GEM); ic.dots([(10, 13)], '#BFEFFF')
    if t == 6:
        ic.dots([(10, 12), (9, 13), (10, 13), (11, 13), (10, 14)], GOLD[0])
        ic.part(gc.rows('''
   #
  ##
 ###
####
 ###''', 0, 9), GOLD)                                                           # a little wing: light gear
    ic.finish_icon()
    sparkle(ic, metal, [(25, 5), (2, 27), (27, 13)] if t >= 5 else [])
    return ic


ITEMS = {'helm': ('Helm', helm), 'hammer': ('Hammer', hammer), 'boots': ('Boots', boots)}


def write(name, ic):
    colors = sorted({c for row in ic.px for c in row if c is not None and c not in (gc.OUTLINE, gc.SHADOW)})
    key = {gc.OUTLINE: 'A', gc.SHADOW: 'E'}
    key.update({c: gc.KEYS[i] for i, c in enumerate(colors)})
    lines = [f'#! {key[c]} {c}' for c in colors] + [''.join('.' if c is None else key[c] for c in row) for row in ic.px]
    (ART / 'sprites' / f'{name}.txt').write_text('\n'.join(lines) + '\n')


if __name__ == '__main__':
    cells = []
    for item, (_, fn) in ITEMS.items():
        for metal in METALS:
            ic = fn(metal)
            write(f'item_{item}_{metal}', ic)
            cells.append(ic)
    # contact sheet (reuse the character sheet writer at 30x30)
    gc.W, gc.H = N, N
    gc.sheet(cells, len(METALS), 5, ART / 'out' / 'items.png')
    print(f'wrote {len(cells)} items; sheet: art/out/items.png')
