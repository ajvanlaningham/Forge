"""Generate the character roster: every race x class on one shared body frame.

    python3 art/gen_characters.py          # writes art/sprites/char_<race>_<class>.txt + a review sheet

The frame is fixed (32x40): head, torso, hands and feet sit at the same pixels for every race, so
forged gear (a helm on the head, boots on the feet, a weapon in the hand) can be layered over any
character later (Decision: "layered, wearable later"). Parts are drawn back to front; each part gets
its own black outline ring, which reads as the house style's chunky outlines between pieces.
Shading follows the house style: lit top and left edge, shaded right and bottom edge.

Construct x Wizard is the user's robot wizard (wizard_bot.png) redrawn in this frame: gold visor head
with purple eyes, ear bolt, navy hat with a gold band, navy robe with a purple rune, wrench and leaf
shield.
"""
import struct, sys, zlib
from pathlib import Path

W, H = 32, 44
TOP = 4            # headroom: frame y=0 is canvas row 4, so hats and helm crests can rise to y=-4
ART = Path(__file__).resolve().parent
OUT = ART / 'sprites'

# ------------------------------------------------------------------ colour ramps (light, mid, dark)
OUTLINE = '#000000'
SHADOW = '#2E3B47'

RAMPS = {
    # skin
    'skin_human':    ('#F2C29B', '#D9A27A', '#A8714F'),
    'skin_elf':      ('#F7E0CC', '#E6C3A6', '#B89276'),
    'skin_dwarf':    ('#F0B48E', '#D08A66', '#9A5A45'),
    'skin_orc':      ('#9CBF74', '#76A055', '#4E7639'),
    'skin_halfling': ('#F5C7A0', '#DDA47C', '#A9714E'),
    'brass':         ('#F2CC52', '#D2A032', '#8C6418'),   # the robot's gold
    # hair
    'hair_brown':    ('#9A6A42', '#734A2B', '#4A2E1A'),
    'hair_blonde':   ('#F4E2A0', '#D8B86A', '#A8853E'),
    'hair_black':    ('#4A4A52', '#2E2E36', '#1A1A20'),
    'hair_ginger':   ('#D2703E', '#A84E26', '#6E3016'),
    'hair_curly':    ('#A8744A', '#7C5232', '#53341E'),
    # cloth and leather
    'navy':          ('#4A5D72', '#34465A', '#222E3B'),
    'red':           ('#C25A48', '#8E2A2A', '#5E1A1A'),
    'iron':          ('#C8CCD2', '#8E949C', '#55595F'),
    'leather':       ('#A47450', '#7D5839', '#4A3222'),
    'forest':        ('#7E9E58', '#56733C', '#344A26'),
    'charcoal':      ('#5A5E66', '#3C4048', '#24272D'),
    'cream':         ('#F2EEDF', '#D9C9A3', '#A8946C'),
    'saffron':       ('#F2A848', '#D27E24', '#94520E'),
    'wood':          ('#B07E52', '#80583A', '#523622'),
    'gold':          ('#F2D468', '#E0C045', '#A88420'),
    'steel':         ('#F0F2F5', '#B8BEC6', '#7A828C'),
    'leaf':          ('#7CC46A', '#3E8E4A', '#255A30'),
    'purple':        ('#C690F0', '#A060D8', '#6A3A9A'),
    'pants':         ('#6A5E50', '#4E4438', '#332C24'),
}

# ------------------------------------------------------------------ the canvas

class Canvas:
    """A grid of RGB hex strings (None = transparent) with part-by-part drawing."""
    def __init__(self):
        self.px = [[None] * W for _ in range(H)]

    def get(self, x, y):
        y += TOP
        return self.px[y][x] if 0 <= x < W and 0 <= y < H else None

    def set(self, x, y, c):
        y += TOP
        if 0 <= x < W and 0 <= y < H:
            self.px[y][x] = c

    def part(self, mask, ramp, outline=True, flat=False):
        """Paint a shaded part. mask: set of (x, y). Its outline ring (4-neighbour) is painted
        first over whatever is beneath, so pieces read as separate chunks."""
        light, mid, dark = RAMPS[ramp] if isinstance(ramp, str) else ramp
        if outline:
            for (x, y) in mask:
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    p = (x + dx, y + dy)
                    if p not in mask:
                        self.set(*p, OUTLINE)
        for (x, y) in mask:
            if flat:
                c = mid
            elif (x, y - 1) not in mask or (x - 1, y) not in mask:
                c = light
            elif (x, y + 1) not in mask or (x + 1, y) not in mask:
                c = dark
            else:
                c = mid
            self.set(x, y, c)

    def dots(self, pts, color):
        for p in pts:
            self.set(*p, color)

    def finish(self):
        """Outer outline around the silhouette, then the ground shadow under the feet."""
        filled = {(x, y) for y in range(H) for x in range(W) if self.px[y][x] is not None}
        for y in range(H):
            for x in range(W):
                if (x, y) in filled:
                    continue
                if any((x + dx, y + dy) in filled for dx in (-1, 0, 1) for dy in (-1, 0, 1) if dx or dy):
                    self.px[y][x] = OUTLINE
        for x in range(8, 25):                     # ground shadow, behind everything
            for y in (37 + TOP, 38 + TOP):
                if self.px[y][x] is None and not (y == 38 + TOP and x in (8, 9, 23, 24)):
                    self.px[y][x] = SHADOW


def rect(x0, y0, x1, y1):
    return {(x, y) for x in range(x0, x1 + 1) for y in range(y0, y1 + 1)}

def ellipse(cx, cy, rx, ry):
    return {(x, y) for x in range(int(cx - rx), int(cx + rx) + 1) for y in range(int(cy - ry), int(cy + ry) + 1)
            if ((x - cx) / (rx + 0.5)) ** 2 + ((y - cy) / (ry + 0.5)) ** 2 <= 1}

def rows(spec, x0, y0):
    """A shape from text: any non-space char is in the mask."""
    return {(x0 + i, y0 + j) for j, line in enumerate(spec.strip('\n').splitlines()) for i, ch in enumerate(line) if ch != ' '}

def C(ramp, i):
    return RAMPS[ramp][i]

# ------------------------------------------------------------------ the frame (shared by everyone)
#  head  x10..21 y9..19     torso x11..20 y20..28     arms x8..10 / x21..23 y20..27
#  hands x8..10 / x21..23 y27..29     legs x12..14 / x17..19 y29..33     feet y34..35
HEAD = rect(10, 9, 21, 19) - {(10, 9), (21, 9), (10, 19), (21, 19)}
TORSO = rect(11, 20, 20, 28)
ARM_R = rect(8, 20, 10, 26)        # the character's right arm (viewer's left): weapon hand
ARM_L = rect(21, 20, 23, 26)
HAND_R = rect(8, 27, 10, 29)
HAND_L = rect(21, 27, 23, 29)
LEG_R = rect(12, 29, 14, 33)
LEG_L = rect(17, 29, 19, 33)
FOOT_R = rect(11, 34, 14, 35)
FOOT_L = rect(17, 34, 20, 35)
EYES = [(13, 14), (13, 15), (18, 14), (18, 15)]

# ------------------------------------------------------------------ races

RACES = {
    'human':     dict(skin='skin_human', hair='hair_brown', label='Human'),
    'elf':       dict(skin='skin_elf', hair='hair_blonde', label='Elf'),
    'dwarf':     dict(skin='skin_dwarf', hair='hair_ginger', label='Dwarf'),
    'orc':       dict(skin='skin_orc', hair='hair_black', label='Orc'),
    'halfling':  dict(skin='skin_halfling', hair='hair_curly', label='Halfling'),
    'construct': dict(skin='brass', hair=None, label='Construct'),
}

def draw_head(cv, race):
    r = RACES[race]
    if race == 'construct':
        # the robot: a gold box head, dark visor, purple eyes, ear bolt
        cv.part(rect(9, 10, 22, 19), 'brass')
        cv.part(rect(11, 12, 20, 17), ('#222E3B', '#161B22', '#0E1116'), flat=True)
        cv.dots([(x, y) for x in (12, 13, 18, 19) for y in (13, 14, 15)], C('purple', 1))
        cv.dots([(12, 13), (18, 13)], C('purple', 0))
        cv.dots([(15, 15), (16, 15)], C('purple', 1))          # the little centre light
        cv.part(rect(23, 12, 25, 16), 'brass')                 # ear bolt
        cv.dots([(24, 14)], '#5A3A10')
        return
    # ears behind the head
    if race == 'elf':
        cv.part(rows('''
##
###
 ###
  ##''', 6, 11), r['skin'])
        cv.part(rows('''
  ##
 ###
###
##''', 22, 11), r['skin'])
    elif race == 'halfling':
        cv.part(rows('''
 #
##
##
 #''', 8, 12), r['skin'])                                       # small pointed ears
        cv.part(rows('''
#
##
##
#''', 22, 12), r['skin'])
    elif race in ('human', 'dwarf'):
        cv.part(rect(9, 13, 9, 16), r['skin'])
        cv.part(rect(22, 13, 22, 16), r['skin'])
    elif race == 'orc':
        cv.part(rect(8, 12, 9, 15), r['skin'])
        cv.part(rect(22, 12, 23, 15), r['skin'])
    cv.part(HEAD, r['skin'])
    # face
    cv.dots(EYES, '#1A1A20')
    if race == 'orc':
        cv.dots([(12, 13), (13, 13), (14, 13), (17, 13), (18, 13), (19, 13)], C('skin_orc', 2))  # heavy brow
        cv.dots([(13, 18), (18, 18)], '#F2EEDF')                                                  # tusks
        cv.dots([(13, 17), (18, 17)], '#F2EEDF')
    if race == 'halfling':
        cv.dots([(12, 16), (19, 16)], '#E88A7A')                                                  # rosy cheeks
        cv.dots([(12, 17), (14, 16), (17, 16), (19, 17)], C('skin_halfling', 2))                  # freckles
    if race in ('human', 'elf', 'halfling'):
        cv.dots([(15, 17), (16, 17)], C(r['skin'], 2))                                            # mouth
    if race == 'dwarf':
        # the beard: over the chin and down the chest
        cv.part(rows('''
 ##########
############
############
 ##########
  ########
   ######
    ####''', 10, 16), r['hair'])
        cv.dots([(15, 17), (16, 17)], C('hair_ginger', 2))                                        # mouth in the beard

def side_curls(cv):
    """Halfling curls that show under any hat or hood: they're the race's tell."""
    hair = RACES['halfling']['hair']
    cv.part(rows('''
##
###
##
###
 #''', 9, 11), hair)
    cv.part(rows('''
 ##
###
 ##
###
 #''', 20, 11), hair)


def draw_hair(cv, race, hidden):
    r = RACES[race]
    if race == 'halfling' and hidden:
        side_curls(cv)
    if race == 'construct' or hidden:
        return
    hair = r['hair']
    if race == 'elf':
        cv.part(rows('''
 ##########
############
##        ##
#          #
#          #
#          #
#          #
#          #
##        ##''', 10, 7), hair)
    elif race == 'orc':
        cv.part(rows('''
  ########
 ##########
############''', 10, 7), hair)                                    # flat-top
    elif race == 'halfling':
        cv.part(rows('''
  # ## ## ## #
 ############
##############
###        ###
##          ##
##          ##
 #          #''', 9, 6), hair)                                     # a big curly mop
        cv.dots([(11, 7), (15, 7), (19, 7), (9, 9), (22, 10)], C(hair, 0))
    elif race == 'dwarf':
        cv.part(rows('''
  ########
 ##########
############
#          #''', 10, 7), hair)
    else:
        cv.part(rows('''
  ########
 ##########
############
##   #    ##
#          #''', 10, 7), hair)

# ------------------------------------------------------------------ classes

def body_parts(cv, race, sleeve, torso, pants, boots, hand_ramp=None):
    """Legs, boots, torso and arms in the class's colours; hands in the race's skin (or gloves)."""
    skin = RACES[race]['skin']
    cv.part(LEG_R, pants); cv.part(LEG_L, pants)
    if race == 'halfling':
        # rolled-up trousers and big bare hairy feet
        cv.part(rect(12, 32, 14, 33), skin); cv.part(rect(17, 32, 19, 33), skin)
        cv.part(rect(10, 34, 14, 35), skin); cv.part(rect(17, 34, 21, 35), skin)
        cv.dots([(12, 33), (13, 32), (18, 33), (19, 32)], C('hair_curly', 1))
        cv.dots([(10, 35), (11, 35), (12, 35), (19, 35), (20, 35), (21, 35)], C(skin, 2))
    else:
        cv.part(FOOT_R, boots); cv.part(FOOT_L, boots)
    torso_mask = TORSO | (rect(10, 21, 21, 28) if race == 'dwarf' else set())   # dwarves are broad
    cv.part(torso_mask, torso)
    cv.part(ARM_R, sleeve); cv.part(ARM_L, sleeve)
    if race == 'construct':
        # gold limbs with dark joints, like the robot
        for arm in (ARM_R, ARM_L):
            cv.part(arm, 'brass')
        cv.dots([(9, 23), (22, 23)], '#5A3A10')
        cv.part(LEG_R, 'brass'); cv.part(LEG_L, 'brass')
        cv.dots([(13, 31), (18, 31)], '#5A3A10')
    return hand_ramp or skin

def hands(cv, ramp):
    cv.part(HAND_R, ramp)
    cv.part(HAND_L, ramp)


def wizard(cv, race):
    hand = body_parts(cv, race, 'navy', 'navy', 'navy', 'leather')
    if race == 'construct':
        cv.part(FOOT_R, 'brass'); cv.part(FOOT_L, 'brass')
        cv.part(rect(11, 27, 20, 28), 'navy')                    # hip band
        cv.part(rect(10, 20, 11, 24), 'brass'); cv.part(rect(20, 20, 21, 24), 'brass')   # shoulder panels
    else:
        cv.part(rect(11, 27, 20, 32), 'navy')                    # robe skirt over the legs
    cv.part(rect(12, 26, 19, 26), 'gold', outline=False)         # belt
    cv.dots([(15, 21), (16, 21), (14, 22), (15, 23), (16, 23), (16, 24), (15, 25), (14, 25)], C('purple', 1))  # the S rune
    draw_head(cv, race)
    draw_hair(cv, race, hidden=True)
    wizard_hat(cv)
    if race == 'construct':
        # the robot's own kit: wrench and leaf shield
        cv.part(rows('''
## ##
## ##
#####
 ###
  #
  #
  #
  #
  #
  #
  #
  #
 ###
## ##
#####''', 2, 15), ('#C8CCD2', '#9AA0A8', '#6A7078'))
        hands(cv, 'brass')
        shield(cv, 'gold', emblem='leaf')
    else:
        staff(cv, orb='purple')
        hands(cv, hand)

def wizard_hat(cv):
    """Tall cone with a folded tip, a thick gold band, a wide brim: the robot's hat."""
    cv.part(rows('''
     ##
    ###
    ####
   #####
   ######
  #######
  ########
 #########
 ##########
###########''', 10, -4), 'navy')
    cv.part(rows('''
###
 ####
   ###
    ##''', 16, -4), 'navy')                                  # the tip, folded over to the right
    cv.part(rect(10, 6, 21, 8), 'gold')                          # band
    cv.part(rect(6, 9, 25, 10), 'navy')                          # brim


def warrior(cv, race):
    hand = body_parts(cv, race, 'iron', 'iron', 'pants', 'iron')
    cv.part(rect(13, 21, 18, 28), 'red')                         # tabard
    cv.part(rect(11, 27, 20, 27), 'leather', outline=False)      # belt
    cv.dots([(15, 27), (16, 27)], C('gold', 1))
    cv.part(rect(7, 19, 11, 21), 'iron'); cv.part(rect(20, 19, 24, 21), 'iron')   # pauldrons
    draw_head(cv, race)
    draw_hair(cv, race, hidden=False)
    sword(cv)
    hands(cv, hand)
    shield(cv, 'iron', emblem='cross')

def rogue(cv, race):
    # cloak behind the body
    cv.part(rect(8, 19, 23, 33), 'charcoal')
    hand = body_parts(cv, race, 'charcoal', 'leather', 'charcoal', 'leather')
    cv.part(rect(11, 24, 20, 24), 'charcoal', outline=False)     # belt with buckles
    cv.dots([(13, 24), (18, 24)], C('steel', 1))
    # hood: behind the head...
    cv.part(rows('''
   ########
  ##########
 ############
##############
##############
##############
##############
##############
##############
 ############''', 9, 6), 'charcoal')
    draw_head(cv, race)
    # ...and the hood's front lip over the brow
    cv.part(rows('''
 ##########
############
##        ##
#          #''', 10, 7), 'charcoal')
    cv.part(rect(11, 17, 20, 19) if race != 'construct' else set(), 'charcoal')   # face mask
    draw_hair(cv, race, hidden=True)
    dagger(cv)
    hands(cv, hand)
    cv.part(rows('''
 #
###
 #
 #
 #''', 22, 23), 'steel')                                           # off-hand dagger

def ranger(cv, race):
    cv.part(rect(9, 19, 22, 32), 'forest')                        # short cloak
    cv.part(rect(6, 14, 8, 22), 'leather')                        # quiver over the shoulder
    cv.dots([(6, 12), (7, 11), (8, 12), (6, 13), (8, 13)], C('red', 0))
    cv.dots([(7, 12), (7, 13)], C('cream', 0))
    hand = body_parts(cv, race, 'leather', 'forest', 'pants', 'leather')
    cv.dots([(12 + i, 21 + i) for i in range(8)], C('leather', 2))   # quiver strap
    cv.part(rect(11, 26, 20, 26), 'leather', outline=False)
    draw_head(cv, race)
    draw_hair(cv, race, hidden=False)
    # feathered cap
    cv.part(rows('''
  ########
 ##########
############''', 10, 6), 'forest')
    cv.part(rows('''
#
##
 ##
  #''', 22, 3), 'red')
    # bow in the off hand, arrow tips over the right shoulder
    cv.part({(26, y) for y in range(12, 36)} | {(25, 11), (25, 36), (24, 10), (24, 37)}, 'wood')
    cv.dots([(27 - 1, y) for y in range(13, 35)], None)
    for y in range(12, 37):
        cv.set(28, y, '#D9C9A3') if 13 <= y <= 35 else None
    hands(cv, hand)
    cv.part(rect(21, 27, 23, 29) | rect(24, 23, 26, 25), 'leather')   # the bow hand, gloved

def cleric(cv, race):
    hand = body_parts(cv, race, 'cream', 'cream', 'cream', 'leather')
    cv.part(rect(11, 27, 20, 32), 'cream')                        # vestment skirt
    cv.part(rect(15, 20, 16, 32), 'gold', outline=False)          # stole
    cv.dots([(14, 23), (15, 22), (16, 22), (17, 23), (15, 24), (16, 24)], C('gold', 0))   # sun
    draw_head(cv, race)
    draw_hair(cv, race, hidden=False)
    cv.part(rect(10, 9, 21, 9), 'gold')                           # circlet
    cv.dots([(15, 9), (16, 9)], '#7CC4E8')
    mace(cv)
    hands(cv, hand)
    shield(cv, 'cream', emblem='sun', small=True)

def monk(cv, race):
    hand = body_parts(cv, race, 'saffron', 'saffron', 'saffron', 'leather', hand_ramp='cream')
    cv.part(rows('''
##########
 ########
  ######''', 11, 20), 'saffron')                                 # crossed gi front
    cv.dots([(15, 21), (16, 22), (15, 23)], C('saffron', 2))
    cv.part(rect(11, 26, 20, 27), 'charcoal')                     # sash
    cv.part(rect(19, 27, 20, 30), 'charcoal')
    draw_head(cv, race)
    if race in ('halfling', 'elf'):
        draw_hair(cv, race, hidden=False)                       # not every monk shaves
    elif race != 'construct':
        cv.dots([(11, 9), (12, 9), (19, 9), (20, 9)], C(RACES[race]['skin'], 0))   # shaved head shine
    cv.dots([(11, 22), (12, 23), (13, 24), (14, 25), (17, 25), (18, 24), (19, 23), (20, 22)], C('wood', 1))  # prayer beads
    hands(cv, 'cream')                                             # wrapped fists
    cv.dots([(9, 28), (22, 28)], C('cream', 2))

# ------------------------------------------------------------------ held items

def staff(cv, orb):
    cv.part({(5, y) for y in range(8, 36)} | {(6, y) for y in range(8, 36)}, 'wood')
    cv.part(ellipse(5.5, 5, 2.5, 2.5), orb)
    cv.dots([(4, 4)], '#FFFFFF')

def sword(cv):
    cv.part(rect(5, 6, 6, 24), 'steel')                           # blade
    cv.part(rect(3, 25, 8, 26), 'gold')                           # crossguard
    cv.part(rect(5, 27, 6, 30), 'leather')                        # grip
    cv.part(rect(5, 31, 6, 31), 'gold')                           # pommel

def dagger(cv):
    cv.part(rect(6, 19, 6, 26), 'steel')
    cv.part(rect(4, 27, 7, 27), 'gold')

def mace(cv):
    cv.part(rect(6, 14, 6, 30), 'wood')
    cv.part(ellipse(6, 12, 2, 2), 'iron')
    cv.dots([(6, 9), (3, 12), (9, 12)], C('iron', 2))

def shield(cv, ramp, emblem, small=False):
    if small:
        mask = ellipse(24.5, 27, 3.5, 3.5)
    else:
        mask = rows('''
#########
#########
#########
#########
#########
#########
 #######
  #####
   ###
    #''', 21, 22)
    cv.part(mask, ramp)
    cx, cy = (24, 27) if small else (25, 26)
    if emblem == 'leaf':
        cv.part(rect(23, 24, 27, 29), 'navy', outline=False)
        cv.dots([(25, 25), (26, 25), (24, 26), (25, 26), (26, 26), (25, 27), (25, 28)], C('leaf', 1))
        cv.dots([(25, 25)], C('leaf', 0))
    elif emblem == 'cross':
        cv.dots([(cx, cy - 2), (cx, cy - 1), (cx, cy), (cx, cy + 1), (cx, cy + 2), (cx - 2, cy - 1), (cx - 1, cy - 1), (cx + 1, cy - 1), (cx + 2, cy - 1)], C('red', 1))
    elif emblem == 'sun':
        cv.dots([(cx, cy), (cx + 1, cy), (cx, cy + 1), (cx + 1, cy + 1)], C('gold', 1))
        cv.dots([(cx, cy - 2), (cx + 1, cy + 3), (cx - 2, cy + 1), (cx + 3, cy)], C('gold', 2))

CLASSES = {
    'wizard': (wizard, 'Wizard'),
    'warrior': (warrior, 'Warrior'),
    'rogue': (rogue, 'Rogue'),
    'ranger': (ranger, 'Ranger'),
    'cleric': (cleric, 'Cleric'),
    'monk': (monk, 'Monk'),
}

# ------------------------------------------------------------------ output

KEYS = '0123456789abcdefghijklmnopqrstuvwxyzBCDFGHIJKLMNOPQRSTUVXYZ!@$%^&*+=~?<>:;'

def write(name, cv):
    colors = sorted({c for row in cv.px for c in row if c is not None and c not in (OUTLINE, SHADOW)})
    if len(colors) > len(KEYS):
        sys.exit(f'{name}: too many colours ({len(colors)})')
    key = {OUTLINE: 'A', SHADOW: 'E'}
    key.update({c: KEYS[i] for i, c in enumerate(colors)})
    lines = [f'#! {key[c]} {c}' for c in colors]
    lines += [''.join('.' if c is None else key[c] for c in row) for row in cv.px]
    (OUT / f'{name}.txt').write_text('\n'.join(lines) + '\n')
    return cv

def sheet(cells, cols, scale, path, pad=2, bg=(22, 27, 34)):
    """A contact sheet PNG of canvases (for review)."""
    rows_n = (len(cells) + cols - 1) // cols
    cw, ch = (W + pad) * scale, (H + pad) * scale
    width, height = cols * cw, rows_n * ch
    img = [[bg] * width for _ in range(height)]
    for i, cv in enumerate(cells):
        ox, oy = (i % cols) * cw + pad * scale // 2, (i // cols) * ch + pad * scale // 2
        for y in range(H):
            for x in range(W):
                c = cv.px[y][x]
                if c is None:
                    continue
                rgb = tuple(int(c[k:k + 2], 16) for k in (1, 3, 5))
                for yy in range(scale):
                    row = img[oy + y * scale + yy]
                    for xx in range(scale):
                        row[ox + x * scale + xx] = rgb
    raw = b''.join(b'\x00' + b''.join(bytes(p) for p in r) for r in img)
    chunk = lambda t, d: struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    path.write_bytes(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 2, 0, 0, 0))
                     + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))

def build(race, cls):
    cv = Canvas()
    CLASSES[cls][0](cv, race)
    cv.finish()
    return cv

if __name__ == '__main__':
    cells = []
    for race in RACES:
        for cls in CLASSES:
            cv = build(race, cls)
            write(f'char_{race}_{cls}', cv)
            cells.append(cv)
    (ART / 'out').mkdir(exist_ok=True)
    sheet(cells, len(CLASSES), 4, ART / 'out' / 'characters.png')
    print(f'wrote {len(cells)} characters; sheet: art/out/characters.png')
