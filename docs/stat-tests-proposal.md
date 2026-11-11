# Stat tests: proposal (2026-11-11)

**Status: approved in part (2026-11-11); question 1 open.** Revises the unimplemented protocol v1 in
[`baseline-tests.md`](baseline-tests.md) now that tests live inside the quest. Nothing has been
recorded under v1, so v1 can be replaced without breaking any baseline.

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

1. Replace protocol v1, and **self vs. norms**: open (see the discussion of percentiles).
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
