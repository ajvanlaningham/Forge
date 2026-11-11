"""The forge: an anvil with a glowing ingot, and a hammer striking it, in three frames.

    python3 art/gen_forge.py     # art/sprites/forge_0..2.txt, the splash SVG, art/out/forge.png

forge_0 hammer raised, forge_1 coming down, forge_2 the strike (sparks). The loading screen cycles
them; the splash screen is the strike, written as an SVG of square pixels for MauiSplashScreen.
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import gen_characters as gc

S = 40
ART = Path(__file__).resolve().parent
APP = ART.parent / 'Forge' / 'Forge' / 'Resources'

IRON = ('#9AA0A8', '#5E636B', '#3A3E45')
IRON_TOP = ('#E6E1CF', '#8A8472', '#786C5C')       # the house cream top edge and warm rim
INGOT = ('#FFE08A', '#F2A848', '#D27E24')
WOOD = ('#B07E52', '#80583A', '#523622')
HEAD = ('#C8CCD2', '#8E949C', '#55595F')
SPARK = ('#FFFFFF', '#FFE08A', '#F2A848')


class Frame(gc.Canvas):
    def __init__(self):
        self.px = [[None] * S for _ in range(S)]

    def get(self, x, y):
        return self.px[y][x] if 0 <= x < S and 0 <= y < S else None

    def set(self, x, y, c):
        if 0 <= x < S and 0 <= y < S:
            self.px[y][x] = c

    def finish_frame(self):
        filled = {(x, y) for y in range(S) for x in range(S) if self.px[y][x] is not None and self.px[y][x] not in SPARK}
        for y in range(S):
            for x in range(S):
                if self.px[y][x] is None and any((x + dx, y + dy) in filled for dx in (-1, 0, 1) for dy in (-1, 0, 1) if dx or dy):
                    self.px[y][x] = gc.OUTLINE
        for x in range(8, 34):                          # ground shadow
            if self.px[35][x] is None:
                self.px[35][x] = gc.SHADOW


def anvil(f):
    """A blacksmith's anvil: long tapering horn, deep face, hardy hole, narrow waist, arched feet."""
    base = gc.rect(10, 30, 31, 33) - gc.rect(17, 32, 24, 33)                 # feet with an arch between
    f.part(base | gc.rect(12, 29, 29, 29), IRON)
    f.part(gc.rect(15, 23, 26, 28) | gc.rect(14, 28, 27, 28), IRON)          # waist
    f.part(gc.rows('''
         ########################
    #############################
#################################
   ##############################
        #########################
             ##############''', 1, 17), IRON)                                # horn, face and the underside
    f.part(gc.rect(10, 17, 33, 17), IRON_TOP, outline=False)                 # the lit working face
    f.part(gc.rect(5, 18, 9, 18), IRON_TOP, outline=False)                   # light along the horn
    f.dots([(30, 18), (31, 18)], '#1A1A20')                                  # hardy hole
    f.dots([(32, 20)], '#E0C045')                                            # a gold rivet: house accent
    f.part(gc.rect(17, 14, 24, 16), INGOT)                                   # the glowing ingot
    f.dots([(18, 14), (19, 14)], '#FFFFFF')


def hammer(f, y):
    """The hammer head over the ingot at height y, handle running off to the right."""
    f.part(gc.rect(23, y + 1, 37, y + 2), WOOD)
    f.part(gc.rect(17, y, 23, y + 3), HEAD)
    f.dots([(18, y)], '#FFFFFF')


def sparks(f):
    for (x, y) in [(13, 10), (15, 7), (11, 13), (14, 14), (27, 14), (30, 12), (33, 15), (29, 7), (32, 9)]:
        f.set(x, y, SPARK[0])
        for (dx, dy) in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            if f.get(x + dx, y + dy) is None:
                f.set(x + dx, y + dy, SPARK[1])


def frame(n):
    f = Frame()
    anvil(f)
    hammer(f, (2, 7, 10)[n])
    f.finish_frame()
    if n == 2:
        sparks(f)
    return f


def svg(f, scale=12):
    """Square pixels as rects; crispEdges keeps them sharp when the splash is rasterised."""
    rects = []
    for y in range(S):
        for x in range(S):
            c = f.px[y][x]
            if c is not None:
                rects.append(f'<rect x="{x * scale}" y="{y * scale}" width="{scale}" height="{scale}" fill="{c}"/>')
    size = S * scale
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{size}" height="{size}" viewBox="0 0 {size} {size}" '
            f'shape-rendering="crispEdges">' + ''.join(rects) + '</svg>\n')


if __name__ == '__main__':
    frames = [frame(n) for n in range(3)]
    for n, f in enumerate(frames):
        colors = sorted({c for row in f.px for c in row if c is not None and c not in (gc.OUTLINE, gc.SHADOW)})
        key = {gc.OUTLINE: 'A', gc.SHADOW: 'E'}
        key.update({c: gc.KEYS[i] for i, c in enumerate(colors)})
        lines = [f'#! {key[c]} {c}' for c in colors] + [''.join('.' if c is None else key[c] for c in row) for row in f.px]
        (ART / 'sprites' / f'forge_{n}.txt').write_text('\n'.join(lines) + '\n')
    (APP / 'Splash' / 'splash.svg').write_text(svg(frames[2]))
    gc.W, gc.H = S, S
    gc.sheet(frames, 3, 8, ART / 'out' / 'forge.png')
    print('wrote forge_0..2, splash.svg; sheet art/out/forge.png')
