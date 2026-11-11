"""Generate the character roster: every race x class on one shared body frame.

    python3 art/gen_characters.py          # writes art/sprites/char_<race>_<class>.txt + a review sheet

The frame is fixed (32x48, four rows of headroom for hats): head, torso, hands and feet sit at the same pixels for every race, so
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

W, H = 32, 48
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
            for y in (GROUND + TOP, GROUND + 1 + TOP):
                if self.px[y][x] is None and not (y == GROUND + 1 + TOP and x in (8, 9, 23, 24)):
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
# Proportions: an 8-wide, 9-tall head on a body about four heads tall, so it reads as an adventurer,
# not a chibi. Every race and class draws on these pixels; forged gear will too.
#  head  x12..19 y7..15     torso x11..20 y16..27     arms x8..10 / x21..23 y16..25
#  hands x8..10 / x21..23 y26..28     legs x12..14 / x17..19 y28..37     feet y38..39
HEAD = rect(12, 7, 19, 15) - {(12, 7), (19, 7), (12, 15), (19, 15)}
TORSO = rect(11, 16, 20, 27)
ARM_R = rect(8, 16, 10, 25)        # the character's right arm (viewer's left): weapon hand
ARM_L = rect(21, 16, 23, 25)
HAND_R = rect(8, 26, 10, 28)
HAND_L = rect(21, 26, 23, 28)
LEG_R = rect(12, 28, 14, 37)
LEG_L = rect(17, 28, 19, 37)
FOOT_R = rect(11, 38, 14, 39)
FOOT_L = rect(17, 38, 20, 39)
EYES = [(14, 11), (14, 12), (17, 11), (17, 12)]
MOUTH = [(15, 14), (16, 14)]
GROUND = 41                        # the ground shadow's first row

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
        # the robot: a gold box head, dark visor, purple eyes, centre light, ear bolt
        # A robot can wear a bigger box head than the frame's 8x9 and still take a helm.
        cv.part(rect(22, 9, 24, 12), 'brass')                          # ear bolt (behind)
        cv.dots([(23, 10), (23, 11)], '#5A3A10')
        cv.part(rect(10, 6, 21, 15), 'brass')
        cv.part(rect(11, 8, 20, 13), ('#222E3B', '#161B22', '#0E1116'), flat=True)
        cv.dots([(x, y) for x in (12, 13, 18, 19) for y in (9, 10, 11)], C('purple', 1))
        cv.dots([(15, 12), (16, 12)], C('purple', 0))                  # the little centre light
        return
    skin = r['skin']
    # ears behind the head
    if race == 'elf':
        cv.part(rows('''
##
###
 ###
  ##''', 8, 9), skin)
        cv.part(rows('''
  ##
 ###
###
##''', 20, 9), skin)
    elif race == 'halfling':
        cv.part(rows('''
 #
##
 #''', 10, 10), skin)
        cv.part(rows('''
#
##
#''', 20, 10), skin)
    elif race == 'orc':
        cv.part(rect(10, 9, 11, 12), skin)
        cv.part(rect(20, 9, 21, 12), skin)
    else:
        cv.part(rect(11, 10, 11, 12), skin)
        cv.part(rect(20, 10, 20, 12), skin)
    cv.part(HEAD, skin)
    cv.dots(EYES, '#1A1A20')
    if race == 'orc':
        cv.dots([(13, 10), (14, 10), (17, 10), (18, 10)], C(skin, 2))           # heavy brow
        cv.dots([(14, 14), (17, 14), (14, 13), (17, 13)], '#F2EEDF')            # tusks
    elif race == 'dwarf':
        cv.part(rows('''
 ########
##########
##########
 ########
  ######
   ####
    ##''', 11, 13), r['hair'])                                               # the beard
        cv.dots([(15, 14), (16, 14)], C('hair_ginger', 2))
    else:
        cv.dots(MOUTH, C(skin, 2))
    if race == 'halfling':
        cv.dots([(13, 13), (18, 13)], '#E88A7A')                                 # rosy cheeks
        cv.dots([(13, 12), (18, 12)], C(skin, 2))                                # freckles

def side_curls(cv):
    """Halfling curls that show under any hat or hood: they're the race's tell."""
    hair = RACES['halfling']['hair']
    cv.part(rows('''
##
###
##
 #''', 10, 9), hair)
    cv.part(rows('''
 ##
###
 ##
 #''', 19, 9), hair)

def draw_hair(cv, race, hidden):
    r = RACES[race]
    if race == 'halfling' and hidden:
        side_curls(cv)
    if race == 'construct' or hidden:
        return
    hair = r['hair']
    if race == 'elf':
        cv.part(rows('''
 ########
##########
##      ##
#        #
#        #
#        #
#        #
#        #
##      ##''', 11, 5), hair)                                            # long, past the shoulders
    elif race == 'orc':
        cv.part(rows('''
 ######
########''', 12, 5), hair)                                              # flat-top
    elif race == 'halfling':
        cv.part(rows('''
 # ## ## #
##########
###########
###      ###
##        ##
##        ##''', 10, 4), hair)                                          # a big curly mop
        cv.dots([(12, 5), (15, 5), (18, 5), (10, 7), (21, 8)], C(hair, 0))
    else:
        cv.part(rows('''
  ####
 ######
########
##  #  ##
#        #''', 11, 5), hair)

# ------------------------------------------------------------------ classes

def body_parts(cv, race, sleeve, torso, pants, boots):
    """Legs, feet, torso and arms in the class's colours."""
    skin = RACES[race]['skin']
    cv.part(LEG_R, pants); cv.part(LEG_L, pants)
    if race == 'halfling':
        # rolled-up trousers and big bare hairy feet
        cv.part(rect(12, 35, 14, 37), skin); cv.part(rect(17, 35, 19, 37), skin)
        cv.part(rect(10, 38, 14, 39), skin); cv.part(rect(17, 38, 21, 39), skin)
        cv.dots([(12, 36), (13, 35), (18, 36), (19, 35)], C('hair_curly', 1))
        cv.dots([(10, 39), (11, 39), (12, 39), (19, 39), (20, 39), (21, 39)], C(skin, 2))
    else:
        cv.part(FOOT_R, boots); cv.part(FOOT_L, boots)
    torso_mask = TORSO | (rect(10, 17, 21, 27) if race in ('dwarf', 'orc') else set())   # broad builds
    cv.part(torso_mask, torso)
    cv.part(ARM_R, sleeve); cv.part(ARM_L, sleeve)
    if race == 'construct':
        # gold limbs with dark joints, like the robot
        cv.part(ARM_R, 'brass'); cv.part(ARM_L, 'brass')
        cv.dots([(9, 20), (22, 20)], '#5A3A10')
        cv.part(LEG_R, 'brass'); cv.part(LEG_L, 'brass')
        cv.dots([(13, 32), (18, 32)], '#5A3A10')
    return skin

def hands(cv, ramp):
    cv.part(HAND_R, ramp)
    cv.part(HAND_L, ramp)


def wizard_hat(cv):
    """Tall cone with a folded tip, a gold band, a wide brim: the robot's hat."""
    cv.part(rows('''
    ##
   ###
   ####
  #####
  ######
 #######
 ########
##########''', 11, -4), 'navy')
    cv.part(rows('''
###
 ###
  ###
   ##''', 16, -5), 'navy')                                   # the tip, folded over to the right
    cv.part(rect(11, 4, 20, 5), 'gold')                      # band
    cv.part(rect(8, 6, 23, 7), 'navy')                       # brim

def wizard(cv, race):
    hand = body_parts(cv, race, 'navy', 'navy', 'navy', 'leather')
    if race == 'construct':
        cv.part(FOOT_R, 'brass'); cv.part(FOOT_L, 'brass')
        cv.part(rect(11, 26, 20, 27), 'navy')                                     # hip band
        cv.part(rect(10, 16, 11, 20), 'brass'); cv.part(rect(20, 16, 21, 20), 'brass')   # shoulder panels
    else:
        cv.part(rect(11, 26, 20, 36), 'navy')                                     # the robe, to the ankles
        cv.dots([(15, y) for y in range(28, 36, 2)], C('navy', 2))                # its fold
    cv.part(rect(12, 24, 19, 24), 'gold', outline=False)                          # belt
    cv.dots([(15, 18), (16, 18), (14, 19), (15, 20), (16, 20), (17, 21), (16, 22), (15, 22)], C('purple', 1))   # the S rune
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
  #
 ###
## ##
#####''', 4, 15), ('#C8CCD2', '#9AA0A8', '#6A7078'))
        hands(cv, 'brass')
        shield(cv, 'gold', emblem='leaf')
    else:
        staff(cv, orb='purple')
        hands(cv, hand)

def warrior(cv, race):
    hand = body_parts(cv, race, 'iron', 'iron', 'pants', 'iron')
    cv.part(rect(13, 17, 18, 29), 'red')                         # tabard, past the belt
    cv.part(rect(11, 24, 20, 24), 'leather', outline=False)      # belt
    cv.dots([(15, 24), (16, 24)], C('gold', 1))
    cv.part(rect(8, 15, 12, 17), 'iron'); cv.part(rect(19, 15, 23, 17), 'iron')   # pauldrons
    draw_head(cv, race)
    draw_hair(cv, race, hidden=False)
    sword(cv)
    hands(cv, hand)
    shield(cv, 'iron', emblem='cross')

def rogue(cv, race):
    cv.part(rect(9, 15, 22, 35), 'charcoal')                     # cloak behind the body
    hand = body_parts(cv, race, 'charcoal', 'leather', 'charcoal', 'leather')
    cv.part(rect(11, 23, 20, 23), 'charcoal', outline=False)     # belt with buckles
    cv.dots([(13, 23), (18, 23)], C('steel', 1))
    cv.dots([(12 + i, 17 + i) for i in range(6)], C('charcoal', 2))   # baldric
    # hood: behind the head...
    cv.part(rows('''
  ######
 ########
##########
##########
##########
##########
##########
##########
##########
 ########''', 11, 4), 'charcoal')
    draw_head(cv, race)
    # ...and the hood's front lip over the brow, and a face mask
    cv.part(rows('''
 ######
########
#      #''', 12, 5), 'charcoal')
    if race != 'construct':
        cv.part(rect(13, 13, 18, 15), 'charcoal')
    draw_hair(cv, race, hidden=True)
    dagger(cv)
    hands(cv, hand)
    cv.part(rows('''
 #
###
 #
 #
 #
 #''', 22, 20), 'steel')                                        # off-hand dagger

def ranger(cv, race):
    cv.part(rect(9, 15, 22, 32), 'forest')                       # short cloak
    cv.part(rect(5, 10, 7, 20), 'leather')                       # quiver over the shoulder
    cv.dots([(5, 8), (6, 7), (7, 8), (5, 9), (7, 9)], C('red', 0))
    cv.dots([(6, 8), (6, 9)], C('cream', 0))
    hand = body_parts(cv, race, 'leather', 'forest', 'pants', 'leather')
    cv.dots([(12 + i, 17 + i) for i in range(8)], C('leather', 2))   # quiver strap
    cv.part(rect(11, 25, 20, 25), 'leather', outline=False)
    draw_head(cv, race)
    draw_hair(cv, race, hidden=False)
    cv.part(rows('''
  ####
 ######
########''', 12, 4), 'forest')                                # feathered cap
    cv.part(rows('''
#
##
 ##
  #''', 19, 1), 'red')
    # the bow in the off hand, its string to the right
    cv.part({(26, y) for y in range(10, 37)} | {(25, 9), (25, 37), (24, 8), (24, 38)}, 'wood')
    for y in range(11, 37):
        cv.set(28, y, '#D9C9A3')
    hands(cv, hand)
    cv.part(rect(21, 26, 23, 28) | rect(24, 21, 26, 23), 'leather')   # the bow hand, gloved

def cleric(cv, race):
    hand = body_parts(cv, race, 'cream', 'cream', 'cream', 'leather')
    cv.part(rect(11, 26, 20, 36), 'cream')                        # vestment to the ankles
    cv.part(rect(15, 16, 16, 36), 'gold', outline=False)          # stole
    cv.dots([(14, 19), (15, 18), (16, 18), (17, 19), (15, 20), (16, 20)], C('gold', 0))   # sun
    draw_head(cv, race)
    draw_hair(cv, race, hidden=False)
    cv.part(rect(12, 7, 19, 7), 'gold')                           # circlet
    cv.dots([(15, 7), (16, 7)], '#7CC4E8')
    mace(cv)
    hands(cv, hand)
    shield(cv, 'cream', emblem='sun', small=True)

def monk(cv, race):
    body_parts(cv, race, 'saffron', 'saffron', 'saffron', 'leather')
    cv.part(rows('''
##########
 ########
  ######
   ####''', 11, 16), 'saffron')                                  # crossed gi front
    cv.dots([(15, 17), (16, 18), (15, 19), (16, 20)], C('saffron', 2))
    cv.part(rect(11, 24, 20, 25), 'charcoal')                     # sash
    cv.part(rect(19, 25, 20, 29), 'charcoal')
    draw_head(cv, race)
    if race in ('halfling', 'elf'):
        draw_hair(cv, race, hidden=False)                         # not every monk shaves
    elif race != 'construct':
        cv.dots([(13, 7), (14, 7), (17, 7), (18, 7)], C(RACES[race]['skin'], 0))   # shaved head shine
    cv.dots([(12, 17), (13, 18), (14, 19), (15, 20), (16, 20), (17, 19), (18, 18), (19, 17)], C('wood', 1))  # prayer beads
    hands(cv, 'cream')                                             # wrapped fists
    cv.dots([(9, 27), (22, 27)], C('cream', 2))

# ------------------------------------------------------------------ held items (right hand at y26..28)

def staff(cv, orb):
    cv.part({(5, y) for y in range(3, 40)} | {(6, y) for y in range(3, 40)}, 'wood')
    cv.part(ellipse(5.5, 0, 2.5, 2.5), orb)
    cv.dots([(4, -1)], '#FFFFFF')

def sword(cv):
    cv.part(rect(5, 4, 6, 22), 'steel')                           # blade
    cv.dots([(5, 3)], C('steel', 0))
    cv.part(rect(3, 23, 8, 24), 'gold')                           # crossguard
    cv.part(rect(5, 25, 6, 29), 'leather')                        # grip
    cv.part(rect(5, 30, 6, 30), 'gold')                           # pommel

def dagger(cv):
    cv.part(rect(6, 18, 6, 25), 'steel')
    cv.part(rect(4, 26, 7, 26), 'gold')

def mace(cv):
    cv.part(rect(6, 11, 6, 32), 'wood')
    cv.part(ellipse(6, 8, 2, 2), 'iron')
    cv.dots([(6, 5), (3, 8), (9, 8)], C('iron', 2))

def shield(cv, ramp, emblem, small=False):
    if small:
        mask = ellipse(24.5, 24, 3.5, 3.5)
    else:
        mask = rows('''
#########
#########
#########
#########
#########
#########
#########
 #######
  #####
   ###
    #''', 20, 18)
    cv.part(mask, ramp)
    cx, cy = (24, 24) if small else (24, 22)
    if emblem == 'leaf':
        cv.part(rect(22, 20, 26, 26), 'navy', outline=False)
        cv.dots([(24, 21), (25, 21), (23, 22), (24, 22), (25, 22), (24, 23), (24, 24), (24, 25)], C('leaf', 1))
        cv.dots([(24, 21)], C('leaf', 0))
    elif emblem == 'cross':
        cv.dots([(cx, cy - 2), (cx, cy - 1), (cx, cy), (cx, cy + 1), (cx, cy + 2), (cx, cy + 3),
                 (cx - 2, cy), (cx - 1, cy), (cx + 1, cy), (cx + 2, cy)], C('red', 1))
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
