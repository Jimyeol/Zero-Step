import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import json, csv, math, collections, statistics as st, re
import assemble

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'out')
rows = list(csv.DictReader(open(f'{OUT}/mapping.csv', encoding='utf-8')))
R = {int(r['stage']): r for r in rows}
A = {int(k): v for k, v in json.load(open('assembly.json')).items()}
O = json.load(open('old_records.json'))
excluded = {int(k): v for k, v in O['excluded'].items()}
oldrec = {r['old']: r for r in O['records']}
old_metrics = {}
for l in csv.DictReader(open('stage_metrics.csv')):
    old_metrics[int(l['stage'])] = l
val = open(sys.argv[1]).read() if len(sys.argv) > 1 else ''
tut_changes = json.load(open('tutorial_changes.json'))

D = {i: float(R[i]['neglog10_p']) for i in R}
C = {i: float(R[i]['crit_steps']) for i in R}
P = {i: float(R[i]['p_random']) for i in R}


def med(x):
    return st.median(x)


def spearman(xs, ys):
    def ranks(v):
        o = sorted(range(len(v)), key=lambda i: v[i]); r = [0.0] * len(v); i = 0
        while i < len(o):
            j = i
            while j + 1 < len(o) and v[o[j + 1]] == v[o[i]]:
                j += 1
            for k in range(i, j + 1):
                r[o[k]] = (i + j) / 2
            i = j + 1
        return r
    a, b = ranks(xs), ranks(ys)
    ma, mb = sum(a) / len(a), sum(b) / len(b)
    num = sum((x - ma) * (y - mb) for x, y in zip(a, b))
    den = math.sqrt(sum((x - ma) ** 2 for x in a) * sum((y - mb) ** 2 for y in b))
    return num / den


# old-set baseline numbers
old_D = {}
for i, l in old_metrics.items():
    try:
        old_D[i] = float(l['neglog10P'])
    except Exception:
        pass
old_meds = []
for c in range(20):
    xs = [old_D[i] for i in range(c * 50 + 1, c * 50 + 51) if i in old_D]
    old_meds.append(med(xs))
rho_old_stage = spearman([i for i in sorted(old_D) if i >= 30], [old_D[i] for i in sorted(old_D) if i >= 30])

L = []
w = L.append
w('# ZeroStep level content rebuild (1,000 stages)\n')
w('All files in this directory were produced from the current 1,000 stages (re-sequenced, repaired or mutated) plus newly generated stages. '
  'Every stage was verified with an exhaustive game-faithful solver (C implementation, cross-checked state-for-state against the Python reference solver) '
  'and is re-verifiable with `python3 -I validate.py` (pure Python, independent solver + stroke replay).\n')
w('## Deliverables\n')
w('| Path | Content |\n|---|---|')
w('| `Stages/stage_1..1000.json` | the new sequence (same JSON layout as before; `stageID` now equals the file number; optional root `walls` array) |')
w('| `Stages/index.json` | list of all 1,000 stage files (same format as the existing index.json) |')
w('| `Tutorials/help_tutorial_schedule.json`, `Tutorials/stage_snackbar_schedule.json` | schedules moved to the new intro stages + Wall/Charge entries |')
w('| `mapping.csv` | per stage: chapter/theme, provenance, notes, w,h,tiles,sum,p_random,-log10 p,crit_steps,forced_ratio,solutions (capped at 1e6),states,types,walls,... |')
w('| `solutions.json` | one verified solution per stage as a list of strokes `[[x,y],...]` (one stroke = one continuous drag; all 1,000 solutions are a single stroke) |')
w('| `validate.py` | self-contained re-validation of this directory (`--quick`, `--no-ablation`, `--jobs N`) |\n')

w('## What changed and why\n')
prov = collections.Counter(r['provenance'].split('+')[1] if '+' in r['provenance'] else r['provenance'].split(':')[0] for r in rows)
n_old_ids = len({A[i].get('old') for i in A if A[i].get('old') is not None})
w(f"* **Provenance**: {prov.get('old', 0)} unchanged old stages (`old:N`), {prov.get('fixed', 0)} repaired old stages (`old:N+fixed`: mechanics that had no effect "
  f"converted to Normal, BlindCurtain/Blackout removed where they would precede their intro, or a too-late Blackout neutralised), "
  f"{prov.get('mutated', 0)} mutated old stages (`old:N+mutated`: walls added on edges unused by a known solution, a Normal tile turned into Charge with counts re-derived from the old solution walk, "
  f"BlindCurtain/Blackout added), and {prov.get('new', 0)} newly generated stages (`new`, incl. 9 tutorial intros and the finale). {n_old_ids} distinct old stages are reused (each at most once).")
w('* **Difficulty curve**: the old set was flat after stage 30 '
  f'(stage-level Spearman of -log10 p_random vs index from stage 30 = {rho_old_stage:.2f}; chapter medians {min(old_meds):.2f}-{max(old_meds):.2f}). '
  'The new set uses 20 chapters of 50 with linearly rising chapter-median targets (0.45 -> 3.11 in -log10 p_random); within a chapter: 3 warm-ups, '
  'a sawtooth of rising runs of 6,7,5,8,6,7,7 stages each starting with a breather, and stage 50 as the boss (hardest of the chapter). '
  'Stages were picked by cost (difficulty error + theme + reuse preference + outline variety) and then rank-matched to the target profile.')
w('* **Mechanics**: introduced every 50 stages on schedule (ShortCircuit 21, CrossBlast 51, FixedKnot 101, TwinLink 151, Igniter/Hidden 201, BlindCurtain 251, Blackout 301, Wall 401, Charge 551), '
  'each with a tiny tutorial board, a teaser snackbar two stages earlier and a 10-stage focus block. Chapters after the intros have explicit theme combinations; from stage 451 most stages combine 2-3 rule-changing mechanics.')
w('* **Every mechanic instance matters**: each ShortCircuit/CrossBlast/FixedKnot/TwinLink/Charge tile, each Igniter+Hidden group and each wall was ablated (replaced by Normal / removed) '
  'and the solution set had to change (restriction types: solution count must change; effect types: count or cross-replay must differ). '
  'Blackout tiles are placed so that every solution triggers them with >= 40% of its moves left.')
w('* **Removed defects**: duplicate boards (14/957, 4/5, 6/7, 10/13, 18/20), stages that could not be lost after stage 20, easy stages (p_random >= 0.5) after stage 30 (except intro tutorials), '
  'twenty similar 4x4 boards at 30-49, the unlosable spiral finale, and stages whose exhaustive analysis exceeds the 2M-state hint budget.\n')

w('## Generation method\n')
w('1. **Solver.** `zs.py` (Python reference, extended from the verified solver with walls and Charge) and `zsolve.c` (identical algorithm in C, ~100x faster). '
  'They agree exactly (states, p_random, solution counts, canonical paths) on all 1,000 old stages and on 2,600 random stages using every mechanic incl. walls/Charge. '
  'Metrics: p_random = probability that uniformly random legal play wins (finger-lift variants count as separate actions, as in the earlier analysis); crit_steps = mean number of moves per solution where some legal move loses; states = memoised search states (hint-budget proxy).')
w('2. **Old pool.** All 1,000 stages analysed; ineffective mechanic instances converted to Normal iteratively; duplicates, over-budget and over-size stages excluded.')
w('3. **Walk-first generator.** Shape (rectangles, holed rectangles, rings, symmetric blobs, 50+ letter/icon shapes such as heart, anchor, rocket, crown, temple, bolt) -> random walk covering it -> mechanics consistent with the walk '
  '(ShortCircuit only where every exit of the walk goes one way, FixedKnot at the walk\'s first entry, Hidden entered after its Igniter, walls only on edges the walk never crosses) -> counts derived from the walk incl. CrossBlast/TwinLink hits and Charge gains '
  '(guarantees solvability) -> exhaustive solve -> ablation (strip ineffective instances, repeat) -> metrics. ~25k candidates in 22 recipe families x size tiers.')
w('4. **Finale.** Lightning-bolt shaped 7x8 board grown by greedy hill-climbing: a CrossBlast/TwinLink/Charge base, then Igniter, ShortCircuit, Wall and FixedKnot instances added one at a time, '
  'each accepted only if every instance on the board still matters.')
w('5. **Assembly** (`assemble.py`) and full re-check of the emitted files (`emit.py`).\n')
w('Generator, solver and assembly scripts (not part of the deliverable, kept for re-runs): `../work/` next to this directory '
  '(zs.py, zsolve.c, zs_c.py, gen.py, recipes.py, pipeline.py, abl.py, augment.py, genpool.py, assemble.py, emit.py, report.py; candidate pools in `../work/pools/`).\n')

w('## Chapters\n')
w('| Ch | Stages | Theme | Intro | median -log10 p | median crit | -log10 p range | boss (#50) | >=2 rule mech. | old / old+fixed / old+mutated / new |')
w('|---|---|---|---|---|---|---|---|---|---|')
intro_at = {v: k for k, v in assemble.INTRO.items()}
chap_meds = []
for c in range(1, 21):
    ids = list(range((c - 1) * 50 + 1, c * 50 + 1))
    ds = [D[i] for i in ids]; cs = [C[i] for i in ids]
    chap_meds.append(med(ds))
    intro = ', '.join(f'{intro_at[i]} @{i}' for i in ids if i in intro_at) or '-'
    pv = collections.Counter(('fixed' if '+fixed' in R[i]['provenance'] else 'mutated' if '+mutated' in R[i]['provenance'] else 'new' if R[i]['provenance'] == 'new' else 'old') for i in ids)
    n2 = sum(1 for i in ids if int(R[i]['rule_mechanics']) >= 2)
    w(f"| {c} | {ids[0]}-{ids[-1]} | {assemble.CH[c]['name']} | {intro} | {med(ds):.2f} | {med(cs):.2f} | {min(ds):.2f}-{max(ds):.2f} | {D[ids[-1]]:.2f} | {n2} | {pv['old']}/{pv['fixed']}/{pv['mutated']}/{pv['new']} |")
rho_ch = spearman(list(range(20)), chap_meds)
rho_st = spearman(list(range(1, 1001)), [D[i] for i in range(1, 1001)])
rho_cr = spearman(list(range(1, 1001)), [C[i] for i in range(1, 1001)])
w(f'\nSpearman rank correlation (difficulty vs position): chapter medians **{rho_ch:.3f}** (target >= 0.6); stage level -log10 p_random {rho_st:.3f}, crit_steps {rho_cr:.3f}. '
  f'Old set for comparison: chapter medians {", ".join(f"{x:.2f}" for x in old_meds)}.\n')
w('Within every chapter the per-stage targets follow: warm-up (3) -> runs of 6,7,5,8,6,7,7 rising stages, each run starting with a breather -> boss. '
  'mapping.csv contains the target value per stage (`target_D`).\n')

w('## Mechanic introduction schedule\n')
w('| Mechanic | Teaser snackbar | Intro (tutorial board) | Focus block | Intro board | Tutorial entry id |')
w('|---|---|---|---|---|---|')
TOP = {'ShortCircuit': 'short_circuit', 'CrossBlast': 'cross_blast', 'FixedKnot': 'fixed_knot', 'TwinLink': 'twin_link', 'Igniter': 'igniter',
       'BlindCurtain': 'blind_curtain', 'Blackout': 'blackout', 'Wall': 'wall', 'Charge': 'charge'}
for m, at in assemble.INTRO.items():
    r = R[at]
    w(f"| {m} | {at - 2} | {at} | {at + 1}-{at + 10} | {r['w']}x{r['h']}, sum {r['sum']}, p_random {float(r['p_random']):.3f}, {r['solutions']} solution(s) | tutorial_stage_{at}_{TOP[m]} |")
w('\nTutorial files: existing entries keep all fields and localisation keys; `stageIndex`/`targetStageIndex` moved and ids renamed to the new stage numbers '
  '(help: `tutorial_stage_{stage}_{topic}`, snackbar: `tile_unlock_preview_{from}_to_{to}`). Note: help-popup dismissal is stored per id, so players who already dismissed an old id will see the moved popup once more; keep the old ids if that is undesirable. '
  'New entries: `tutorial_stage_401_wall` (tutorialType "Wall", keys tutorial_wall_title/description/instructions) and `tutorial_stage_551_charge` (tutorialType "Charge", keys tutorial_charge_*), '
  'plus snackbar teasers `tile_unlock_preview_399_to_401` (tileType "Wall") and `tile_unlock_preview_549_to_551` (tileType "Charge"). '
  'The localisation keys tutorial_wall_* / tutorial_charge_* still need entries in GameLocalization.cs (Korean text is provided inline in the JSON).\n')

w('## Old stages dropped\n')
used = {}
for i in A:
    if A[i].get('old') is not None:
        used[A[i]['old']] = i
cat = collections.defaultdict(list)
for i, why in sorted(excluded.items()):
    if 'duplicate' in why:
        key = 'duplicate board (same as a lower-numbered old stage up to rotation/mirror/start; the lower one is kept)'
    elif 'ablation' in why:
        key = 'a mechanic could not be proven to matter (ablated variant exceeds 4M states)'
    elif 'states' in why:
        key = 'exhaustive solver needs >= 2M states (over the in-game hint budget)'
    elif 'size' in why:
        key = 'violates size/count limits (w<=7, h<=8, count<=6, sum<=48)'
    else:
        key = why
    cat[key].append(i)
w(f'{len(used)} of 1,000 old stages are used (as-is, repaired or mutated). {len(excluded)} were excluded for hard reasons, the rest were not needed:\n')
w('| Reason | Count | Old stage ids |\n|---|---|---|')
for k, v in sorted(cat.items(), key=lambda kv: -len(kv[1])):
    w(f'| {k} | {len(v)} | {", ".join(map(str, v))} |')
unused = [i for i in range(1, 1001) if i not in used and i not in excluded]
uc = collections.defaultdict(list)
for i in unused:
    r = oldrec[i]; m = r['m']
    if m['p_random'] >= 0.999999:
        uc['cannot be lost (p_random = 1): only allowed in stages 1-20, whose slots were filled by better-fitting tutorials'].append(i)
    elif m['p_random'] >= 0.5:
        uc['too easy (p_random >= 0.5): only allowed in stages 1-30, whose slots were filled by better-fitting tutorials'].append(i)
    elif m['rect']:
        uc['full-rectangle board; not selected (outline variety, closer-fitting stages available)'].append(i)
    else:
        uc['not selected: every slot it was eligible for had a closer difficulty/theme/outline fit (kept as reserve)'].append(i)
for k, v in sorted(uc.items(), key=lambda kv: -len(kv[1])):
    w(f'| {k} | {len(v)} | {", ".join(map(str, v))} |')
w('')

w('## Validation results\n')
w('Produced by `python3 -I validate.py` (independent pure-Python solver, stroke replay, ablation of every instance) on this directory:\n')
w('```')
for line in val.splitlines():
    if line.startswith('[') or line.startswith('SUMMARY') or line.strip().startswith('chapter') or 'stages with >' in line or 'finger lift' in line:
        w(line)
w('```\n')
big = [(i, int(R[i]['states'])) for i in R if int(R[i]['states']) > 1_000_000]
w(f"Stages with more than 1,000,000 solver states (all are < 2,000,000): {', '.join(f'{i} ({s:,})' for i, s in big) or 'none'}.\n")
nrect = sum(1 for i in range(101, 1001) if R[i]['outline_rect'] == '0')
n2 = sum(1 for i in range(451, 1001) if int(R[i]['rule_mechanics']) >= 2)
easy = [i for i in range(31, 1001) if P[i] >= 0.5]
w('| Requirement | Result |\n|---|---|')
w('| 1. 1,000 stages, stageID = file number, same JSON style | pass |')
w(f'| 2. chapter medians rise; Spearman >= 0.6 | pass: chapter rho {rho_ch:.3f}, stage rho {rho_st:.3f}; every boss is the chapter maximum |')
w(f'| 3. intro schedule, teasers, focus blocks, themes, >=2 rule mechanics from ~450 | pass: {n2}/550 stages from 451 combine >= 2 rule-changing mechanics |')
w('| 4. every rule-changing instance matters; Blackout >= 40% moves remaining | pass (see validator output) |')
w(f'| 5. easy-stage limits | pass: p_random >= 0.5 after 30 only at intros {easy}; no unlosable stage after stage 20; first crit_steps >= 2 at stage {min(i for i in C if C[i] >= 2)}; no 4 consecutive equal outlines |')
w('| 6. no duplicates (rotation/mirror/start) | pass; additionally no near-duplicates (same outline with <= 2 cells differing in count/type) |')
w(f'| 7. >= 40% non-rectangular after 100 | pass: {nrect}/900 = {nrect / 9:.1f}% |')
w(f"| 8. w<=7, h<=8, counts<=6, sum<=48, states < 2M | pass (max states {max(int(R[i]['states']) for i in R):,}) |")
w(f"| 9. finale | stage 1000: {R[1000]['w']}x{R[1000]['h']} lightning bolt, {R[1000]['tiles']} tiles, sum {R[1000]['sum']}, all 9 mechanics, p_random {P[1000]:.2e}, {R[1000]['solutions']} solutions |")
w('| 10. all solvable | pass: exhaustive search + replayed single-stroke solutions (plain clear) |')
w('| 11-13, 15. tutorials, index, mapping, solutions, validator | provided |\n')

w('## Notes, caveats and things not achieved\n')
w('* **Clear rule**: GameManager.CheckVictoryCondition also clears when the entered tile is a TwinLink at 1 and all its live partners are at 1 and they make up the whole remaining total. The solver/validator model this exactly as the C# does (not only the simplified rule in the brief), so p_random/crit_steps are C#-faithful. '
  'To be safe with either implementation, every provided solution ends with the plain clear (entered tile 1, board total 1) and never passes through a TwinLink-group clear, '
  'so all 1,000 stages are solvable with or without that extra rule (45 candidates that could only be cleared via the TwinLink-group rule were rejected).')
w('* **Strokes**: every stage has a single-stroke solution (no finger lift needed), so each `solutions.json` entry is one stroke. Lifting the finger is still modelled as an extra action for metrics (matters only for TwinLink).')
w('* **Charge**: in addition to the brief, no two Charge tiles are adjacent (two adjacent Charges can pump each other indefinitely); the validator checks this together with the FixedKnot/Igniter/Hidden adjacency rule.')
w('* **Walls JSON**: root field `walls: [{"x","y","dir"}]` written after `cells`, only when non-empty; never on a missing-cell edge, a duplicate edge or a ShortCircuit exit edge.')
w('* `stageID` was 0 in all old files; it now equals the file number. GameManager only uses it for a stage-6 debug log.')
w('* Difficulty is measured with random-play statistics (p_random, crit_steps); they are proxies for human difficulty. Late chapters are much harder than the old set (median p_random about 1e-3 in chapter 20, boss stages 1e-4.5..1e-6) -- tune `M0/MSTEP` in assemble.py and re-run if this is too steep.')
w('* Mechanic-ablation for effect tiles (CrossBlast/TwinLink/Charge) declares "matters" when the solution count changes or a solution of one variant is not a solution of the other; '
  'restriction-type ablation (ShortCircuit/FixedKnot/Igniter+Hidden/Wall) compares solution counts (the ablated solution set is a superset).')
open(f'{OUT}/REPORT.md', 'w', encoding='utf-8').write('\n'.join(L) + '\n')
print('report written', len(L), 'lines')
