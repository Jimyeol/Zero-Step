"""Upgrade pack: breather stages (one per 5 from 301) and two-phase Portal stages (30, from 451).

usage: python3 -I Tools/StagePipeline/generator/upgrade_pack.py [--seed N] [--dry-run]

Works on the stage set currently in the project (Assets/Resources/Stages + solutions.txt + mapping.csv)
and refuses to run twice (Tools/StagePipeline/roles.json marks an applied pack; restore the stage files
with git to rerun). For every 5-stage block from 301:
  * a block holding a Portal index gets a new two-phase Portal stage there;
  * any other block gets a breather on its middle slot: an existing easy stage of the block is moved there
    if it has one, otherwise the hardest stage of the block is removed and a new easy board takes the slot.
Protected stages keep their number: intros (301, 401), chapter warm-ups (c*50+1..3), bosses (c*50), 1000.
Other stages of a block keep their order. New boards are unique against every existing board.
Writes stage JSON, solutions.txt ("N:2|..." for phase 2), mapping.csv, roles.json and the Portal
tutorial/snackbar entries.
"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
PIPE = os.path.abspath(os.path.join(HERE, '..'))
sys.path.insert(0, HERE)
sys.path.insert(0, PIPE)
import json, csv, math, random, collections, copy, argparse, statistics, multiprocessing as mp
import gen, recipes, pipeline, zs, zs_c
import validate as V

PROJECT = os.path.abspath(os.path.join(PIPE, '..', '..'))
STAGES = os.path.join(PROJECT, 'Assets', 'Resources', 'Stages')
TUTORIALS = os.path.join(PROJECT, 'Assets', 'Resources', 'Tutorials')
MAPPING = os.path.join(PIPE, 'mapping.csv')
ROLES = os.path.join(PIPE, 'roles.json')

PORTALS = [451, 458, 465] + list(range(485, 986, 20)) + [995]
FIRST_BLOCK, LAST_BLOCK = 301, 991          # blocks 301-305 ... 991-995 (996-1000 is the finale run-up)
BREATHER_MAX_D = 0.8                        # -log10 p <= 0.8  (random play wins >= 16%)
BREATHER_MIN_D = 0.302                      # p < 0.5 keeps them out of the "easy stage" spacing rule
INTRO_AT = {'SC': 21, 'CB': 51, 'FK': 101, 'TL': 151, 'IGN': 201, 'BC': 251, 'BO': 301, 'CHG': 401}
MECH_TYPE = {'SC': 'ShortCircuit', 'CB': 'CrossBlast', 'FK': 'FixedKnot', 'TL': 'TwinLink', 'IGN': 'Igniter',
             'BC': 'BlindCurtain', 'BO': 'Blackout', 'CHG': 'Charge'}
FOCUS = {'BO': range(302, 312), 'CHG': range(402, 412)}   # 10 stages after an intro must show its mechanic
CFG = {"mode": "Normal", "difficulty": "Normal", "spotlightRadius": 2.5, "showGridLines": False}


# ------------------------------------------------------------------------------------------- io
def ordered(i, s):
    o = collections.OrderedDict()
    o['stageID'] = i
    o['width'] = s['width']; o['height'] = s['height']
    o['startPoint'] = collections.OrderedDict([('x', s['startPoint']['x']), ('y', s['startPoint']['y'])])
    o['config'] = s.get('config') or copy.deepcopy(CFG)
    o['cells'] = s['cells']
    if s.get('phase2'):
        p = s['phase2']
        o['phase2'] = collections.OrderedDict([('width', p['width']), ('height', p['height']),
                                               ('startPoint', collections.OrderedDict([('x', p['startPoint']['x']), ('y', p['startPoint']['y'])])),
                                               ('cells', p['cells'])])
    return o


def load_solutions():
    sols = {}
    for line in open(os.path.join(STAGES, 'solutions.txt'), encoding='utf-8'):
        line = line.strip()
        if line:
            parts = line.split('|')
            sols[parts[0]] = [[[int(v) for v in cell.split(',')] for cell in st.split(';')] for st in parts[1:]]
    return sols


def fmt_solution(key, strokes):
    return key + '|' + '|'.join(';'.join(f'{x},{y}' for x, y in st) for st in strokes)


# ------------------------------------------------------------------------------------------- uniqueness
def cmap(s, sw, sx, sy):
    cs = [c for c in s['cells'] if c.get('count', 0) > 0]
    q = [((c['y'] if sw else c['x']) * sx, (c['x'] if sw else c['y']) * sy) for c in cs]
    mx, my = min(p[0] for p in q), min(p[1] for p in q)
    return {(p[0] - mx, p[1] - my): (c['count'], V.ttype(c)) for c, p in zip(cs, q)}


class Uniq:
    """board_key + near-duplicate index over every board accepted so far (same rules as validate.py)."""
    def __init__(self):
        self.keys = set()
        self.by_outline = collections.defaultdict(list)

    def clashes(self, b):
        if V.board_key(b) in self.keys:
            return True
        for other in self.by_outline.get(V.outline(b), []):
            for sw in (False, True):
                for sx in (1, -1):
                    for sy in (1, -1):
                        db = cmap(b, sw, sx, sy)
                        if set(db) == set(other) and sum(1 for p in other if other[p] != db[p]) <= 2:
                            return True
        return False

    def add(self, b):
        self.keys.add(V.board_key(b))
        self.by_outline[V.outline(b)].append(cmap(b, False, 1, 1))


# ------------------------------------------------------------------------------------------- layout
def protected(i):
    c, k = divmod(i - 1, 50)
    return i in (301, 401, 1000) or k + 1 in (1, 2, 3, 50)


def plan_blocks(D, P):
    """-> list of dicts: block, kind (portal|breather), slot, order (new index -> old index or None for new)."""
    out = []
    for b in range(FIRST_BLOCK, LAST_BLOCK + 1, 5):
        idx = list(range(b, b + 5))
        portal = [p for p in PORTALS if p in idx]
        if portal:
            slot = portal[0]
            free = [i for i in idx if i == slot or not protected(i)]
        else:
            free = [i for i in idx if not protected(i)]
            slot = min(free, key=lambda i: (abs(i - (b + 2)), i))
        olds = list(free)                       # stages that may move, in original order
        kind = 'portal' if portal else 'breather'
        order = {i: i for i in idx}
        moved_easy = None
        if kind == 'breather':
            easy = [i for i in olds if BREATHER_MIN_D <= D[i] <= BREATHER_MAX_D]
            if easy:
                moved_easy = min(easy, key=lambda i: abs(i - slot))
        if moved_easy is not None:
            rest = [i for i in olds if i != moved_easy]
            order[slot] = moved_easy
            removed = None
        else:
            removed = max(olds, key=lambda i: (D[i], i))
            rest = [i for i in olds if i != removed]
            order[slot] = None
        for new_i, old_i in zip([i for i in free if i != slot], rest):
            order[new_i] = old_i
        out.append({'block': b, 'kind': kind, 'slot': slot, 'order': order, 'removed': removed, 'reused': moved_easy})
    return out


def allowed_mechs(i):
    return [m for m, at in INTRO_AT.items() if at <= i]


# ------------------------------------------------------------------------------------------- breathers
def breather_recipe(mech):
    R = {'shapes': ['poly', 'poly', 'art', 'sym', 'holed', 'ring', 'tiny', 'rect'], 'n': (7, 13), 'mult': (1.15, 1.6),
         'cap': [2, 2, 3], 'straight': [0, 1], 'maxlen': 20, 'mechs': {}, 'hidden_max': 2, 'tl_size': [2]}
    if mech:
        R['mechs'] = {mech: (1, 1)}
    return R


def single_stroke(items):
    """plain-clear single-stroke solutions (as emit.py does): {cid: strokes or None}"""
    nr = zs_c.run([(cid, s, {'limit': 2_000_000, 'norelease': True, 'plainwin': True}) for cid, s in items])
    out = {}
    for cid, s in items:
        r = nr[cid]
        if not (r['complete'] and r['solvable']):
            out[cid] = None
            continue
        strokes = zs.to_strokes(r['path'], r['released'])
        out[cid] = strokes if zs.replay_strokes(s, strokes)['ok'] else None
    return out


def make_pool(rng, specs, per, log):
    """specs: list of (tag, mech, recipe); mech None = no mechanic, False = keep any. -> {tag: [(stage, strokes, metrics)]}"""
    cands, tag_of = {}, {}
    k = 0
    for tag, mech, R in specs:
        made = 0
        tries = 0
        while made < per and tries < per * 20:
            tries += 1
            s = recipes.build(rng, R)
            if s is None or s.get('walls'):
                continue
            cid = f'{tag}#{k}'
            k += 1
            cands[cid] = s
            tag_of[cid] = (tag, mech)
            made += 1
    log(f'  finalize {len(cands)} candidates ...')
    fin = pipeline.finalize(cands)
    good = [(cid, rec) for cid, rec in fin.items() if rec['ok']]
    ss = single_stroke([(cid, rec['stage']) for cid, rec in good])
    pool = collections.defaultdict(list)
    for cid, rec in good:
        tag, mech = tag_of[cid]
        st = ss.get(cid)
        if st is None:
            continue
        types = set(rec['metrics']['types']) - {'Hidden'}
        if mech is not False and types != ({MECH_TYPE[mech]} if mech else set()):
            continue                     # the mechanic was stripped as ineffective (or an extra one appeared)
        pool[tag].append((rec['stage'], st, rec['metrics']))
    return pool


# ------------------------------------------------------------------------------------------- portal phase 1
def phase1_board(rng, ncells, total, cap=2):
    """Normal tiles + one Portal (count 1) that can only be the last step: walk a shape, then step out of it."""
    for _ in range(200):
        fam = rng.choice(['poly', 'poly', 'tiny', 'rect'])
        if fam == 'tiny' or ncells[1] <= 4:
            w, h = rng.choice([(2, 2), (1, 3), (1, 4), (2, 3), (3, 2), (1, 5), (2, 2)])
            cells = set(gen.rect(w, h))
        elif fam == 'rect':
            w, h = rng.choice([(2, 3), (3, 2), (2, 4), (4, 2), (3, 3), (2, 2)])
            cells = set(gen.rect(w, h))
        else:
            cells = set(gen.random_poly(rng, rng.randint(*ncells), 5, 5))
        if not (ncells[0] <= len(cells) <= ncells[1]) or not gen.connected(cells):
            continue
        L = rng.randint(*total) - 2          # moves inside the shape (sum of shape counts - 1)
        if L < len(cells) - 1:
            continue
        walk = gen.random_walk(rng, cells, L, cap=cap, avoid_back=rng.choice([0.3, 0.6, 0.9]))
        if not walk or len(walk) - 1 > L + 3:
            continue
        end = walk[-1]
        outs = [(end[0] + dx, end[1] + dy) for dx, dy in gen.DIRS if (end[0] + dx, end[1] + dy) not in cells]
        if not outs:
            continue
        portal = rng.choice(sorted(outs))
        allc = cells | {portal}
        norm = gen.normalize(allc)
        mx = min(x for x, y in allc); my = min(y for x, y in allc)
        sh = lambda p: (p[0] - mx, p[1] - my)
        counts = collections.Counter(sh(p) for p in walk)
        counts[sh(portal)] = 1
        if max(counts.values()) > cap or not gen.fits(norm):
            continue
        types = {sh(portal): 'Portal'}
        s = gen.make_stage(set(norm), sh(walk[0]), dict(counts), types)
        path = [list(sh(p)) for p in walk] + [list(sh(portal))]
        if not zs.replay(s, path)['ok']:
            continue
        return s
    return None


def solve_phase1(s):
    r = zs.analyze(s, norelease=True)
    if not (r['complete'] and r['solvable']):
        return None
    strokes = zs.to_strokes(r['path'], r['released'])
    rp = zs.replay_strokes(s, strokes)
    if not rp['ok']:
        return None
    return {'p': r['p_random'], 'strokes': strokes}


# ------------------------------------------------------------------------------------------- metrics (validate.py's own solver)
def vsolve(job):
    i, s = job
    p2 = V.phase2_stage(s)
    if p2 is None:
        return i, V.solve(s)
    return i, V.combine_solves(V.solve(s), V.solve(p2))


def mapping_row(i, s, sv, strokes_n, prov, theme, target_d):
    bs = [b for _, b in V.boards(i, s)]
    cells = [c for b in bs for c in b['cells'] if c.get('count', 0) > 0]
    last = bs[-1]
    lc = [c for c in last['cells'] if c.get('count', 0) > 0]
    xs = [c['x'] for c in lc]; ys = [c['y'] for c in lc]
    types = sorted({V.ttype(c) for c in cells} - {'Normal'})
    rules = {t for t in types if t in pipeline.RULE_TYPES}
    if 'Hidden' in types:
        rules.add('Igniter')
    p = sv['p_random']
    return {'stage': i, 'chapter': (i - 1) // 50 + 1, 'theme': theme, 'provenance': prov, 'notes': '',
            'w': max(xs) - min(xs) + 1, 'h': max(ys) - min(ys) + 1, 'tiles': len(cells), 'sum': sum(c['count'] for c in cells),
            'p_random': repr(p), 'neglog10_p': abs(round(-math.log10(p), 4)), 'crit_steps': round(sv['crit_steps'], 4),
            'forced_ratio': round(sv['forced_ratio'], 4), 'solutions': (str(int(sv['solutions'])) if sv['solutions'] < 1e6 else '1000000+'),
            'states': sv['states'], 'types': '+'.join(types) or 'Normal', 'walls': 0, 'rule_mechanics': len(rules),
            'outline_rect': int(len(lc) == (max(xs) - min(xs) + 1) * (max(ys) - min(ys) + 1)), 'strokes': strokes_n,
            'target_D': target_d, 'changed_vs_shipped': ''}


# ------------------------------------------------------------------------------------------- main
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--seed', type=int, default=20261114)
    ap.add_argument('--dry-run', action='store_true')
    a = ap.parse_args()
    log = lambda m: print(m, flush=True)
    if os.path.exists(ROLES):
        sys.exit('roles.json exists: the pack is already applied (restore stage files with git to rerun)')
    rng = random.Random(a.seed)

    stages = {i: json.load(open(os.path.join(STAGES, f'stage_{i}.json'), encoding='utf-8')) for i in range(1, 1001)}
    sols = load_solutions()
    rows = {int(r['stage']): r for r in csv.DictReader(open(MAPPING, encoding='utf-8'))}
    fields = list(next(iter(rows.values())).keys())
    D = {i: float(rows[i]['neglog10_p']) for i in rows}
    P = {i: float(rows[i]['p_random']) for i in rows}

    blocks = plan_blocks(D, P)
    removed = [bk['removed'] for bk in blocks if bk['removed']]
    log(f'{len(blocks)} blocks: {sum(bk["kind"] == "portal" for bk in blocks)} portal, '
        f'{sum(bk["kind"] == "breather" and bk["reused"] is None for bk in blocks)} new breathers, '
        f'{sum(bk["reused"] is not None for bk in blocks)} existing easy stages moved into the breather slot; '
        f'{len(removed)} stages removed (median D {statistics.median(D[i] for i in removed):.2f})')

    # regular-stage chapter medians after the change (portal difficulty cap)
    order = {i: i for i in range(1, 1001)}
    for bk in blocks:
        order.update(bk['order'])
    special = {bk['slot'] for bk in blocks}
    ch_med = {}
    for c in range(20):
        ds = [D[order[i]] for i in range(c * 50 + 1, c * 50 + 51) if i not in special and order[i] is not None]
        ch_med[c] = statistics.median(ds)

    uniq = Uniq()
    removed_set = set(removed)
    for i, s in stages.items():
        if i in removed_set:
            continue
        for _, b in V.boards(i, s):
            uniq.add(b)

    # ---------------- breathers
    need = [bk for bk in blocks if bk['kind'] == 'breather' and bk['reused'] is None]
    mechs = ['SC', 'CB', 'FK', 'TL', 'IGN', 'BC', 'BO', 'CHG', None]
    specs = [(m or 'N', m, breather_recipe(m)) for m in mechs]
    log('breathers: building pool ...')
    pool = make_pool(rng, specs, 700, log)
    pool = {t: [x for x in v if BREATHER_MIN_D <= x[2]['D'] <= BREATHER_MAX_D] for t, v in pool.items()}
    log('  usable per mechanic: ' + ', '.join(f'{t}:{len(v)}' for t, v in sorted(pool.items())))
    new_boards = {}           # slot -> (stage, strokes list-of-solution-lines, prov)
    last_m = None
    for bk in need:
        i = bk['slot']
        req = [m for m, rngi in FOCUS.items() if i in rngi]
        opts = req or [m for m in allowed_mechs(i) if m != last_m] + [None]
        rng.shuffle(opts)
        chosen = None
        for m in opts:
            cand = pool.get(m or 'N', [])
            rng.shuffle(cand)
            # later breathers may be a little bigger
            want_sum = 10 + 8 * (i - 301) / 700
            cand.sort(key=lambda x: abs(x[2]['sum'] - want_sum) + rng.random() * 4)
            for k, (s, st, met) in enumerate(cand):
                if uniq.clashes(s):
                    continue
                chosen = (m, s, st, met)
                cand.pop(k)
                break
            if chosen:
                break
        if not chosen:
            sys.exit(f'no breather candidate for {i}')
        m, s, st, met = chosen
        uniq.add(s)
        last_m = m
        new_boards[i] = (s, {str(i): st}, f'upgrade:breather:{m or "Normal"}')

    # ---------------- portals
    log('portals: building phase-1 boards ...')
    p1_pool = {'intro': [], 'early': [], 'main': []}
    sizes = {'intro': ((3, 4), (4, 6)), 'early': ((4, 6), (6, 9)), 'main': ((5, 8), (8, 12))}
    for tier, (nc, tot) in sizes.items():
        tries = 0
        while len(p1_pool[tier]) < 120 and tries < 6000:
            tries += 1
            s = phase1_board(rng, nc, tot)
            if s is None or uniq.clashes(s):
                continue
            r = solve_phase1(s)
            if r is None or not (0.2 <= r['p'] <= (0.95 if tier == 'intro' else 0.7)):
                continue
            if any(V.board_key(s) == V.board_key(x[0]) for x in p1_pool[tier]):
                continue
            p1_pool[tier].append((s, r['strokes'], -math.log10(r['p'])))
    log('  phase-1 pool: ' + ', '.join(f'{t}:{len(v)}' for t, v in p1_pool.items()))
    log('portals: building phase-2 boards ...')
    p2_specs = []
    for m in ['SC', 'CB', 'FK', 'TL', 'IGN', 'CHG']:
        R = {'shapes': ['poly', 'poly', 'art', 'sym', 'holed', 'ring', 'rect'], 'n': (7, 12), 'mult': (1.25, 1.8),
             'cap': [2, 3], 'straight': [0, 1], 'maxlen': 22, 'mechs': {m: (1, 1)}, 'hidden_max': 3, 'tl_size': [2, 2, 3]}
        p2_specs.append((m, m, R))
    pairs = [('SC', 'CB'), ('SC', 'FK'), ('CB', 'TL'), ('FK', 'IGN'), ('TL', 'CHG'), ('CB', 'CHG'), ('SC', 'IGN'), ('TL', 'IGN')]
    for m1, m2 in pairs:
        R = {'shapes': ['poly', 'poly', 'art', 'sym', 'holed', 'rect'], 'n': (8, 13), 'mult': (1.3, 1.9),
             'cap': [2, 3], 'straight': [0, 1], 'maxlen': 24, 'mechs': {m1: (1, 1), m2: (1, 1)}, 'hidden_max': 3, 'tl_size': [2]}
        p2_specs.append((f'{m1}+{m2}', None, R))
    raw = make_pool_multi(rng, p2_specs, 260, log)
    p2_pool = raw
    log('  phase-2 pool: ' + ', '.join(f'{t}:{len(v)}' for t, v in sorted(p2_pool.items())))
    for n, i in enumerate(PORTALS):
        tier = 'intro' if n == 0 else 'early' if n < 3 else 'main'
        hi = ch_med[(i - 1) // 50] - 0.25
        lo = max(0.6, hi - 1.0) if n >= 3 else 0.3
        tags = [t for t in p2_pool if '+' not in t] if n < 3 else list(p2_pool)
        best = None
        rng.shuffle(p1_pool[tier])
        for s1, st1, d1 in p1_pool[tier]:
            if uniq.clashes(s1):
                continue
            rng.shuffle(tags)
            for t in tags:
                for k, (s2, st2, met2) in enumerate(p2_pool[t]):
                    d = d1 + met2['D']
                    if not (lo <= d <= hi):
                        continue
                    if n < 3 and met2['sum'] > 16:
                        continue
                    if uniq.clashes(s2):
                        continue
                    best = (s1, st1, s2, st2, t, k)
                    break
                if best:
                    break
            if best:
                break
        if not best:
            sys.exit(f'no portal combination for {i} (D range {lo:.2f}-{hi:.2f})')
        s1, st1, s2, st2, t, k = best
        p2_pool[t].pop(k)
        uniq.add(s1); uniq.add(s2)
        s = copy.deepcopy(s1)
        s['phase2'] = {'width': s2['width'], 'height': s2['height'], 'startPoint': s2['startPoint'], 'cells': s2['cells']}
        new_boards[i] = (s, {str(i): st1, f'{i}:2': st2}, f'upgrade:portal:{t}')

    # ---------------- assemble
    new_stages, new_sols, new_rows = {}, {}, {}
    for i in range(1, 1001):
        old = order[i]
        if old is None:
            s, ss, prov = new_boards[i]
            new_stages[i] = json.loads(json.dumps(ordered(i, s)))
            new_sols.update(ss)
        else:
            new_stages[i] = json.loads(json.dumps(ordered(i, stages[old])))
            for key in (str(old), f'{old}:2'):
                if key in sols:
                    new_sols[key.replace(str(old), str(i), 1)] = sols[key]
            r = dict(rows[old])
            r['stage'] = i; r['chapter'] = rows[i]['chapter']; r['theme'] = rows[i]['theme']; r['target_D'] = rows[i]['target_D']
            new_rows[i] = r
    log('measuring new stages with validate.py\'s solver ...')
    fresh = [i for i in range(1, 1001) if order[i] is None]
    with mp.Pool(min(12, os.cpu_count() or 4)) as pool_:
        sv = dict(pool_.map(vsolve, [(i, new_stages[i]) for i in fresh]))
    for i in fresh:
        x = sv[i]
        assert x.get('complete') and x.get('solvable'), (i, x)
        nstrokes = len(new_sols[str(i)]) + len(new_sols.get(f'{i}:2', []))
        new_rows[i] = mapping_row(i, new_stages[i], x, nstrokes, new_boards[i][2], rows[i]['theme'], rows[i]['target_D'])
    breathers = sorted(bk['slot'] for bk in blocks if bk['kind'] == 'breather')
    roles = {'breathers': breathers, 'portals': sorted(PORTALS)}

    # summary
    Dn = {i: float(new_rows[i]['neglog10_p']) for i in new_rows}
    for i in PORTALS:
        log(f'  portal {i}: D={Dn[i]:.2f} (chapter regular median {ch_med[(i - 1) // 50]:.2f}) {new_rows[i]["provenance"]}')
    bd = [Dn[i] for i in breathers]
    log(f'  breathers: {len(breathers)}  D {min(bd):.2f}-{max(bd):.2f}  median {statistics.median(bd):.2f}')
    for lo_, hi_ in [(1, 100), (101, 200), (201, 300), (301, 400), (401, 500), (501, 600), (601, 700), (701, 800), (801, 900), (901, 1000)]:
        e = sum(1 for i in range(lo_, hi_ + 1) if Dn[i] <= 1.0)
        h = sum(1 for i in range(lo_, hi_ + 1) if Dn[i] >= 2.0)
        run_, worst = 0, 0
        for i in range(lo_, hi_ + 1):
            run_ = 0 if Dn[i] <= 1.0 else run_ + 1
            worst = max(worst, run_)
        log(f'  {lo_:4d}-{hi_:4d}: easy(p>=10%) {e:3d}  hard(p<1%) {h:3d}  longest run without easy {worst}')
    if a.dry_run:
        return

    # ---------------- write
    for i in range(1, 1001):
        if order[i] != i:
            with open(os.path.join(STAGES, f'stage_{i}.json'), 'w', encoding='utf-8') as f:
                json.dump(new_stages[i], f, indent=2, ensure_ascii=False)
                f.write('\n')
    with open(os.path.join(STAGES, 'solutions.txt'), 'w', encoding='utf-8') as f:
        for i in range(1, 1001):
            for key in (str(i), f'{i}:2'):
                if key in new_sols:
                    f.write(fmt_solution(key, new_sols[key]) + '\n')
    with open(MAPPING, 'w', newline='', encoding='utf-8') as f:
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        for i in range(1, 1001):
            w.writerow({k: new_rows[i].get(k, '') for k in fields})
    with open(ROLES, 'w', encoding='utf-8') as f:
        json.dump(roles, f, indent=1)
        f.write('\n')
    add_tutorials()
    log('written')


def make_pool_multi(rng, specs, per, log):
    pool = make_pool(rng, [(t, False, R) for t, m, R in specs], per, log)
    out = {}
    for t, v in pool.items():
        keep = []
        want = set(MECH_TYPE[m] for m in t.split('+'))
        for s, st, met in v:
            types = set(met['types']) - {'Hidden'}
            if types == want or (t in MECH_TYPE and types == {MECH_TYPE[t]}):
                keep.append((s, st, met))
        out[t] = keep
    return out


def add_tutorials():
    hp = os.path.join(TUTORIALS, 'help_tutorial_schedule.json')
    sp = os.path.join(TUTORIALS, 'stage_snackbar_schedule.json')
    h = json.load(open(hp, encoding='utf-8'), object_pairs_hook=collections.OrderedDict)
    s = json.load(open(sp, encoding='utf-8'), object_pairs_hook=collections.OrderedDict)
    if not any(e['tutorialType'] == 'Portal' for e in h['entries']):
        h['entries'].append(collections.OrderedDict([
            ('id', 'tutorial_stage_451_portal'), ('stageIndex', 451), ('tutorialType', 'Portal'),
            ('titleKey', 'tutorial_portal_title'), ('descriptionKey', 'tutorial_portal_description'),
            ('instructionTextKey', 'tutorial_portal_instructions'), ('closeButtonTextKey', 'help_close_button'),
            ('title', '포탈'),
            ('description', '다른 칸을 모두 지운 뒤 마지막으로 포탈에 들어가면 2페이즈가 시작됩니다.'),
            ('instructionText', '1. 포탈은 마지막 칸이에요\n   다른 칸을 전부 0으로 만든 뒤 마지막 이동으로만 들어갈 수 있어요.\n'
                                '2. 두 번째 판이 나와요\n   포탈에 들어가면 새 판이 나타나요. 두 판을 모두 풀어야 스테이지 클리어예요.\n'
                                '3. 실패하면 처음부터\n   두 번째 판에서 실패하면 첫 번째 판부터 다시 해요.'),
            ('closeButtonText', '확인')]))
        with open(hp, 'w', encoding='utf-8') as f:
            json.dump(h, f, indent=2, ensure_ascii=False)
    if not any(e.get('tileType') == 'Portal' for e in s['entries']):
        s['entries'].append(collections.OrderedDict([
            ('id', 'tile_unlock_preview_449_to_451'), ('stageIndex', 449), ('targetStageIndex', 451),
            ('tileType', 'Portal'), ('displayName', 'Portal'), ('messageKey', 'snackbar_default_new_tile_unlock'),
            ('message', '새로운 타입의 타일이 열립니다! {remainingStages}스테이지 남았습니다.'), ('duration', 2.8)]))
        with open(sp, 'w', encoding='utf-8') as f:
            json.dump(s, f, indent=2, ensure_ascii=False)


if __name__ == '__main__':
    main()
