"""Programs from recipes (Decision 28, 2026-11-13).

    python3 content/gen_programs.py            # write every quest's versions, print the timing table
    python3 content/gen_programs.py --check    # timing table only, write nothing

Writing a program by hand means picking dozens of exercises, sets and rests, and keeping every day
inside 30-40 minutes. Here a quest is a short recipe instead:

  * POOLS  - exercises by role (squat, hinge, horizontal push...) for each implement, drawn from the
             exercise library, so a recipe says "a squat" and each version gets its own.
  * a DAY  - blocks in order: warm-up, strength circuit, accessory circuit, finisher. Each block
             lists roles and a FORMAT: seconds on / off, rounds by week, suggested reps.
  * a QUEST - its identity (title, subtitle, discipline) and its days for 3 and 4 days a week.

Everything is timed and reps are only a suggested pace (Decision 28): a rep exercise becomes a
"paced" slot (seconds + a rep range), a hold or a timed move stays seconds only, a one-sided move
gets its seconds per side. The warm-up and strength circuit are the block's fixed "main" slots;
the accessory circuit and finisher rotate every week for variety. Every day of every week is
timed with the app's own formula (DayTiming / GuidedPlan) and the script refuses to write a day
outside 30-40 minutes. ProgramTests re-checks the shipped files the same way.

Adding a quest: add its recipe to QUESTS, run this, add the new files to GameConstants.Programs.
"""
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
RAW = ROOT / 'Forge' / 'Forge' / 'Resources' / 'Raw'
OUT = RAW / 'programs'

# The app's timing constants (GuidedPlan): a set's setup, the default rest.
SETUP = 5
MIN_MINUTES, MAX_MINUTES = 30, 40
TEST_DAY_MIN = 25  # week 4's test days: the test itself adds 5-15 minutes
WEEKS = 4

# --- the exercise library ---------------------------------------------------------------------------

LIBRARY = {}
for f in ('strength', 'mobility', 'conditioning', 'recovery'):
    for e in json.loads((RAW / f'{f}.v1.json').read_text()):
        if e.get('isActive', True):
            LIBRARY[e['name']] = e


def action(name):
    return LIBRARY[name]['action'].lower()


def one_sided(name):
    return bool(LIBRARY[name].get('unilateral'))


# --- pools: what can fill a role, per implement ('db' = dumbbells, 'kb' = one kettlebell) ----------
# Bodyweight moves appear in both. Order matters: the first entries are the defaults.

POOLS = {
    'squat':    {'db': ['Heel-Elevated Goblet Squat', 'Sumo Goblet Squat'],
                 'kb': ['Goblet Squat', 'Kettlebell Front Squat']},
    'hinge':    {'db': ['Romanian Deadlift', 'Staggered-Stance Romanian Deadlift', 'Hip Thrust'],
                 'kb': ['Kettlebell Deadlift', 'Kettlebell Swing']},
    'lunge':    {'db': ['Walking Lunge', 'Bulgarian Split Squat', 'Split Squat'],
                 'kb': ['Racked Reverse Lunge', 'Split Squat']},
    'push_h':   {'db': ['Dumbbell Floor Press', 'Push-up', 'Deficit Push-up'],
                 'kb': ['Kettlebell Floor Press', 'Push-up', 'Deficit Push-up']},
    'push_v':   {'db': ['Dumbbell Overhead Press', 'Arnold Press'],
                 'kb': ['Kettlebell Press']},
    'pull_h':   {'db': ['One-arm Row', 'Bent-over Row', 'Renegade Row'],
                 'kb': ['Kettlebell Row']},
    'pull_v':   {'db': ['Pull-up Negative', 'Dead Hang'], 'kb': ['Pull-up Negative', 'Dead Hang']},
    'power':    {'db': ['Squat Jumps', 'Burpee'], 'kb': ['Kettlebell Swing', 'Kettlebell Clean', 'Kettlebell Snatch']},
    'glute':    {'db': ['Hip Thrust', 'Glute Bridge', 'Feet-Elevated Hamstring Bridge'],
                 'kb': ['Glute Bridge', 'Feet-Elevated Hamstring Bridge']},
    'calf':     {'db': ['Dumbbell Calf Raise', 'Single-Leg Calf Raise'], 'kb': ['Single-Leg Calf Raise']},
    'shoulder': {'db': ['Lateral Raise', 'Rear Delt Fly'], 'kb': ['Kettlebell Halo', 'Turkish Get-up']},
    'biceps':   {'db': ['Dumbbell Curl', 'Hammer Curl'], 'kb': ['Dead Hang', 'Pull-up Negative']},
    'triceps':  {'db': ['Dumbbell Triceps Extension', 'Dumbbell Skull Crusher', 'Bench Dip'], 'kb': ['Bench Dip', 'Deficit Push-up', 'Push-up']},
    'core':     {'db': ['Dead Bug', 'Plank', 'Reverse Crunch', 'Side Plank', 'Hollow Hold'],
                 'kb': ['Dead Bug', 'Plank', 'Reverse Crunch', 'Side Plank', 'Hollow Hold']},
    'full':     {'db': ['Renegade Row', 'Farmer\'s Carry'], 'kb': ['Turkish Get-up', 'Kettlebell Clean']},
    'cardio':   {'db': ['Jumping Jacks', 'High Knees', 'Mountain Climbers', 'Burpee', 'Squat Jumps', 'Shadow Boxing'],
                 'kb': ['Jumping Jacks', 'High Knees', 'Mountain Climbers', 'Burpee', 'Squat Jumps', 'Shadow Boxing']},
    'warm':     {'db': ['Cat-Cow', 'World\'s Greatest Stretch', '90/90 Hip Switch', 'Air Squat', 'Glute Bridge', 'Lat Wall Slide', 'Thread the Needle'],
                 'kb': ['Cat-Cow', 'World\'s Greatest Stretch', '90/90 Hip Switch', 'Air Squat', 'Glute Bridge', 'Lat Wall Slide', 'Thread the Needle']},
}

# Short cues shown under the exercise name in the workout.
CUES = {
    'Dumbbell Floor Press': 'Elbows touch the floor lightly each rep',
    'Kettlebell Floor Press': 'Elbow touches the floor; press straight up',
    'One-arm Row': 'Free hand on the block; pull to the hip',
    'Kettlebell Row': 'Free hand on your knee; pull to the hip',
    'Kettlebell Swing': 'Hips snap the bell up; arms just guide it',
    'Romanian Deadlift': 'Soft knees, hips back, flat back',
    'Kettlebell Deadlift': 'Hips back, bell between the feet',
    'Goblet Squat': 'Bell at the chest, sit between the heels',
    'Heel-Elevated Goblet Squat': 'Heels on a plate, sit straight down',
    'Push-up': 'Rigid plank from head to heel',
    'Pull-up Negative': 'Jump up, lower slowly',
    'Dead Bug': 'Low back pressed to the floor',
    'Plank': 'Squeeze glutes, breathe',
    'Hip Thrust': 'Shoulders on the box, drive through the heels',
}

# --- formats ----------------------------------------------------------------------------------------
# on/off in seconds; rounds by week; reps = (min, max) suggested for a rep move.

def fmt(on, off, rounds, reps):
    return {'on': on, 'off': off, 'rounds': rounds, 'reps': reps}


# --- quests -------------------------------------------------------------------------------------------

FOUNDATIONS = {
    'quest': 'copper-helm', 'version': 4, 'name': 'Foundations',
    'subtitle': 'Balanced strength circuits, upper and lower days',
    'discipline': 'Hearth', 'goal': 'Balanced', 'difficulty': 1,
    'description': 'Timed circuits for the whole body: a short warm-up, a strength circuit of the '
                   'block\'s main lifts, an accessory circuit that changes every week, and a quick '
                   'finisher. About 35 minutes a day; reps are a suggested pace, the clock runs the rest.',
    'formats': {
        'warm': fmt(40, 15, [1, 1, 1, 1], (8, 10)),
        'strength': fmt(60, 45, [3, 3, 4, 4], (8, 12)),
        'accessory': fmt(45, 30, [3, 3, 3, 3], (10, 15)),
        'finisher': fmt(20, 10, [4, 4, 4, 4], (8, 12)),
    },
    # Three main lifts a day: the accessory and finisher give back time as the rounds rise.
    'formats_3': {
        'warm': fmt(40, 15, [1, 1, 1, 1], (8, 10)),
        'strength': fmt(60, 45, [3, 3, 4, 4], (8, 12)),
        'accessory': fmt(45, 30, [3, 3, 2, 2], (10, 15)),
        'finisher': fmt(20, 10, [4, 4, 3, 3], (8, 12)),
    },
    4: [
        ('Upper A', {'warm': ['warm', 'warm', 'warm', 'warm'], 'strength': ['push_h', 'pull_h'],
                     'accessory': ['shoulder', 'biceps', 'core'], 'finisher': ['cardio', 'cardio']}),
        ('Lower A', {'warm': ['warm', 'warm', 'warm', 'warm'], 'strength': ['squat', 'hinge'],
                     'accessory': ['lunge', 'glute', 'core'], 'finisher': ['cardio', 'cardio']}),
        ('Upper B', {'warm': ['warm', 'warm', 'warm', 'warm'], 'strength': ['push_v', 'pull_h'],
                     'accessory': ['push_h', 'triceps', 'core'], 'finisher': ['cardio', 'cardio']}),
        ('Lower B', {'warm': ['warm', 'warm', 'warm', 'warm'], 'strength': ['hinge', 'lunge'],
                     'accessory': ['squat', 'calf', 'core'], 'finisher': ['cardio', 'cardio']}),
    ],
    3: [
        ('Full body A', {'warm': ['warm', 'warm', 'warm', 'warm'], 'strength': ['squat', 'push_h', 'pull_h'],
                         'accessory': ['glute', 'shoulder', 'core'], 'finisher': ['cardio', 'cardio']}),
        ('Full body B', {'warm': ['warm', 'warm', 'warm', 'warm'], 'strength': ['hinge', 'push_v', 'pull_h'],
                         'accessory': ['lunge', 'biceps', 'core'], 'finisher': ['cardio', 'cardio']}),
        ('Full body C', {'warm': ['warm', 'warm', 'warm', 'warm'], 'strength': ['lunge', 'push_h', 'power'],
                         'accessory': ['squat', 'triceps', 'core'], 'finisher': ['cardio', 'cardio']}),
    ],
}

IRON_WILL = {
    'quest': 'copper-hammer', 'version': 2, 'name': 'Iron Will',
    'subtitle': 'Strength-focused lifting: heavier weights, slower circuits',
    'discipline': 'Anvil', 'goal': 'Strength', 'difficulty': 2,
    'description': 'Heavier weights at a slower pace: a long strength circuit with fuller rests, '
                   'a short accessory circuit and a carry or core finisher. Reps are suggested; '
                   'when it feels easy, the weight goes up.',
    'formats': {
        'warm': fmt(40, 15, [1, 1, 1, 1], (6, 8)),
        'strength': fmt(50, 60, [3, 4, 4, 4], (5, 8)),
        'accessory': fmt(45, 30, [2, 2, 2, 2], (8, 12)),
        'finisher': fmt(40, 20, [2, 2, 2, 2], (8, 12)),
    },
    4: [
        ('Upper A', {'warm': ['warm', 'warm', 'warm', 'warm'], 'strength': ['push_h', 'pull_h', 'push_v'],
                     'accessory': ['triceps', 'biceps'], 'finisher': ['core', 'full']}),
        ('Lower A', {'warm': ['warm', 'warm', 'warm', 'warm'], 'strength': ['squat', 'hinge', 'lunge'],
                     'accessory': ['glute', 'calf'], 'finisher': ['core', 'full']}),
        ('Upper B', {'warm': ['warm', 'warm', 'warm', 'warm'], 'strength': ['push_v', 'pull_h', 'push_h'],
                     'accessory': ['shoulder', 'biceps'], 'finisher': ['core', 'full']}),
        ('Lower B', {'warm': ['warm', 'warm', 'warm', 'warm'], 'strength': ['hinge', 'squat', 'glute'],
                     'accessory': ['lunge', 'calf'], 'finisher': ['core', 'full']}),
    ],
    3: [
        ('Full body A', {'warm': ['warm', 'warm', 'warm', 'warm'], 'strength': ['squat', 'push_h', 'pull_h'],
                         'accessory': ['glute', 'triceps'], 'finisher': ['core', 'full']}),
        ('Full body B', {'warm': ['warm', 'warm', 'warm', 'warm'], 'strength': ['hinge', 'push_v', 'pull_h'],
                         'accessory': ['lunge', 'biceps'], 'finisher': ['core', 'full']}),
        ('Full body C', {'warm': ['warm', 'warm', 'warm', 'warm'], 'strength': ['lunge', 'push_h', 'hinge'],
                         'accessory': ['squat', 'shoulder'], 'finisher': ['core', 'full']}),
    ],
}

SECOND_WIND = {
    'quest': 'copper-boots', 'version': 2, 'name': 'Second Wind',
    'subtitle': 'Conditioning circuits for heart and lungs',
    'discipline': 'Bellows', 'goal': 'Conditioning', 'difficulty': 1,
    'description': 'Kettlebell-class circuits: lighter weights, more moves, short rests, and a '
                   'twenty-on, ten-off finisher. Built to raise your heart rate and keep it there.',
    'formats': {
        'warm': fmt(40, 15, [1, 1, 1, 1], (10, 12)),
        'strength': fmt(45, 20, [4, 4, 4, 4], (10, 15)),
        'accessory': fmt(40, 20, [3, 3, 4, 4], (12, 20)),
        'finisher': fmt(20, 10, [4, 4, 5, 5], (8, 12)),
    },
    4: [
        ('Engine A', {'warm': ['warm', 'warm', 'warm', 'warm'], 'strength': ['power', 'squat', 'push_h'],
                      'accessory': ['cardio', 'lunge', 'core'], 'finisher': ['cardio', 'cardio']}),
        ('Engine B', {'warm': ['warm', 'warm', 'warm', 'warm'], 'strength': ['power', 'hinge', 'pull_h'],
                      'accessory': ['cardio', 'push_v', 'core'], 'finisher': ['cardio', 'cardio']}),
        ('Engine C', {'warm': ['warm', 'warm', 'warm', 'warm'], 'strength': ['power', 'lunge', 'push_v'],
                      'accessory': ['cardio', 'glute', 'core'], 'finisher': ['cardio', 'cardio']}),
        ('Engine D', {'warm': ['warm', 'warm', 'warm', 'warm'], 'strength': ['power', 'squat', 'pull_h'],
                      'accessory': ['cardio', 'push_h', 'core'], 'finisher': ['cardio', 'cardio']}),
    ],
    3: [
        ('Engine A', {'warm': ['warm', 'warm', 'warm', 'warm'], 'strength': ['power', 'squat', 'push_h'],
                      'accessory': ['cardio', 'lunge', 'core'], 'finisher': ['cardio', 'cardio']}),
        ('Engine B', {'warm': ['warm', 'warm', 'warm', 'warm'], 'strength': ['power', 'hinge', 'pull_h'],
                      'accessory': ['cardio', 'push_v', 'core'], 'finisher': ['cardio', 'cardio']}),
        ('Engine C', {'warm': ['warm', 'warm', 'warm', 'warm'], 'strength': ['power', 'lunge', 'push_v'],
                      'accessory': ['cardio', 'glute', 'core'], 'finisher': ['cardio', 'cardio']}),
    ],
}

# Miche's own plan (written with their Claude, 2026-11-12), converted to the timed format with
# their exercises and cues kept exactly: a block entry that names a library exercise is used as
# is; a role picks from the pools. A short warm-up is added; there's no finisher (a muscle-
# building split). The talk with Miche happened before this conversion (user, 2026-11-13).
FOURFOLD_PATH = {
    'quest': 'copper-split', 'version': 2, 'name': 'The Fourfold Path',
    'subtitle': 'Four-day upper/lower dumbbell circuits',
    'discipline': 'Hearth', 'goal': 'Balanced', 'difficulty': 2,
    'implements': ['db'], 'schedules': [4],
    'description': 'Upper / lower twice a week at home; every major muscle group twice a week. '
                   'A short warm-up, the day\'s two main lifts as a strength circuit, then an '
                   'accessory circuit. Timed sets with suggested reps; when it feels easy, the '
                   'weight goes up. Dumbbells, a knee-height block, a mat, two yoga blocks and a mini band.',
    'formats': {
        'warm': fmt(40, 15, [1, 1, 1, 1], (8, 10)),
        'strength': fmt(60, 45, [4, 4, 4, 3], (8, 12)),   # their four working sets, three in week 4
        'accessory': fmt(40, 20, [3, 3, 3, 3], (10, 15)),
    },
    'notes': {
        'Dumbbell Floor Press': 'Elbows touch the floor lightly each rep',
        'One-arm Row': 'Free hand on the block',
        'Dumbbell Overhead Press': 'Standing',
        'Dumbbell Pullover': 'Lying on the mat',
        'Bench Dip': 'On the block',
        'Dumbbell Curl': 'Palms up',
        'Bulgarian Split Squat': 'Back foot on the block',
        'Heel-Elevated Goblet Squat': 'Heels on a yoga block laid flat',
        'Banded Lateral Walk': 'Band above the knees or at the ankles',
        'Single-Leg Calf Raise': 'Off the edge of the block',
        'Bent-over Row': 'Two dumbbells',
        'Deficit Push-up': 'Hands on yoga blocks',
        'Dumbbell Skull Crusher': 'Lying on the mat',
        'Renegade Row': 'Or plank shoulder taps',
        'Hip Thrust': 'Upper back on the block, dumbbell on the hips, band above the knees',
        'Box Step-up': 'Or reverse lunges',
        'Banded Clamshell': 'Or fire hydrants',
        'Feet-Elevated Hamstring Bridge': 'Heels on the block, knees only slightly bent',
    },
    4: [
        ('Upper A — push', {'warm': ['Cat-Cow', 'Lat Wall Slide', 'Thread the Needle'],
                            'strength': ['Dumbbell Floor Press', 'One-arm Row'],
                            'accessory': ['Dumbbell Overhead Press', 'Dumbbell Pullover', 'Lateral Raise', 'Bench Dip', 'Dumbbell Curl', 'Reverse Crunch']}),
        ('Lower A — quads', {'warm': ['Cat-Cow', 'World\'s Greatest Stretch', 'Glute Bridge'],
                             'strength': ['Bulgarian Split Squat', 'Romanian Deadlift'],
                             'accessory': ['Heel-Elevated Goblet Squat', 'Dead Bug', 'Banded Lateral Walk', 'Single-Leg Calf Raise']}),
        ('Upper B — pull', {'warm': ['Cat-Cow', 'Lat Wall Slide', 'Thread the Needle'],
                            'strength': ['Bent-over Row', 'Arnold Press'],
                            'accessory': ['Deficit Push-up', 'Rear Delt Fly', 'Hammer Curl', 'Dumbbell Skull Crusher', 'Renegade Row']}),
        ('Lower B — glutes & hamstrings', {'warm': ['Cat-Cow', 'World\'s Greatest Stretch', '90/90 Hip Switch'],
                                           'strength': ['Hip Thrust', 'Staggered-Stance Romanian Deadlift'],
                                           'accessory': ['Box Step-up', 'Side Plank', 'Sumo Goblet Squat', 'Banded Clamshell', 'Feet-Elevated Hamstring Bridge']}),
    ],
}

QUESTS = [FOUNDATIONS, IRON_WILL, SECOND_WIND, FOURFOLD_PATH]
IMPLEMENTS = {'db': 'Dumbbell', 'kb': 'Kettlebell'}

# --- building ---------------------------------------------------------------------------------------


def slot(name, f, block, rounds_by_week=None, notes=None):
    """One timed slot. A rep move is paced (seconds + suggested reps); a one-sided move gets
    half the time per side, so it fills the same slot of the circuit."""
    on = f['on'] // 2 if one_sided(name) else f['on']
    s = {'exercise': name, 'seconds': on, 'restSeconds': f['off'], 'superset': block}
    if action(name) == 'reps':
        lo, hi = f['reps']
        if one_sided(name):
            lo, hi = max(4, lo // 2 + 1), max(5, hi // 2 + 1)
        s['repsMin'], s['repsMax'] = lo, hi
    if rounds_by_week:
        s['setsByWeek'] = rounds_by_week
    s['sets'] = (rounds_by_week or [3])[0]
    cue = (notes or {}).get(name) or CUES.get(name)
    if cue:
        s['notes'] = cue
    return s


class Picker:
    """Fills roles from the pools without repeating an exercise inside a day."""

    def __init__(self, imp):
        self.imp = imp

    def pick(self, role, used, turn=0):
        if role not in POOLS:  # a named exercise, used exactly as the recipe says
            if role not in LIBRARY:
                raise SystemExit(f'"{role}" is neither a role nor a library exercise')
            used.add(role)
            return role
        pool = [n for n in POOLS[role][self.imp] if n in LIBRARY]
        options = [n for n in pool[turn % len(pool):] + pool[:turn % len(pool)] if n not in used]
        if not options:
            raise SystemExit(f'pool "{role}" ({self.imp}) has nothing left for this day')
        used.add(options[0])
        return options[0]


def has_test(day_index, days_per_week):
    """ProgramDefinition.TestFor: strength on the first day, mobility on the second, conditioning on the last."""
    return day_index in (0, 1, days_per_week - 1)


def build_day(recipe, name, blocks, imp, day_index, days_per_week):
    f = recipe.get(f'formats_{days_per_week}', recipe['formats'])
    notes = recipe.get('notes')
    p = Picker(imp)
    used = set()
    main = []
    # Warm-up: one round, fixed for the block; the same moves stay mobility, not loaded work.
    for i, role in enumerate(blocks['warm']):
        main.append(slot(p.pick(role, used, day_index + i), f['warm'], 'W', f['warm']['rounds'], notes))
    # Main lifts: the pool's first choice on A days, its second on B days (4-day upper/lower).
    for role in blocks['strength']:
        main.append(slot(p.pick(role, used, day_index // 2), f['strength'], 'S', f['strength']['rounds'], notes))
    fixed = set(used)
    weeks = []
    for w in range(WEEKS):
        week_used = set(fixed)
        acc = [slot(p.pick(r, week_used, w + day_index + i), f['accessory'], 'A', [f['accessory']['rounds'][w]], notes)
               for i, r in enumerate(blocks['accessory'])]
        # Week 4's test days end with the stat test (the day's last block) instead of a finisher.
        fin = [] if w == WEEKS - 1 and has_test(day_index, days_per_week) else [
            slot(p.pick(r, week_used, w * 2 + day_index + i), f['finisher'], 'F', [f['finisher']['rounds'][w]], notes)
            for i, r in enumerate(blocks.get('finisher', []))]
        weeks.append(acc + fin)
    return {'name': name, 'main': main, 'accessories': weeks}


def day_seconds(day, week):
    """The app's timeline (GuidedPlan.Build + SetSeconds): circuits rest after every move."""
    def sets(s):
        by = s.get('setsByWeek')
        return by[min(week, len(by)) - 1] if by else s['sets']
    slots = day['main'] + day['accessories'][(week - 1) % len(day['accessories'])]
    steps = []
    i = 0
    while i < len(slots):
        block = [slots[i]]
        while i + len(block) < len(slots) and slots[i + len(block)]['superset'] == slots[i]['superset']:
            block.append(slots[i + len(block)])
        i += len(block)
        for r in range(1, max(sets(s) for s in block) + 1):
            for s in block:
                if sets(s) >= r:
                    work = (s['seconds'] + SETUP) * (2 if one_sided(s['exercise']) else 1)
                    steps.append(work + s['restSeconds'])
    total = sum(steps)
    last = slots[-1]
    return total - last['restSeconds']


def equipment_of(days):
    eq = set()
    for d in days:
        for s in d['main'] + [x for wk in d['accessories'] for x in wk]:
            eq.update(LIBRARY[s['exercise']].get('equipment', []))
    return sorted(eq)


def active_rest(quest, imp, days_per_week):
    """Carried over from the quest's previous version: active-rest days are unchanged."""
    prev = sorted(OUT.glob(f'{quest}-{imp}-{days_per_week}day.v*.json'), key=lambda p: int(p.stem.split('.v')[-1]))
    prev = [p for p in prev if int(p.stem.split('.v')[-1]) < 99]
    return json.loads(prev[-1].read_text())['activeRest'] if prev else None


def build(recipe, imp, days_per_week):
    days = [build_day(recipe, name, blocks, imp, i, days_per_week) for i, (name, blocks) in enumerate(recipe[days_per_week])]
    pid = f"{recipe['quest']}-{imp}-{days_per_week}day"
    minutes = [round(day_seconds(d, w) / 60) for d in days for w in range(1, WEEKS + 1)]
    program = {
        'id': pid, 'version': recipe['version'], 'name': recipe['name'], 'subtitle': recipe['subtitle'],
        'quest': recipe['quest'], 'discipline': recipe['discipline'], 'goal': recipe['goal'],
        'implement': IMPLEMENTS[imp], 'difficulty': recipe['difficulty'],
        'minutes': f'{min(minutes)}–{max(minutes)}',
        'description': recipe['description'],
        'equipment': equipment_of(days),
        'days': days,
    }
    rest = active_rest(recipe['quest'], imp, days_per_week)
    if rest:
        program['activeRest'] = rest
    return pid, program, days


def main():
    write = '--check' not in sys.argv
    bad = []
    files = []
    for recipe in QUESTS:
        for imp in recipe.get('implements', IMPLEMENTS):
            for n in recipe.get('schedules', (4, 3)):
                pid, program, days = build(recipe, imp, n)
                row = []
                for d in days:
                    mins = [day_seconds(d, w) / 60 for w in range(1, WEEKS + 1)]
                    row.append('/'.join(f'{m:.0f}' for m in mins))
                    # A week-4 test day is shorter on its own: the test fills the rest.
                    bad += [f'{pid} {d["name"]} week {w + 1}: {m:.1f} min' for w, m in enumerate(mins)
                            if not (TEST_DAY_MIN if w == WEEKS - 1 and has_test(days.index(d), n) else MIN_MINUTES) <= m <= MAX_MINUTES]
                print(f'{pid:<24} v{program["version"]}  ' + '  '.join(row))
                name = f'{pid}.v{program["version"]}.json'
                files.append(f'programs/{name}')
                if write:
                    (OUT / name).write_text(json.dumps(program, indent=2, ensure_ascii=False) + '\n')
    if bad:
        print('\nOutside 30-40 minutes:\n  ' + '\n  '.join(bad))
        sys.exit(1)
    print(f'\n{"wrote" if write else "checked"} {len(files)} programs')


if __name__ == '__main__':
    main()
