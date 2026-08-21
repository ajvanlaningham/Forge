# Baseline Test Protocols

**Protocol version: 1. Status: specified, not yet implemented (Epic 5 in [`TODO.md`](../TODO.md)).**

> **These protocols are frozen.** Movements, reps, order, and loads never change once a baseline
> is recorded. Improving a test destroys comparability with every prior result — a score is only
> meaningful against an identical earlier measurement. If a protocol genuinely must change,
> publish it as version 2 and start a new baseline; never edit version 1 in place.

## Why these tests

Three stats, each measuring something the daily quests actually train:

| Stat | Measures | Fed by |
|---|---|---|
| **STR** | Relative strength | Daily strength quest |
| **DEX** | Mobility and flexibility | Daily mobility quest |
| **CON** | Work capacity and endurance | Weekly conditioning goal |

Speed is deliberately absent. It is the quality most likely to improve as a byproduct of the
other three plus weight loss, and the one whose tests carry the most impact risk at a high
bodyweight. Revisit it as a fourth stat later.

## Cadence

- A **test window** opens on reaching every 4th level and stays open **7 days**.
- At 1050 XP per level and 21 quests per week, a level is one week of full adherence, so the
  window opens roughly monthly.
- The window contains **three separate sessions** — one per stat — placed on any days within it.
  An hour of maximal work in one session is both miserable and bad measurement: by the AMRAP you
  would be testing recovery from the strength work, not work capacity.
- Space strength and conditioning a couple of days apart. Mobility is low-fatigue and pairs with
  anything.
- **Each completed test session grants XP equal to a quest**, so testing never costs consistency
  or breaks a streak.
- An unfinished window closes without penalty; stats keep their prior scores. The next window
  opens at the next multiple of 4 regardless.

## Establishing the first baseline

Run the first baseline **twice, roughly a week apart, and keep the second result.**

First attempts at an unfamiliar max-effort test are almost always underestimates — pacing is
wrong, and people stop before they are actually done. A falsely low baseline then inflates every
score earned afterward, permanently. One extra session buys years of trustworthy data.

After calibration, baselines are immutable except through an explicit reset that warns about
losing comparability.

---

## STR — relative strength

Fixed load, max clean reps, converted to an estimated 1RM.

True 1RM testing is deliberately avoided: it is the least necessary risk in the programme, and
it is hard to do safely alone.

| Lift | Pattern |
|---|---|
| Goblet squat | Lower |
| Dumbbell floor press | Upper push |
| Dumbbell row | Upper pull |

**Protocol:** one all-out set per lift at a fixed load, full recovery between lifts. Record load
and reps. Loads are chosen at the first baseline and **reused unchanged at every retest**.

**Pull-ups are deliberately not the pull test.** At a high bodyweight the honest result is likely
zero, and a metric that reads zero for months is a discouragement, not a measurement. Rows scale
from day one. Pull-ups belong in the app as a tracked milestone instead.

**Metric:**

```
e1RM  = weight × (1 + reps / 30)          # Epley; accurate to roughly 10 reps
score input = mean(e1RM per lift) / bodyweight
```

Bodyweight is captured **at test time** and stored with the result, since the metric depends on it.

Relative rather than absolute is the scored value, so a month spent holding lifts steady while
losing weight reads as the progress it is. Absolute e1RM is displayed alongside.

## DEX — mobility

**Protocol:** deep squat hold, measured in seconds, to the point form breaks. Optionally a
sit-and-reach distance as a second measure.

**Metric:** hold time in seconds.

## CON — work capacity

**Protocol:** 10-minute AMRAP of a fixed circuit.

**Metric: total reps completed, not rounds.**

This matters more than it looks. Rounds are a coarse count — going from 2 rounds to 3 reads as a
50% improvement and would swamp the scale. Counting reps, including a partial final round, gives
resolution of one rep instead of one round.

Size the circuit to yield roughly **6–10 rounds**, so a round is a small unit of work rather than
an event. A round of about 60–75 seconds is the target. A starting shape, to be fixed at the first
baseline and frozen thereafter:

- 8 air squats
- 6 incline push-ups
- 8 kettlebell swings

A 10-minute bike distance test is the lower-variance alternative — an AMRAP score moves partly
because you got stronger, not only fitter. The circuit is preferred anyway, on the grounds that a
test you will actually do beats a more precise one you skip.

---

## Scoring

All three stats use one formula:

```
score = 10 + 25 × log2(current / baseline)
```

- Clamped to **1–100**.
- Baseline scores **10**, leaving room to move down as well as up. Detraining is real information.
- **Doubling any metric is +25 points, regardless of starting value.** Going from 2 rounds to 4 is
  genuinely easier than 8 to 16; a linear percentage scale pretends otherwise. Diminishing returns
  fall out of the maths rather than being bolted on.
- **A single retest may not move a score by more than +10.** A backstop for the early tests, where
  gains are legitimately enormous.
- Reaching 100 takes years. That is correct for a lifetime-scale number.

This replaces the original linear `ScoreFrom` — +1 point per 10% improvement from a floor of 1 —
under which reaching 100 required roughly a 990% improvement, making the top 85% of the scale
unreachable. See PBI 5.1.
