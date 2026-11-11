"""Forgeable gear that fits each hero (Decision 22): 36 heroes x 3 slots = 108 designs, every one
rendered in all 7 metals.

    python3 art/gen_gear.py              # art/sprites/gear_<race>_<class>_<slot>_<metal>.txt + sheets
    python3 art/gen_gear.py anvil        # one slot only (anvil | hearth | bellows)

Slots: anvil = the weapon in hand, hearth = the defensive piece, bellows = light gear. Each design is
drawn once with the metal as a parameter; cloth, wood and leather keep their own colours and take
the metal as trim, fittings or gems. 30x30 like the equipment and item icons: lit top-left, black
outlines between parts, slate cast shadow (gen_items.Icon).
"""
import json, math, sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import gen_characters as gc
import gen_items as gi
from gen_characters import rect, rows, ellipse

ART = Path(__file__).resolve().parent
METALS = gi.METALS
TIER = gi.TIER
GEM_BLUE = ('#D8F6FF', '#7ED8F8', '#3A8ABA')
GEM_GREEN = ('#E0FFD0', '#8EE070', '#4A9A3A')
GEM_PURPLE = ('#F0D8FF', '#B070E0', '#6A3A9A')
GEM_RED = ('#FFC0B0', '#E04838', '#8A2018')
WOOD = gi.WOOD
LEATHER = gi.LEATHER
BONE = ('#F4ECD6', '#D6CAA8', '#9A8E70')
CLOTH = {
    'navy': ('#5A78C8', '#3A54A0', '#24346A'),
    'white': ('#FFFFFF', '#E4E4EA', '#A8A8B4'),
    'rust': ('#B8604A', '#8E3E2E', '#5E2418'),
    'hide': ('#C8A07A', '#9A7552', '#6A4A30'),
    'purple': ('#9A6AC8', '#74489E', '#4A2C6A'),
    'black': ('#4A4E58', '#2C3038', '#181A20'),
    'midnight': ('#4A4A78', '#30305A', '#1C1C3A'),
    'crimson': ('#D04848', '#A02A2A', '#681818'),
    'green': ('#7EA858', '#56803C', '#36542A'),
    'brown': ('#9A7452', '#74543A', '#4A3424'),
    'fur': ('#D8C4A0', '#B09A74', '#7A6648'),
    'saffron': ('#F2A848', '#D27E24', '#94520E'),
    'teal': ('#5AB4A8', '#358A80', '#205A54'),
    'moon': ('#E8F0FF', '#B8C8F0', '#7A8CC0'),
}
GLOW = {'blue': '#9CE8FF', 'green': '#B8FF8A', 'gold': '#FFF2A8', 'purple': '#E0B0FF'}


class G(gi.Icon):
    def __init__(self, metal):
        super().__init__()
        self.m = metal

    def met(self, mask, outline=True):
        self.metal(mask, self.m, outline)

    def lit(self, x, y):
        self.set(x, y, METALS[self.m][0])

    def done(self, sparkles=()):
        t = TIER[self.m]
        if self.m == 'damascus':
            for y in range(30):
                for x in range(30):
                    c = self.get(x, y)
                    if c in METALS['damascus'][1:3] and (x + 2 * ((y // 2) % 2) + y) % 5 == 0:
                        self.set(x, y, METALS['damascus'][3])
        self.finish_icon()
        if t >= 5:
            gi.sparkle(self, self.m, list(sparkles) or [(3, 4), (26, 4), (26, 26)])
        return self


def diag(x0, y0, length, width=2, step=1):
    """A 1:1 diagonal running up and to the right from (x0, y0), `width` pixels thick."""
    return {(x0 + i + w, y0 - i) for i in range(length) for w in range(width)}


def glow(ic, pts, color):
    for (x, y) in pts:
        for d in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)):
            if ic.get(x + d[0], y + d[1]) is None:
                ic.set(x + d[0], y + d[1], color)
        ic.set(x, y, '#FFFFFF')

# ====================================================================== ANVIL: weapons

def staff_shaft(ic, length=20, wood=WOOD, metal_shaft=False):
    shaft = diag(4, 27, length)
    if metal_shaft:
        ic.met(shaft)
    else:
        ic.part(shaft, wood)
    ic.met(rect(3, 26, 6, 28))                                               # ferrule
    ic.met(diag(4 + length - 4, 27 - length + 4, 2, 3))                       # collar under the head

def w_archmage_staff(m):
    ic = G(m)
    staff_shaft(ic, 19)
    ic.met(rows('''
#   #
## ##
 ###''', 19, 6))                                                             # twisted metal prongs
    ic.part(rows('''
 #
###
###
 #''', 20, 2), GEM_BLUE)
    glow(ic, [(26, 3)], GLOW['blue'])
    return ic.done()

def w_leaf_staff(m):
    ic = G(m)
    staff_shaft(ic, 19, metal_shaft=True)
    ic.part(rows('''
  ##
 ####
#####
####
 ##''', 20, 2), GEM_GREEN)
    ic.dots([(22, 4), (21, 5)], GEM_GREEN[2])                                 # the leaf's vein
    return ic.done()

def w_rune_staff(m):
    ic = G(m)
    staff_shaft(ic, 17)
    ic.met(rect(18, 2, 26, 9))                                               # rune-stone in a metal cap
    ic.part(rect(19, 4, 25, 8), ('#9AA0A8', '#6E747C', '#4A4E55'), outline=False)
    ic.dots([(21, 5), (22, 6), (23, 5), (22, 7), (22, 8)], GLOW['blue'])
    return ic.done()

def w_skull_staff(m):
    ic = G(m)
    staff_shaft(ic, 18)
    ic.part(rows('''
 ####
######
######
 #  #
 ####''', 19, 3), BONE)
    ic.dots([(20, 5), (23, 5)], '#1A1A20')
    ic.met(rows('''
#    #
##  ##''', 18, 1))                                                           # metal horns on the skull
    glow(ic, [(21, 1)], GLOW['green'])
    return ic.done()

def w_star_wand(m):
    ic = G(m)
    ic.part(diag(8, 24, 10), WOOD)
    ic.met(diag(7, 25, 3, 3))                                                # metal grip band
    ic.met(rows('''
  #
  #
#####
 ###
 # #''', 17, 6))
    glow(ic, [(24, 4), (14, 8)], GLOW['gold'])
    return ic.done()

def w_wrench(m):
    ic = G(m)
    ic.met(diag(6, 25, 15, 3))
    ic.met(rows('''
## ##
## ##
#####
 ###''', 19, 3))
    ic.met(ellipse(6.5, 24.5, 2.5, 2.5))
    ic.set(6, 24, None); ic.set(7, 24, None)
    return ic.done()

def blade(ic, length, width=2, tip=True):
    ic.met(diag(9, 20, length, width))
    if tip:
        ic.lit(9 + length - 1, 20 - length + 1)

def hilt(ic, guard_metal=True, grip=LEATHER):
    guard = {(6 + i, 19 + i) for i in range(5)} | {(7 + i, 19 + i) for i in range(4)}
    guard = {(x - 1, y - 2) for (x, y) in guard}
    ic.met(guard) if guard_metal else ic.part(guard, gi.GOLD)
    ic.part(diag(5, 25, 4), grip)
    ic.met(rect(3, 26, 5, 28))

def w_longsword(m):
    ic = G(m)
    blade(ic, 17)
    hilt(ic)
    return ic.done()

def w_curved_blade(m):
    ic = G(m)
    ic.met(diag(9, 20, 10, 2) | diag(18, 10, 6, 2, 1) | {(19 + i, 11 - i // 2 - i) for i in range(5)})
    ic.met({(18 + i, 10 - i) for i in range(7)} | {(19 + i, 9 - i) for i in range(6)})
    hilt(ic, grip=('#7EA858', '#56803C', '#36542A'))
    return ic.done()

def w_greatsword(m):
    ic = G(m)
    blade(ic, 18, width=3)
    ic.dots([(10 + i, 19 - i) for i in range(16)], METALS[m][3])             # fuller
    hilt(ic)
    ic.dots([(4, 27)], GEM_PURPLE[1])
    return ic.done()

def w_short_sword(m):
    ic = G(m)
    blade(ic, 11)
    hilt(ic)
    return ic.done()

def w_glowing_blade(m):
    ic = G(m)
    blade(ic, 12)
    hilt(ic, grip=('#7EA858', '#56803C', '#36542A'))
    glow(ic, [(23, 6), (16, 9), (21, 13)], GLOW['blue'])                      # it glows when orcs are near
    return ic.done()

def w_axe(m):
    ic = G(m)
    ic.part(diag(5, 27, 18), WOOD)
    ic.met(rows('''
   ###
  #####
 #######
#########
#########
 #######
  #####''', 15, 2))
    ic.part(rect(19, 4, 20, 7), WOOD, outline=False)
    ic.met(rect(3, 26, 6, 28))
    return ic.done()

def w_cleaver(m):
    ic = G(m)
    ic.part(diag(5, 27, 10), WOOD)
    ic.met(rect(12, 3, 26, 17) - {(12, 3), (12, 4), (13, 3), (26, 17)})
    ic.dots([(23, 5), (24, 5), (23, 6), (24, 6)], None)                      # the hanging hole
    ic.dots([(14, 15), (15, 14), (17, 16)], GEM_RED[2])                      # nicks
    return ic.done()

def dagger(ic, x, y, length=9, flip=False):
    if flip:
        ic.met({(x - i, y - i) for i in range(length)} | {(x - i - 1, y - i) for i in range(length)})
        ic.part({(x + 1 + i, y + 1 + i) for i in range(3)}, LEATHER)
    else:
        ic.met(diag(x, y, length))
        ic.part({(x - 1 - i, y + 1 + i) for i in range(3)}, LEATHER)

def w_twin_daggers(m):
    ic = G(m)
    dagger(ic, 8, 22, 12)
    dagger(ic, 22, 22, 12, flip=True)
    ic.met(rect(5, 21, 9, 22)); ic.met(rect(20, 21, 24, 22))
    return ic.done()

def w_long_knife(m):
    ic = G(m)
    blade(ic, 15, width=1)
    ic.met(diag(10, 20, 14, 1))
    hilt(ic, grip=CLOTH['midnight'])
    return ic.done()

def crossbow(ic, big=False):
    s = 2 if big else 0
    ic.part(rect(4, 13, 24 + s, 15), WOOD)                                    # stock
    ic.met(rect(18 + s, 5, 20 + s, 23))                                       # the bow
    ic.dots([(17 + s, y) for y in range(6, 23)], '#D9C9A3')
    ic.met(rect(3, 12, 6, 16))                                                # butt plate
    ic.part(rect(10, 16, 12, 19), WOOD)                                       # grip
    ic.met(rect(21 + s, 13, 26 + s, 14))                                      # bolt head

def w_hand_crossbow(m):
    ic = G(m)
    crossbow(ic)
    return ic.done()

def w_heavy_crossbow(m):
    ic = G(m)
    crossbow(ic, big=True)
    ic.met(rect(8, 12, 9, 16))                                                 # winding crank
    return ic.done()

def w_scimitar(m):
    ic = G(m)
    ic.met({(9 + i, 20 - i) for i in range(10)} | {(10 + i, 20 - i) for i in range(10)} | {(11 + i, 21 - i) for i in range(9)}
           | {(19, 10), (20, 9), (21, 9), (22, 8), (23, 8), (24, 8), (20, 10)})
    hilt(ic, grip=CLOTH['crimson'])
    return ic.done()

def w_wrist_blade(m):
    ic = G(m)
    ic.part(rect(4, 16, 15, 23), CLOTH['black'])                               # bracer
    ic.met(rect(5, 17, 14, 18))
    ic.met(rect(15, 18, 27, 19))                                               # the blade, sprung
    ic.lit(27, 18)
    ic.dots([(8, 21), (11, 21)], GEM_PURPLE[1])
    return ic.done()

def longbow(ic, ramp=WOOD, tips=True, elegant=False):
    curve = [(8, 3), (9, 4), (10, 5), (11, 7), (11, 9), (12, 11), (12, 13), (12, 15), (12, 17), (11, 19), (11, 21), (10, 23), (9, 25), (8, 26)]
    mask = set(curve) | {(x + 1, y) for (x, y) in curve}
    ic.part(mask, ramp)
    for y in range(3, 27):
        ic.set(7, y, '#D9C9A3')
    if tips:
        ic.met(rect(7, 1, 9, 3)); ic.met(rect(7, 26, 9, 28))
    ic.met(rect(11, 13, 14, 16))                                               # grip plate
    if elegant:
        ic.met(rows('''
 #
###
 ##''', 13, 7)); ic.met(rows('''
 ##
###
 #''', 13, 19))                                                               # leaf flourishes

def w_longbow(m):
    ic = G(m); longbow(ic)
    ic.met(rect(15, 14, 26, 14)); ic.met(rows('''
#
##
#''', 25, 13))                                                               # nocked arrow
    return ic.done()

def w_elven_bow(m):
    ic = G(m); longbow(ic, ramp=('#F4ECD6', '#D6C29A', '#9A8660'), elegant=True)
    return ic.done()

def w_brass_bow(m):
    ic = G(m); longbow(ic, ramp=METALS[m][1:], tips=False)
    ic.met(ellipse(9, 3, 2, 2)); ic.met(ellipse(9, 26, 2, 2))                  # pulleys
    ic.dots([(9, 3), (9, 26)], '#1A1A20')
    return ic.done()

def w_spear(m):
    ic = G(m)
    ic.part(diag(3, 28, 20), WOOD)
    ic.met(rows('''
    #
   ###
  ####
 ####
####
 ##''', 20, 1))
    ic.dots([(21, 8), (20, 9), (22, 8)], CLOTH['crimson'][1])
    return ic.done()

def w_sling(m):
    ic = G(m)
    ic.dots([(4 + i, 26 - i) for i in range(8)] + [(12 + i, 18 - 2 * i) for i in range(5)], LEATHER[1])
    ic.part(rows('''
 ###
#####
#####
 ###''', 15, 4), LEATHER)                                                   # the pouch
    ic.met(ellipse(17.5, 5.5, 1.5, 1.5))                                      # a metal shot in it
    ic.met(ellipse(23, 13, 2, 2)); ic.met(ellipse(24, 20, 2, 2))              # spare shot
    return ic.done()

def w_flanged_mace(m):
    ic = G(m)
    ic.part(diag(5, 27, 13), WOOD)
    ic.met(ellipse(20, 8, 4, 4))
    ic.met(rows('''
  #
  #
#####
  #
  #''', 18, 6))
    for (x, y) in ((20, 2), (26, 8), (14, 8), (20, 14)):
        ic.met({(x, y)})
    ic.met(rect(3, 26, 6, 28))
    return ic.done()

def w_crescent_staff(m):
    ic = G(m)
    staff_shaft(ic, 17, metal_shaft=True)
    ic.met(rows('''
  ####
 #
#
#
 #
  ####''', 18, 1))
    glow(ic, [(24, 4)], '#C8D8FF')
    return ic.done()

def w_smith_hammer(m):
    ic = G(m)
    ic.part(diag(5, 27, 15), WOOD)
    ic.met(rows('''
 ######
########
########
########
 ######''', 15, 2) | rows('''
  ##
 ####
 ####''', 12, 9))
    ic.dots([(27, 1), (25, 0), (28, 4)], '#FFB060')
    ic.met(rect(3, 26, 6, 28))
    return ic.done()

def w_bone_mace(m):
    ic = G(m)
    ic.part(diag(5, 27, 13), BONE)
    ic.part(ellipse(20, 8, 4, 4), BONE)
    ic.dots([(18, 7), (22, 7)], '#1A1A20')
    ic.dots([(19, 10), (20, 10), (21, 10)], BONE[2])
    for (x, y) in ((20, 3), (25, 8), (15, 8), (24, 4), (16, 4)):
        ic.met({(x, y), (x, y - 1) if y < 8 else (x + (1 if x > 20 else -1), y)})
    return ic.done()

def w_walking_staff(m):
    ic = G(m)
    ic.part({(9, y) for y in range(5, 29)} | {(10, y) for y in range(5, 29)}, WOOD)
    ic.part(rows('''
 ####
#    #
     #''', 9, 1), WOOD)                                                     # the crook
    for y in (10, 18, 26):
        ic.met(rect(8, y, 11, y + 1))
    return ic.done()

def w_lantern_pole(m):
    ic = G(m)
    ic.met({(6, y) for y in range(10, 29)})
    ic.met(rect(5, 6, 20, 7))                                                  # the arm
    ic.met(rect(16, 9, 24, 21))
    ic.part(rect(18, 11, 22, 19), ('#FFF4C8', '#FFE08A', '#F2A848'), outline=False)
    ic.met(rect(19, 8, 21, 8))
    glow(ic, [(14, 13), (26, 15)], GLOW['gold'])
    return ic.done()

def w_bo_staff(m):
    ic = G(m)
    ic.part(diag(2, 28, 26), WOOD)
    ic.met(diag(2, 28, 4, 2)); ic.met(diag(24, 6, 4, 2))
    ic.part(diag(13, 17, 4), CLOTH['saffron'])                                 # grip wrap
    return ic.done()

def w_war_fan(m):
    ic = G(m)
    for i in range(7):                                                         # ribs fanning out
        ic.met({(7 + k, 24 - k) for k in range(2)} | {(7 + round(k * (0.3 + i * 0.25)), 24 - k) for k in range(15)}, outline=False)
    ic.part({(x, y) for x in range(8, 27) for y in range(4, 22) if (x - 7) ** 2 + (24 - y) ** 2 < 260 and (x - 7) ** 2 + (24 - y) ** 2 > 60}, CLOTH['teal'])
    for i in range(7):
        ic.met({(7 + round(k * (0.3 + i * 0.25)), 24 - k) for k in range(8, 15)}, outline=False)
    ic.met(ellipse(7, 24, 2, 2))
    return ic.done()

def w_knuckles(m):
    ic = G(m)
    ic.met(rect(5, 9, 24, 14))
    for x in (7, 12, 17, 22):
        ic.set(x, 11, None); ic.set(x + 1, 11, None); ic.set(x, 12, None); ic.set(x + 1, 12, None)
    ic.met(rows('''
####################
 ##################
  ################''', 5, 15))
    return ic.done()

def w_cestus(m):
    ic = G(m)
    ic.part(rect(6, 8, 22, 22), LEATHER)
    for y in (11, 15, 19):
        ic.dots([(x, y) for x in range(7, 22)], LEATHER[2])
    for (x, y) in ((9, 9), (14, 9), (19, 9), (11, 13), (16, 13), (9, 17), (14, 17), (19, 17)):
        ic.met(rect(x, y, x + 1, y + 1))
    ic.met(rows('''
 #   #   #
###########''', 6, 5))
    return ic.done()

def w_gourd(m):
    ic = G(m)
    ic.part(ellipse(14, 20, 7, 6) | ellipse(14, 10, 4, 4), CLOTH['hide'])
    ic.met(rect(10, 13, 18, 14))                                               # metal band at the waist
    ic.met(rect(12, 3, 16, 5))                                                 # stopper cap
    ic.dots([(8 + i, 6 - (i % 2)) for i in range(3)], LEATHER[1])
    return ic.done()

def w_tonfa(m):
    ic = G(m)
    ic.met(rect(10, 3, 13, 27))
    ic.met(rect(14, 7, 21, 9))
    ic.part(rect(18, 10, 21, 15), LEATHER)
    return ic.done()

# ---- reworked: thick blades, a real axe head, side-view crossbows, a ribbed fan
import math

def band(x0, y0, length, half=1):
    """A thick 1:1 diagonal (up-right) from (x0, y0): all cells within `half` of the line."""
    c = x0 + y0
    return {(x, y) for x in range(x0 - 1, x0 + length + 2) for y in range(y0 - length - 2, y0 + 2)
            if abs(x + y - c) <= half and x0 - 1 <= x <= x0 + length and y0 - length <= y <= y0 + 1}

def blade(ic, length, width=1, tip=True):
    b = band(10, 19, length, half=width)
    ic.met(b)
    ic.dots([(10 + i, 18 - i) for i in range(length - 1)], METALS[ic.m][0])   # the lit edge
    if tip:
        ic.lit(10 + length - 1, 19 - length + 1)

def hilt(ic, guard_metal=True, grip=LEATHER):
    guard = {(x, y) for x in range(6, 14) for y in range(15, 24) if x - y in (-10, -11) and 6 <= x <= 13}
    ic.met(guard) if guard_metal else ic.part(guard, gi.GOLD)
    ic.part(band(5, 24, 4, half=0) | band(6, 24, 3, half=0), grip)
    ic.met(ellipse(4, 26, 1.5, 1.5))

def w_longsword(m):
    ic = G(m); blade(ic, 17); hilt(ic); return ic.done()

def w_short_sword(m):
    ic = G(m); blade(ic, 11); hilt(ic); return ic.done()

def w_greatsword(m):
    ic = G(m); blade(ic, 18, width=2)
    ic.dots([(11 + i, 19 - i) for i in range(15)], METALS[m][3])             # fuller
    hilt(ic); ic.dots([(4, 26)], GEM_PURPLE[1]); return ic.done()

def w_glowing_blade(m):
    ic = G(m); blade(ic, 12); hilt(ic, grip=('#7EA858', '#56803C', '#36542A'))
    glow(ic, [(24, 4), (17, 6), (25, 12)], GLOW['blue']); return ic.done()

def w_long_knife(m):
    ic = G(m); blade(ic, 14, width=0)
    ic.met(band(11, 19, 13, half=0))
    hilt(ic, grip=CLOTH['midnight']); return ic.done()

def w_curved_blade(m):
    ic = G(m)
    b = band(10, 19, 10, half=1)
    tip = {(20 + i, 9 - i) for i in range(4)} | {(20 + i, 10 - i) for i in range(5)} | {(21 + i, 10 - i) for i in range(4)}
    curve = {(23 + i, 6 - (i + 1) // 2) for i in range(4)} | {(23 + i, 7 - (i + 1) // 2) for i in range(4)}
    ic.met(b | tip | curve)
    hilt(ic, grip=('#7EA858', '#56803C', '#36542A')); return ic.done()

def w_axe(m):
    ic = G(m)
    ic.part(band(5, 26, 19, half=0) | band(6, 26, 18, half=0), WOOD)
    ic.met(rows('''
    ###
   #####
  ######
 #######
########
########
 #######
  ######
   ####''', 15, 1))
    ic.part(rect(17, 3, 18, 8), METALS[m][1:], outline=True)                   # the socket
    ic.dots([(16, 5)], METALS[m][3])
    ic.met(ellipse(4, 27, 1.5, 1.5))
    return ic.done()

def crossbow(ic, big=False):
    s = 3 if big else 0
    ic.part(rect(3, 14, 21 + s, 16), WOOD)                                    # stock
    ic.part(rect(8, 17, 10, 21 + s // 2), WOOD)                               # grip
    prod = {(19 + s, y) for y in range(6 - s, 25 + s)} | {(20 + s, y) for y in range(7 - s, 24 + s)}
    ic.met(prod)                                                               # the bow arms
    for (y0, y1) in ((6 - s, 14), (24 + s, 16)):
        steps = abs(y1 - y0)
        for k in range(steps + 1):
            ic.set(18 + s - round(k * 4 / steps), y0 + (k if y1 > y0 else -k), '#D9C9A3')   # string
    ic.met(rect(10, 13, 25 + s, 13))                                           # the bolt
    ic.met(rows('''
#
##
#''', 26 + s, 12))
    ic.met(rect(2, 13, 4, 17))                                                 # butt plate

def w_hand_crossbow(m):
    ic = G(m); crossbow(ic); return ic.done()

def w_heavy_crossbow(m):
    ic = G(m); crossbow(ic, big=True)
    ic.met(ellipse(6, 20, 2, 2)); ic.met(rect(6, 17, 6, 19))                   # winding crank
    return ic.done()

def w_war_fan(m):
    ic = G(m)
    px, py = 6, 25
    cloth, ribs, edge = set(), set(), set()
    for y in range(30):
        for x in range(30):
            dx, dy = x - px, py - y
            r = math.hypot(dx, dy)
            if dx < 0 or dy < 0:
                continue
            a = math.degrees(math.atan2(dy, dx))
            if 5 <= r <= 18 and 0 <= a <= 90:
                cloth.add((x, y))
                if r > 16.8:
                    edge.add((x, y))
                elif any(abs(a - k) < 3.2 for k in range(0, 91, 18)):
                    ribs.add((x, y))
    ic.part(cloth, CLOTH['teal'])
    ic.met(ribs, outline=False)
    ic.met(edge, outline=False)
    for k in range(0, 91, 18):
        for r in range(1, 6):
            ic.met({(px + round(r * math.cos(math.radians(k))), py - round(r * math.sin(math.radians(k))))}, outline=False)
    ic.met(ellipse(px, py, 1.5, 1.5))
    return ic.done()

def w_smith_hammer(m):
    ic = G(m)
    ic.part(band(5, 26, 14, half=0) | band(6, 26, 13, half=0), WOOD)
    ic.met(rect(14, 2, 24, 8))                                                 # the head
    ic.met(rect(23, 1, 26, 9))                                                 # striking face
    ic.part(rect(17, 3, 19, 7), METALS[m][1:], outline=True)                   # socket band
    ic.dots([(27, 3), (28, 6), (27, 9)], '#FFB060')                            # sparks
    ic.met(ellipse(4, 27, 1.5, 1.5))
    return ic.done()

def w_archmage_staff(m):
    ic = G(m)
    staff_shaft(ic, 18)
    ic.met(rows('''
#     #
##   ##
 ## ##
  ###''', 17, 5))                                                            # twisted metal prongs
    ic.part(rows('''
  #
 ###
#####
 ###
  #''', 18, 1), GEM_BLUE)
    glow(ic, [(26, 1)], GLOW['blue'])
    return ic.done()


# ====================================================================== the table: (race, class) -> slot -> (name, drawer)

GEAR = {
    ('human', 'wizard'):     {'anvil': ("Archmage's Staff", w_archmage_staff)},
    ('elf', 'wizard'):       {'anvil': ('Leafcrystal Staff', w_leaf_staff)},
    ('dwarf', 'wizard'):     {'anvil': ('Runestone Staff', w_rune_staff)},
    ('orc', 'wizard'):       {'anvil': ('Skull Staff', w_skull_staff)},
    ('halfling', 'wizard'):  {'anvil': ('Star Wand', w_star_wand)},
    ('construct', 'wizard'): {'anvil': ('Great Wrench', w_wrench)},
    ('human', 'warrior'):     {'anvil': ('Longsword', w_longsword)},
    ('elf', 'warrior'):       {'anvil': ('Curved Blade', w_curved_blade)},
    ('dwarf', 'warrior'):     {'anvil': ('Bearded Axe', w_axe)},
    ('orc', 'warrior'):       {'anvil': ('Cleaver', w_cleaver)},
    ('halfling', 'warrior'):  {'anvil': ('Short Sword', w_short_sword)},
    ('construct', 'warrior'): {'anvil': ('Greatsword', w_greatsword)},
    ('human', 'rogue'):     {'anvil': ('Twin Daggers', w_twin_daggers)},
    ('elf', 'rogue'):       {'anvil': ('Long Knife', w_long_knife)},
    ('dwarf', 'rogue'):     {'anvil': ('Hand Crossbow', w_hand_crossbow)},
    ('orc', 'rogue'):       {'anvil': ('Scimitar', w_scimitar)},
    ('halfling', 'rogue'):  {'anvil': ('Glowing Blade', w_glowing_blade)},
    ('construct', 'rogue'): {'anvil': ('Wrist Blade', w_wrist_blade)},
    ('human', 'ranger'):     {'anvil': ('Longbow', w_longbow)},
    ('elf', 'ranger'):       {'anvil': ('Elven Bow', w_elven_bow)},
    ('dwarf', 'ranger'):     {'anvil': ('Heavy Crossbow', w_heavy_crossbow)},
    ('orc', 'ranger'):       {'anvil': ('Hunting Spear', w_spear)},
    ('halfling', 'ranger'):  {'anvil': ('Sling', w_sling)},
    ('construct', 'ranger'): {'anvil': ('Pulley Bow', w_brass_bow)},
    ('human', 'cleric'):     {'anvil': ('Flanged Mace', w_flanged_mace)},
    ('elf', 'cleric'):       {'anvil': ('Crescent Staff', w_crescent_staff)},
    ('dwarf', 'cleric'):     {'anvil': ("Smith's Warhammer", w_smith_hammer)},
    ('orc', 'cleric'):       {'anvil': ('Bone Mace', w_bone_mace)},
    ('halfling', 'cleric'):  {'anvil': ('Walking Staff', w_walking_staff)},
    ('construct', 'cleric'): {'anvil': ('Lantern Pole', w_lantern_pole)},
    ('human', 'monk'):     {'anvil': ('Bo Staff', w_bo_staff)},
    ('elf', 'monk'):       {'anvil': ('War Fan', w_war_fan)},
    ('dwarf', 'monk'):     {'anvil': ('Knuckles', w_knuckles)},
    ('orc', 'monk'):       {'anvil': ('Cestus', w_cestus)},
    ('halfling', 'monk'):  {'anvil': ('Gourd', w_gourd)},
    ('construct', 'monk'): {'anvil': ('Tonfa', w_tonfa)},
}
# ====================================================================== HEARTH: the defensive piece

def heater(ic, field, emblem=None):
    mask = rows('''
###############
###############
###############
###############
###############
###############
###############
 #############
 #############
  ###########
   #########
    #######
     #####
      ###''', 7, 4)
    ic.met(mask)
    inner = {(x, y) for (x, y) in mask if all((x + dx, y + dy) in mask for dx in (-1, 0, 1) for dy in (-1, 0, 1))}
    inner = {(x, y) for (x, y) in inner if all((x + dx, y + dy) in mask for dx in (-2, 2) for dy in (-2, 2)) or y < 7}
    ic.part(inner, field, outline=False)
    return inner

def h_heater_shield(m):
    ic = G(m); heater(ic, CLOTH['white'])
    ic.dots([(14, y) for y in range(7, 17)] + [(15, y) for y in range(7, 17)] + [(x, 10) for x in range(10, 20)] + [(x, 11) for x in range(10, 20)], CLOTH['crimson'][1])
    return ic.done()

def h_leaf_shield(m):
    ic = G(m); heater(ic, CLOTH['navy'])
    ic.part(rows('''
  ##
 ####
######
 ####
  ##
  #
  #''', 12, 7), GEM_GREEN)
    return ic.done()

def h_tower_shield(m):
    ic = G(m)
    ic.met(rect(8, 2, 21, 27))
    ic.part(rect(10, 4, 19, 25), CLOTH['black'], outline=False)
    ic.dots([(14, 8), (15, 8), (14, 9), (15, 9), (12, 11), (13, 11), (14, 11), (15, 11), (16, 11), (17, 11), (14, 12), (15, 12), (14, 13), (15, 13), (14, 14), (15, 14), (14, 15), (15, 15)], GEM_PURPLE[1])
    for (x, y) in ((9, 3), (20, 3), (9, 26), (20, 26)):
        ic.lit(x, y)
    return ic.done()

def h_round_shield(m):
    ic = G(m)
    ic.met(ellipse(14.5, 14.5, 12, 12))
    ic.part(ellipse(14.5, 14.5, 10, 10), CLOTH['crimson'], outline=False)
    ic.dots([(14 + dx, y) for y in range(5, 25, 3) for dx in (0,)], CLOTH['crimson'][2])
    ic.met(ellipse(14.5, 14.5, 3, 3))
    ic.lit(13, 13)
    return ic.done()

def helm_dome(ic, horns=False, nose=True):
    dome = ellipse(14.5, 14, 9, 9) - {(x, y) for x in range(30) for y in range(19, 30)}
    ic.met(dome | rect(6, 14, 9, 22) | rect(20, 14, 23, 22))
    ic.dots([(x, y) for x in range(9, 21) for y in (15, 16)], '#161B22')
    ic.dots([(x, y) for x in range(10, 20) for y in range(17, 22)], '#161B22')
    if nose:
        ic.met(rect(14, 13, 15, 20))
    ic.met(rect(5, 12, 24, 13))
    if horns:
        ic.part(rows('''
##
###
 ###
  ##
   #''', 1, 2), BONE)
        ic.part(rows('''
   ##
  ###
 ###
 ##
 #''', 23, 2), BONE)

def h_horned_helm(m):
    ic = G(m); helm_dome(ic, horns=True); return ic.done()

def h_infiltrator_visor(m):
    ic = G(m)
    ic.met(rect(5, 4, 24, 24) - {(5, 4), (24, 4), (5, 24), (24, 24)})
    ic.part(rect(7, 11, 22, 14), ('#222E3B', '#161B22', '#0E1116'), outline=True)
    ic.dots([(x, 12) for x in range(8, 22)], GEM_PURPLE[1])
    ic.dots([(10, 12), (19, 12)], GEM_PURPLE[0])
    ic.dots([(9, 19), (12, 19), (15, 19), (18, 19)], METALS[m][3])
    return ic.done()

def h_antenna_helm(m):
    ic = G(m)
    ic.met(rect(5, 9, 24, 26) - {(5, 9), (24, 9)})
    ic.part(rect(8, 13, 21, 19), ('#222E3B', '#161B22', '#0E1116'), outline=True)
    ic.dots([(x, y) for x in (10, 11, 18, 19) for y in (15, 16)], GEM_PURPLE[1])
    ic.met(rect(14, 2, 15, 8))
    ic.part(ellipse(14.5, 1.5, 1.5, 1.5), GEM_RED)
    return ic.done()

def pointed_hat(ic, cloth, band=True, stars=False, floppy=False):
    if floppy:
        cone = rows('''
              ###
            #####
          #######
        ########
      #########
    ##########
   ###########
  ############''', 1, 6)
    else:
        cone = rows('''
            ##
          ####
         #####
        ######
       #######
      ########
     #########
    ##########
   ###########''', 1, 2)
    ic.part(cone, cloth)
    ic.part(rect(1, 19, 28, 22), cloth)                                      # brim
    if band:
        ic.met(rect(6, 16, 23, 18))
        ic.met(rect(13, 15, 16, 19))                                         # buckle
        ic.dots([(14, 17), (15, 17)], '#161B22')
    if stars:
        for (x, y) in ((12, 8), (16, 12), (9, 13)):
            ic.dots([(x, y)], GLOW['gold'])
            ic.dots([(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)], gi.GOLD[1])

def h_starry_hat(m):
    ic = G(m); pointed_hat(ic, CLOTH['navy'], stars=True); return ic.done()

def h_floppy_hat(m):
    ic = G(m); pointed_hat(ic, CLOTH['purple'], floppy=True)
    ic.part(rect(15, 8, 18, 11), CLOTH['green'])                             # a patch
    return ic.done()

def circlet(ic, gem, crescent=False):
    band = {(x, y) for x in range(3, 27) for y in range(14, 20)
            if 0.55 <= ((x - 14.5) / 12) ** 2 + ((y - 13) / 5.5) ** 2 <= 1.0}
    ic.met(band)
    ic.met(rows('''
  #
 ###
#####''', 12, 13))
    if crescent:
        ic.met(rows('''
 ###
#
#
 ###''', 13, 5))
    ic.part(ellipse(14.5, 13, 1.5, 1.5), gem)

def h_circlet(m):
    ic = G(m); circlet(ic, GEM_GREEN); return ic.done()

def h_moon_circlet(m):
    ic = G(m); circlet(ic, ('#FFFFFF', '#E8F0FF', '#9AAAD0'), crescent=True)
    glow(ic, [(19, 6)], '#C8D8FF'); return ic.done()

def hood(ic, cloth, mask=None):
    shape = rows('''
     ##########
   ##############
  ################
 ##################
 ##################
##################
##################
##################
##################
##################
 ################
  ##############
   ############''', 5, 3)
    ic.part(shape, cloth)
    face = rows('''
  ########
 ##########
 ##########
 ##########
 ##########
  ########''', 9, 8)
    ic.part(face, ('#222E3B', '#161B22', '#0E1116'), outline=True)
    if mask:
        ic.part(rect(10, 12, 19, 14), mask, outline=True)
    ic.dots([(12, 10), (17, 10)], '#E8E8E8')                                   # eyes in the dark

def h_cowl(m):
    ic = G(m); hood(ic, CLOTH['black'], mask=CLOTH['black'])
    ic.met(ellipse(14.5, 16, 2, 1.5)); return ic.done()

def h_shadow_hood(m):
    ic = G(m); hood(ic, CLOTH['midnight'])
    ic.met(rows('''
 #
###
 #''', 13, 15)); return ic.done()

def h_ranger_hood(m):
    ic = G(m); hood(ic, CLOTH['green'])
    ic.met(ellipse(14.5, 16, 2, 1.5)); return ic.done()

def h_rune_hood(m):
    ic = G(m); hood(ic, CLOTH['rust'])
    ic.met(rect(12, 15, 17, 18))
    ic.dots([(14, 16), (15, 17), (14, 17)], GLOW['blue']); return ic.done()

def h_goggle_cap(m):
    ic = G(m)
    ic.part(ellipse(14.5, 14, 11, 9) - {(x, y) for x in range(30) for y in range(18, 30)}, LEATHER)
    ic.part(rect(3, 16, 26, 19), LEATHER)
    ic.met(rect(4, 12, 25, 13))                                               # strap
    for cx in (9.5, 19.5):
        ic.met(ellipse(cx, 12.5, 3.5, 3.5))
        ic.part(ellipse(cx, 12.5, 2, 2), GEM_BLUE, outline=False)
    return ic.done()

def h_fur_hat(m):
    ic = G(m)
    ic.part(ellipse(14.5, 13, 11, 9), CLOTH['fur'])
    for (x, y) in ((8, 8), (13, 6), (19, 8), (10, 14), (17, 13), (22, 15)):
        ic.dots([(x, y), (x + 1, y)], CLOTH['fur'][2])
    ic.part(rect(3, 18, 26, 22), CLOTH['fur'])
    ic.met(ellipse(14.5, 19.5, 2.5, 2.5))
    return ic.done()

def h_horn_headdress(m):
    ic = G(m)
    ic.part(rect(6, 15, 23, 20), CLOTH['hide'])
    ic.part(rows('''
##
###
 ###
  ###
   ##''', 2, 4), BONE)
    ic.part(rows('''
   ##
  ###
 ###
###
##''', 23, 4), BONE)
    ic.met(rect(4, 8, 6, 9)); ic.met(rect(23, 8, 25, 9))                      # rings on the horns
    ic.part(rows('''
 #
###
###
 #
 #''', 13, 8), GEM_RED)
    ic.dots([(9, 17), (12, 18), (17, 18), (20, 17)], BONE[0])
    return ic.done()

def h_bandana(m):
    ic = G(m)
    ic.part(ellipse(13.5, 15, 10, 8) - {(x, y) for x in range(30) for y in range(17, 30)}, CLOTH['crimson'])
    ic.part(rows('''
###
####
 ####
  ###''', 22, 15), CLOTH['crimson'])                                         # the knot's tails
    for (x, y) in ((8, 11), (13, 9), (17, 12), (11, 14)):
        ic.dots([(x, y)], '#FFFFFF')
    ic.met(ellipse(5, 21, 2, 2)); ic.set(5, 21, None)                        # a hoop earring
    return ic.done()

def h_mail_shirt(m):
    ic = G(m)
    shirt = rect(8, 5, 21, 25) | rect(3, 5, 7, 14) | rect(22, 5, 26, 14)
    shirt -= rect(12, 5, 17, 7)
    ic.met(shirt)
    for (x, y) in shirt:
        if (x + y) % 2 == 0 and ic.get(x, y) not in (None, gc.OUTLINE):
            ic.set(x, y, METALS[m][3] if (x // 2 + y) % 3 == 0 else METALS[m][1])
    return ic.done()

def h_scale_cuirass(m):
    ic = G(m)
    plate = rect(7, 5, 22, 24) - rect(11, 5, 18, 7) | rect(4, 6, 6, 11) | rect(23, 6, 25, 11)
    ic.met(plate)
    for y in range(9, 24, 3):
        for x in range(8 + (y % 2), 22, 3):
            ic.dots([(x, y), (x + 1, y + 1)], METALS[m][3])
    ic.part(rect(7, 24, 22, 25), CLOTH['green'])
    return ic.done()

def h_spiked_pauldron(m):
    ic = G(m)
    ic.met(ellipse(14.5, 16, 11, 8) - {(x, y) for x in range(30) for y in range(20, 30)})
    ic.met(ellipse(14.5, 18, 9, 5) - {(x, y) for x in range(30) for y in range(23, 30)})
    for x in (6, 11, 18, 23):
        ic.met(rows('''
 #
 #
###''', x - 1, 3 if x in (11, 18) else 6))
    ic.part(rect(10, 23, 19, 25), LEATHER)
    return ic.done()

def h_skull_pauldron(m):
    ic = G(m)
    ic.met(ellipse(14.5, 18, 11, 7) - {(x, y) for x in range(30) for y in range(22, 30)})
    ic.part(ellipse(14.5, 12, 6, 6), BONE)
    ic.dots([(12, 12), (13, 12), (16, 12), (17, 12)], '#1A1A20')
    ic.dots([(13, 16), (15, 16), (17, 16)], BONE[2])
    return ic.done()

def h_leaf_brooch(m):
    ic = G(m)
    ic.part(rows('''
    ######
  ##########
 ############
##############
##############
##############
 ############''', 8, 12), CLOTH['green'])
    ic.met(rows('''
    ##
   ####
  ######
 #######
 ######
#####
###
#''', 8, 4))                                                                 # the leaf brooch
    ic.dots([(14, 6), (13, 7), (12, 8), (11, 9), (10, 10)], METALS[m][3])
    return ic.done()

def h_patched_cloak(m):
    ic = G(m)
    ic.part(rows('''
   ##########
  ############
 ##############
 ##############
################
################
################
################
################
 ##############''', 6, 7), CLOTH['brown'])
    ic.part(rect(9, 14, 12, 17), CLOTH['green']); ic.part(rect(17, 18, 20, 21), CLOTH['hide'])
    ic.met(ellipse(14.5, 8, 2.5, 2))
    return ic.done()

def h_holy_tome(m):
    ic = G(m)
    ic.part(rect(6, 4, 23, 26), CLOTH['crimson'])
    ic.part(rect(8, 5, 23, 25), ('#F2EEDF', '#E0D6BC', '#B8A880'), outline=True)   # pages at the edge
    ic.part(rect(6, 4, 21, 26), CLOTH['crimson'])
    for (x, y) in ((6, 4), (19, 4), (6, 24), (19, 24)):
        ic.met(rect(x, y, x + 2, y + 2))
    ic.met(rows('''
  #
  #
#####
  #
  #''', 11, 11))                                                             # the sun-cross
    ic.met(rect(20, 13, 23, 15))                                              # clasp
    return ic.done()

def h_smith_apron(m):
    ic = G(m)
    ic.part(rect(7, 6, 22, 27) | rect(4, 3, 7, 6) | rect(22, 3, 25, 6), LEATHER)
    ic.met(rows('''
########
 ######
  ####
  ####
 ######''', 11, 12))                                                         # the anvil emblem
    for (x, y) in ((8, 7), (21, 7), (8, 25), (21, 25)):
        ic.met({(x, y)})
    return ic.done()

def h_skull_talisman(m):
    ic = G(m)
    for i in range(10):
        ic.met({(4 + i, 3 + i)}, outline=False); ic.met({(25 - i, 3 + i)}, outline=False)
    ic.part(ellipse(14.5, 17, 6, 6), BONE)
    ic.dots([(12, 16), (13, 16), (16, 16), (17, 16)], '#1A1A20')
    ic.dots([(13, 20), (15, 20), (17, 20)], BONE[2])
    ic.met(rect(13, 10, 16, 11))
    return ic.done()

def h_friar_pendant(m):
    ic = G(m)
    for i in range(11):
        ic.set(5 + i, 2 + i, CLOTH['hide'][1]); ic.set(24 - i, 2 + i, CLOTH['hide'][1])   # the rope
    ic.met(ellipse(14.5, 18, 6, 6))
    ic.part(ellipse(14.5, 18, 4, 4), WOOD, outline=False)
    ic.met(rows('''
 #
###
 #
 #''', 13, 16), outline=False)
    return ic.done()

def h_halo(m):
    ic = G(m)
    ring = {(x, y) for x in range(30) for y in range(30) if 0.62 <= ((x - 14.5) / 12) ** 2 + ((y - 14.5) / 6) ** 2 <= 1.0}
    ic.met(ring)
    glow(ic, [(3, 7), (26, 20), (14, 4)], GLOW['gold'])
    return ic.done()

def beads(ic, bead, medallion=True, gem=None):
    for k in range(18):
        a = math.radians(k * 20)
        x, y = 14.5 + 10 * math.cos(a), 13 + 8 * math.sin(a)
        ic.part(ellipse(round(x), round(y), 1, 1), bead)
    if medallion:
        ic.met(ellipse(14.5, 24, 3, 3))
        if gem:
            ic.part(ellipse(14.5, 24, 1, 1), gem, outline=False)

def h_prayer_beads(m):
    ic = G(m); beads(ic, WOOD); return ic.done()

def h_zen_beads(m):
    ic = G(m); beads(ic, METALS[m][1:], gem=GEM_PURPLE); return ic.done()

def h_jade_bracers(m):
    ic = G(m)
    for x0 in (3, 16):
        ic.part(rect(x0, 6, x0 + 10, 23), GEM_GREEN)
        ic.met(rect(x0, 8, x0 + 10, 9)); ic.met(rect(x0, 20, x0 + 10, 21))
    return ic.done()

def h_champion_belt(m):
    ic = G(m)
    ic.part(rect(1, 11, 28, 18), LEATHER)
    ic.met(rect(8, 7, 21, 22))
    ic.part(rect(11, 10, 18, 19), gi.GOLD, outline=True)
    ic.dots([(14, 13), (15, 13), (13, 14), (16, 14), (14, 16), (15, 16)], METALS[m][3])
    return ic.done()

def h_red_sash(m):
    ic = G(m)
    ic.part(rect(1, 10, 28, 16), CLOTH['crimson'])
    ic.part(rows('''
 ###
#####
 ###
 ##
 ###
 ####''', 18, 15), CLOTH['crimson'])
    ic.met(ellipse(9, 13, 2.5, 2.5)); ic.set(9, 13, None)
    ic.met(ellipse(14, 13, 2.5, 2.5)); ic.set(14, 13, None)
    return ic.done()

def h_headband(m):
    ic = G(m)
    band = {(x, y) for x in range(2, 28) for y in range(10, 19) if 0.5 <= ((x - 14.5) / 12.5) ** 2 + ((y - 10) / 7) ** 2 <= 1.0}
    ic.part(band, CLOTH['saffron'])
    ic.part(rows('''
##
###
 ###
  ##''', 24, 16), CLOTH['saffron'])
    ic.met(rect(11, 14, 18, 18))
    ic.dots([(14, 16), (15, 16)], gi.GOLD[0])
    return ic.done()

# ====================================================================== BELLOWS: light gear

BOOT = rows('''
#########
#########
#########
#########
#########
#########
#########
#########
#########
#########
##########
############
##############
################
#################
#################
#################
 ################''', 5, 4)

def footwear(m, style, cloth=LEATHER, accent=None):
    ic = G(m)
    t = TIER[m]
    if style == 'boot':
        ic.part(BOOT, cloth)
        ic.part(rect(5, 22, 21, 23) - {(21, 23)}, ('#3A271B', '#2B1D14', '#1A120C'))
        ic.met(rect(4, 2, 14, 5)); ic.met(rect(16, 16, 21, 21) - {(21, 16), (20, 16), (21, 17)})
    elif style == 'sabaton':
        ic.met(BOOT)
        for y in (8, 12, 16):
            ic.dots([(x, y) for x in range(6, 13)], METALS[m][3])
        ic.met(rect(16, 16, 21, 21) - {(21, 16), (20, 16), (21, 17)})
        ic.part(rect(5, 22, 21, 23) - {(21, 23)}, ('#3A271B', '#2B1D14', '#1A120C'))
    elif style == 'elven':
        tall = rows('''
########
########
########
########
########
########
########
########
########
#########
###########
#############
###############
################
#################
 ################''', 6, 6)
        ic.part(tall, cloth)
        ic.met(rows('''
 ##
####
 ##''', 8, 9))                                                                # a leaf clasp
        ic.met(rect(6, 5, 13, 6))
    elif style == 'slipper':
        shoe = rows('''
       #######
     ###########
   ###############
  #################
 ##################
 ##################
  ################''', 2, 12)
        ic.part(shoe, cloth)
        ic.part(rows('''
  ###
 #   #
#    #
     #''', 19, 6), cloth)                                                      # the curled toe
        ic.met(rect(7, 11, 13, 14))
        ic.met(ellipse(19.5, 7, 1.5, 1.5))
    elif style == 'fur':
        ic.part(BOOT, cloth)
        ic.part(rect(3, 2, 15, 7), CLOTH['fur'])
        for (x, y) in ((5, 3), (9, 4), (12, 3), (7, 6)):
            ic.dots([(x, y)], CLOTH['fur'][2])
        ic.met(rect(5, 12, 13, 13))
    elif style == 'sandal':
        ic.part(rows('''
####
#####
######
###########
################
##################
###################''', 4, 13), ('#F2C29B', '#D9A27A', '#A8714F'))            # the foot
        ic.part(rect(3, 20, 24, 22) - {(24, 22)}, LEATHER)                       # sole
        for x in (9, 15):
            ic.part(rect(x, 16, x + 1, 19), LEATHER)                              # straps over the foot
        ic.part(rect(3, 14, 9, 15), LEATHER)                                      # ankle strap...
        ic.met(rect(6, 13, 8, 16))                                                # ...and its buckle
        ic.dots([(18, 19), (20, 19), (22, 19)], '#A8714F')                        # toes
    elif style == 'soft':
        ic.part(BOOT, cloth)
        for y in (8, 12, 16):
            ic.met(rect(5, y, 13, y))
        ic.met(rect(10, 7, 11, 17), outline=False)
    elif style == 'wraps':
        ic.part(rows('''
          #####
        ##########
      #############
     ###############
    ################''', 3, 14) | rect(7, 4, 13, 18), ('#F2EEDF', '#D9C9A3', '#A8946C'))
        for y in range(6, 20, 3):
            ic.dots([(x, y) for x in range(7, 14)], '#A8946C')
        ic.met(rect(6, 3, 14, 5))
    elif style == 'anklet':
        ring = {(x, y) for x in range(30) for y in range(30) if 0.5 <= ((x - 14.5) / 10) ** 2 + ((y - 12) / 4) ** 2 <= 1.0}
        ic.met(ring)
        for cx in (9, 14, 20):
            ic.met(ellipse(cx, 18, 1.5, 1.5))
        ic.part(ellipse(14.5, 22, 2, 2.5), accent or GEM_GREEN)
    elif style == 'bracers':
        for x0 in (3, 16):
            ic.part(rect(x0, 5, x0 + 10, 24), LEATHER)
            for y in (8, 14, 20):
                ic.met(rect(x0 + 2, y, x0 + 8, y + 1))
    elif style == 'legplate':
        ic.met(rect(9, 2, 20, 22) - {(9, 2), (20, 2)})
        ic.met(rect(7, 22, 22, 26))
        ic.part(ellipse(14.5, 9, 2, 2), accent or GEM_PURPLE, outline=True)
        ic.dots([(12, 15), (17, 15), (12, 18), (17, 18)], METALS[m][3])
    if t >= 4 and style not in ('anklet', 'legplate'):
        pass
    return ic.done()

def b(style, cloth=LEATHER, accent=None):
    return lambda m: footwear(m, style, cloth, accent)


HEARTH = {
    ('human', 'wizard'): ('Starry Hat', h_starry_hat),        ('elf', 'wizard'): ('Circlet', h_circlet),
    ('dwarf', 'wizard'): ('Rune Hood', h_rune_hood),          ('orc', 'wizard'): ('Horned Headdress', h_horn_headdress),
    ('halfling', 'wizard'): ('Floppy Hat', h_floppy_hat),     ('construct', 'wizard'): ('Leaf Shield', h_leaf_shield),
    ('human', 'warrior'): ('Heater Shield', h_heater_shield), ('elf', 'warrior'): ('Scale Cuirass', h_scale_cuirass),
    ('dwarf', 'warrior'): ('Horned Helm', h_horned_helm),     ('orc', 'warrior'): ('Spiked Pauldron', h_spiked_pauldron),
    ('halfling', 'warrior'): ('Round Shield', h_round_shield),('construct', 'warrior'): ('Tower Shield', h_tower_shield),
    ('human', 'rogue'): ('Cowl', h_cowl),                     ('elf', 'rogue'): ('Shadow Hood', h_shadow_hood),
    ('dwarf', 'rogue'): ('Goggled Cap', h_goggle_cap),        ('orc', 'rogue'): ('Bandana', h_bandana),
    ('halfling', 'rogue'): ('Mail Shirt', h_mail_shirt),      ('construct', 'rogue'): ('Infiltrator Visor', h_infiltrator_visor),
    ('human', 'ranger'): ('Ranger Hood', h_ranger_hood),      ('elf', 'ranger'): ('Leaf Brooch', h_leaf_brooch),
    ('dwarf', 'ranger'): ('Fur Hat', h_fur_hat),              ('orc', 'ranger'): ('Skull Pauldron', h_skull_pauldron),
    ('halfling', 'ranger'): ('Patched Cloak', h_patched_cloak),('construct', 'ranger'): ('Antenna Helm', h_antenna_helm),
    ('human', 'cleric'): ('Holy Tome', h_holy_tome),          ('elf', 'cleric'): ('Moon Circlet', h_moon_circlet),
    ('dwarf', 'cleric'): ("Smith's Apron", h_smith_apron),    ('orc', 'cleric'): ('Skull Talisman', h_skull_talisman),
    ('halfling', 'cleric'): ("Friar's Pendant", h_friar_pendant), ('construct', 'cleric'): ('Halo', h_halo),
    ('human', 'monk'): ('Prayer Beads', h_prayer_beads),      ('elf', 'monk'): ('Jade Bracers', h_jade_bracers),
    ('dwarf', 'monk'): ("Champion's Belt", h_champion_belt),  ('orc', 'monk'): ('Red Sash', h_red_sash),
    ('halfling', 'monk'): ('Lucky Headband', h_headband),     ('construct', 'monk'): ('Zen Beads', h_zen_beads),
}

BELLOWS = {
    ('human', 'wizard'): ('Curled Slippers', b('slipper', CLOTH['navy'])),   ('elf', 'wizard'): ('Silk Slippers', b('slipper', CLOTH['moon'])),
    ('dwarf', 'wizard'): ('Iron-shod Boots', b('boot', CLOTH['rust'])),      ('orc', 'wizard'): ('Fur Boots', b('fur', CLOTH['hide'])),
    ('halfling', 'wizard'): ('Toe Ring', b('anklet', accent=GEM_PURPLE)),    ('construct', 'wizard'): ('Leg Plates', b('legplate', accent=GEM_PURPLE)),
    ('human', 'warrior'): ('Sabatons', b('sabaton')),                       ('elf', 'warrior'): ('Elven Boots', b('elven', CLOTH['green'])),
    ('dwarf', 'warrior'): ('War Boots', b('sabaton')),                       ('orc', 'warrior'): ('Fur Boots', b('fur', LEATHER)),
    ('halfling', 'warrior'): ('Ankle Guard', b('anklet', accent=GEM_RED)),   ('construct', 'warrior'): ('Leg Plates', b('legplate', accent=GEM_PURPLE)),
    ('human', 'rogue'): ('Soft Boots', b('soft', CLOTH['black'])),          ('elf', 'rogue'): ('Silent Boots', b('soft', CLOTH['midnight'])),
    ('dwarf', 'rogue'): ('Tunnel Boots', b('boot', LEATHER)),                ('orc', 'rogue'): ("Brigand's Boots", b('boot', CLOTH['black'])),
    ('halfling', 'rogue'): ('Lucky Anklet', b('anklet', accent=GEM_BLUE)),   ('construct', 'rogue'): ('Silent Plates', b('legplate', accent=GEM_PURPLE)),
    ('human', 'ranger'): ('Bracers', b('bracers')),                          ('elf', 'ranger'): ('Elven Boots', b('elven', CLOTH['green'])),
    ('dwarf', 'ranger'): ('Trail Boots', b('boot', CLOTH['green'])),         ('orc', 'ranger'): ('Fur Boots', b('fur', CLOTH['hide'])),
    ('halfling', 'ranger'): ("Wanderer's Anklet", b('anklet', accent=GEM_GREEN)), ('construct', 'ranger'): ('Scout Plates', b('legplate', accent=GEM_GREEN)),
    ('human', 'cleric'): ('Sandals', b('sandal')),                           ('elf', 'cleric'): ('Moon Sandals', b('sandal')),
    ('dwarf', 'cleric'): ('Forge Boots', b('sabaton')),                      ('orc', 'cleric'): ('Bone-laced Boots', b('boot', CLOTH['crimson'])),
    ('halfling', 'cleric'): ("Pilgrim's Anklet", b('anklet', accent=('#FFF4C8', '#FFE08A', '#F2A848'))), ('construct', 'cleric'): ('Halo Plates', b('legplate', accent=('#FFF4C8', '#FFE08A', '#F2A848'))),
    ('human', 'monk'): ('Sandals', b('sandal')),                             ('elf', 'monk'): ('Foot Wraps', b('wraps')),
    ('dwarf', 'monk'): ('Foot Wraps', b('wraps')),                           ('orc', 'monk'): ('Foot Wraps', b('wraps')),
    ('halfling', 'monk'): ('Bead Anklet', b('anklet', accent=GEM_RED)),      ('construct', 'monk'): ('Zen Plates', b('legplate', accent=('#F2D468', '#E0C045', '#A88420'))),
}

for key, entry in HEARTH.items():
    GEAR[key]['hearth'] = entry
for key, entry in BELLOWS.items():
    GEAR[key]['bellows'] = entry


SLOTS = ('anvil', 'hearth', 'bellows')


def write(name, ic):
    colors = sorted({c for row in ic.px for c in row if c is not None and c not in (gc.OUTLINE, gc.SHADOW)})
    key = {gc.OUTLINE: 'A', gc.SHADOW: 'E'}
    key.update({c: gc.KEYS[i] for i, c in enumerate(colors)})
    lines = [f'#! {key[c]} {c}' for c in colors] + [''.join('.' if c is None else key[c] for c in row) for row in ic.px]
    (ART / 'sprites' / f'{name}.txt').write_text('\n'.join(lines) + '\n')


if __name__ == '__main__':
    slots = [a for a in sys.argv[1:] if a in SLOTS] or list(SLOTS)
    gc.W, gc.H = 30, 30
    names = {}
    for slot in slots:
        cells = []
        for race in gc.RACES:
            for cls in gc.CLASSES:
                entry = GEAR[(race, cls)].get(slot)
                if entry is None:
                    continue
                label, draw = entry
                names[f'{race}_{cls}_{slot}'] = label
                for metal in METALS:
                    ic = draw(metal)
                    write(f'gear_{race}_{cls}_{slot}_{metal}', ic)
                    if metal == 'copper':
                        cells.append(ic)
        gc.sheet(cells, 6, 5, ART / 'out' / f'gear_{slot}.png')
        print(f'{slot}: {len(cells)} designs x {len(METALS)} metals')
    if slots == list(SLOTS):
        (ART / 'gear.json').write_text(json.dumps(names, indent=1, sort_keys=True) + '\n')
        cs = ART.parent / 'Forge' / 'Forge.Core' / 'Training' / 'HeroGear.g.cs'
        entries = '\n'.join(f'            ["{k}"] = "{v}",' for k, v in sorted(names.items()))
        cs.write_text(f'''// <auto-generated> by art/gen_gear.py from art/gear.json. Do not edit by hand. </auto-generated>
namespace Forge.Training
{{
    /// <summary>The name of every hero's forgeable pieces (Decision 22), keyed race_class_slot.</summary>
    public static partial class HeroGear
    {{
        public static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>
        {{
{entries}
        }};
    }}
}}
''')
