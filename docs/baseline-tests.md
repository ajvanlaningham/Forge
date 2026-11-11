# Stat Test Protocols

**Protocol version: 2 (approved 2026-11-11). Status: specified, not yet implemented (Epic 5 in
[`TODO.md`](../TODO.md)).** Version 1 (below the line at the end) was never used and is superseded.

> **Protocols are frozen once a baseline is recorded.** Movements, order and conditions never
> change under a version; a genuine change ships as a new version with a new baseline. Within v2,
> each user's choices made at baseline (test lifts, step height, push-up incline) are frozen too.

## What changed since v1

- **Tests live in Honing (week 4) of every quest**, already slotted by `ProgramDefinition.TestFor`:
  strength on the first training day, mobility on the second, conditioning on the last. That
  replaces v1's "test window every 4th level". One test of each kind per block, roughly monthly.
- **Gear varies by hero and by home.** v1's "fixed load, frozen forever" breaks as soon as you get
  strong enough to rep the load past ~10 (estimates degrade there), and a kettlebell user has no
  dumbbell row. Strength needs a load rule, not a frozen load.
- **The research is mixed on two v1 tests** (below): a deep-squat *hold* measures comfort more than
  range, and a 10-minute AMRAP circuit moves with strength and pacing as much as with fitness.

## What the research says (short)

| Quality | Test | Evidence | Home-friendly? |
|---|---|---|---|
| Strength | Reps to failure → estimated 1RM | Epley/Brzycki agree within ~2–3% under 10 reps; worse above 10 and for lower-body lifts | Yes, with any owned weight |
| Strength / endurance | Push-ups to failure | Push-up capacity tracked CVD events in 1,104 men over 10 years (Yang et al., JAMA Netw Open 2019); observational | Yes, no gear |
| Conditioning | 3-minute step test, 1-min recovery heart rate | YMCA model predicts peak VO2 (R ≈ 0.83) *if completed per protocol*; submaximal, safe at high bodyweight; best used against your own earlier result | Yes: a step, the app as metronome, a pulse count |
| Conditioning | Rockport 1-mile walk | Classic submax test; general equation not validated for obese adults | Needs a measured mile and HR |
| Mobility | Knee-to-wall (ankle dorsiflexion, cm) | Excellent intra-rater reliability; new 2026 norms for 899 adults | Yes: a wall and a tape measure |
| Mobility | Back-saver sit-and-reach (cm) | Very high reliability (ICC 0.89–0.98); moderate validity for hamstrings | Yes: a box or step and a ruler |
| Mobility | Shoulder reach (fist gap, cm) | Simple; reliability not well studied; shouldn't be read alone | Yes: a tape measure |
| Mobility | Deep-squat hold | Low convergent validity for joint mobility (2024) | Yes, but weak |
| Whole body | Sitting-rising test (0–10) | Predicted mortality in 2,002 adults 51–80 (Araújo 2012); observational; coarse scale with a ceiling | Yes, no gear |
| Grip / hang | Dead hang time | No validated adult norms; fine as a personal benchmark | Yes, with a bar |

Common thread: for tracking one person over time, **identical conditions matter more than
absolute accuracy**. Scoring stays relative to your own baseline (Decision 5), so a consistent
protocol is what counts.

## Proposal

### STR: strength (Honing, training day 1)

- **Three patterns, one lift each, chosen once from your gear and then kept:** a squat or lunge, a
  press, a row. Dumbbell: goblet-style squat or split squat, floor press, bent-over row.
  Kettlebell: front squat, press, row. Gym: the barbell or dumbbell version of each. Changing a
  test lift starts a new baseline for that lift.
- **Load rule instead of a frozen load:** each test uses the heaviest weight you own that you can
  lift **3–10 times** (suggested from your training history). The estimate is load-independent,
  so moving up a weight doesn't break comparability.
- **One all-out set per lift**, after the session's warm-ups, stopping when form breaks. Logged as
  load × reps → e1RM (Epley). In the guided session this is the one set that asks "How many?"
  instead of logging the target.
- **Score input:** mean e1RM ÷ bodyweight at test time (Decision 6), absolute e1RM shown alongside.
- **Push-ups to failure** are recorded alongside (no gear, strong evidence) as a fourth STR
  measure, at the same incline each time (floor, or a recorded box height).

### DEX: mobility (Honing, training day 2)

Three measures with a tape measure, each side where it applies, best of two tries:

1. **Knee-to-wall** (cm, ankle): the strongest-evidence home mobility test.
2. **Back-saver sit-and-reach** (cm, hamstrings and low back), using a step or box and a ruler.
3. **Shoulder reach** (cm gap between fists behind the back; smaller is better).

Score input: the mean of the three measures' log ratios against baseline (gaps are inverted so
smaller = better). The **deep-squat hold** drops out as a test (weak validity) and stays in active
rest as an exercise. The **sitting-rising test** becomes a milestone (0–10, shown on Hero), not a
stat input, because its ceiling makes it useless for a log scale.

### CON: conditioning (Honing, last training day)

- **3-minute step test**: the app plays the 96-beat metronome (up-up-down-down, 24 steps a
  minute), then times a 1-minute pulse count starting 5 seconds after you stop. Lower recovery
  heart rate = fitter, so the score input is baseline ÷ current. Submaximal, so safe at any
  bodyweight. The step height is recorded at baseline and reused.
- **A bike or circuit test as a second measure** for those with the gear: 10 minutes for distance
  on the same bike, or v1's AMRAP circuit counted in total reps. Shown but not scored, until
  there's a heart-rate strap (Epic 7) to make the step test more precise.

### Baseline and calibration

- **First quest, week 1:** a short calibration run of each test, not scored. First attempts
  underestimate (pacing, unfamiliarity), and a low baseline would inflate every later score.
- **First quest, week 4 (Honing):** the baseline. Every later Honing week updates the scores.
- Scoring unchanged from Decision 5: `10 + 25 × log2(current / baseline)`, clamped 1–100, at most
  +10 per test.
- Tests are part of the training day, so they need no extra XP rule; the day's XP covers them.
- Never medical advice: the app reports your own trend, not health risk. No norms on the main
  screens; an optional "how this compares" line can come later.

## Decisions (2026-11-11)

1. **Protocol v1 is replaced by this v2** (nothing was ever recorded under v1). **Scores stay
   relative to yourself** (Decision 5): the stat score measures improvement from your own
   baseline. Population norms appear only as an **optional, coarse band** ("about average for
   men 40–49, ACSM") behind a tap, on tests with credible tables (push-ups, sit-and-reach,
   knee-to-wall, the step test once one source is chosen), and only if age and sex are entered.
   No health claims. The Guild, when it comes, compares **progress** (points gained), not
   percentiles.
2. **Push-ups count toward STR**, alongside the three lifts.
3. **CON is the step test with a hand-counted pulse** until there's a heart-rate strap. A known
   resting heart rate may be typed in by hand (it gives the recovery result context and feeds
   the future RHR trend card).
4. **Calibration run in week 1 of the first quest: yes.**

## Sources

- Yang J. et al., push-up capacity and CVD events in men, JAMA Network Open (2019):
  [Harvard Chan summary](https://hsph.harvard.edu/news/push-up-capacity-cardiovascular-disease-events-men),
  [Cooper Institute](https://www.cooperinstitute.org/blog/does-your-ability-to-do-pushups-predict-cardiovascular-risk)
- Step-test validity: [ESC 365, brief step-test protocol for peak VO2](https://esc365.escardio.org/journal/30047);
  [Texas State lab protocol](https://hper-server.its.txstate.edu/hper/faculty/pankey/3317/4312/lab/lab3.html);
  [Topend Sports, Queens College step test](https://ipv6.topendsports.com/testing/tests/step-queens.htm)
- 1RM prediction: [weight-dependent 1RM equation, arXiv 2026](https://arxiv.org/pdf/2603.17495);
  [J. Human Kinetics study](https://johk.pl/wp-content/uploads/2023/04/10078-82-2022-v82-2022-18.pdf)
- Knee-to-wall: [international WBLT norms, 899 adults (PubMed)](https://pubmed.ncbi.nlm.nih.gov/41723909/)
- Deep squat validity: [Thieme 2024](https://www.thieme-connect.de/products/ejournals/html/10.1055/a-1708-9735?lang=en);
  [PMC4325286](https://pmc.ncbi.nlm.nih.gov/articles/PMC4325286)
- Sit-and-reach: [Hui & Yuen 2000 (PubMed)](https://pubmed.ncbi.nlm.nih.gov/10994920/);
  [Baltaci et al. 2003 (PMC)](https://pmc.ncbi.nlm.nih.gov/articles/PMC1724584)
- Shoulder mobility test: [Sprague et al. (PMC)](https://pmc.ncbi.nlm.nih.gov/articles/PMC4196330)
- Sitting-rising test: [Araújo et al. 2012 (PubMed)](https://www.pubmed.gov/23242910/)
- Plank reliability: [prone bridge test, Bohannon et al.](https://dev.nlk.cz/mdv/doc/pmid29861239?lang=en)
- Rockport walk: [Topend Sports](https://www.topendsports.com/testing/tests/rockport.htm);
  [Military Medicine validation](https://academic.oup.com/milmed/article-pdf/178/7/753/23663597/milmed-d-12-00369.pdf)
- Grip and hang norms: [Topend Sports handgrip norms](https://www.topendsports.com/testing/norms/handgrip.htm)
- ACSM field tests (secondary): [PhenX integrated fitness protocol](https://test.phenxtoolkit.org/protocols/view/150201)


---

<details><summary>Protocol version 1 (superseded, never used)</summary>


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

</details>
