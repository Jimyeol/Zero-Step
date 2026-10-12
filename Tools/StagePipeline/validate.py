#!/usr/bin/env python3
"""Re-validate the ZeroStep 1000-stage content set from scratch.

Usage:  python3 -I validate.py [OUT_DIR] [--jobs N] [--quick] [--no-ablation]

  OUT_DIR       directory containing Stages/, Tutorials/, mapping.csv, solutions.json (default: this file's dir)
  --quick       replay + static checks only (skip the exhaustive solver, ablation and Blackout-timing checks)
  --no-ablation run the exhaustive solver but skip the per-instance mechanic ablation
  --jobs N      worker processes (default: CPU count - 1)

Everything is self-contained (pure Python 3, standard library only):
  * a straightforward rule simulator that replays the stroke solutions in solutions.json
    (a stroke = one continuous drag; a new stroke = finger lifted, which clears the TwinLink "previous tile");
  * an independent exhaustive solver (memoised DFS over game states, same rules) that recomputes solvability,
    p_random (probability that uniformly random legal play wins), crit_steps, forced_ratio, solution counts
    and state counts, then checks every content requirement.

Rules modelled (GameManager.cs semantics + the Charge spec; this content set uses NO walls -- any stage with a
`walls` field fails the format check):
  move orthogonally into a live neighbour (count>0; Hidden only once its group is ignited);
  leaving a tile: tile-1, TwinLink partners -1 (except entered + previous
  tile of the current drag), CrossBlast neighbours -1 (except entered), Charge: chargeable neighbours
  (Normal/ShortCircuit/CrossBlast/TwinLink/BlindCurtain/Blackout/Charge) with count>0 +1 (except entered);
  entering: Igniter ignites its group, FixedKnot first entry only on move == targetOrder; after each move a
  FixedKnot never entered with count>0 and move >= targetOrder is game over; clear when the entered tile is 1
  and the board total is 1 (plus the TwinLink-group variant present in GameManager.cs).
"""
import sys, os, json, csv, time, math, collections, multiprocessing as mp

DIRS = [(1, 0), (-1, 0), (0, 1), (0, -1)]
SC_DIR = {'UP': (0, 1), 'DOWN': (0, -1), 'RIGHT': (1, 0), 'LEFT': (-1, 0)}
CHARGEABLE = {'Normal', 'ShortCircuit', 'CrossBlast', 'TwinLink', 'BlindCurtain', 'Blackout', 'Charge'}
KNOWN = {'Normal', 'ShortCircuit', 'CrossBlast', 'FixedKnot', 'TwinLink', 'Igniter', 'Hidden',
         'BlindCurtain', 'Blackout', 'Charge'}
RULE_TYPES = {'ShortCircuit', 'CrossBlast', 'FixedKnot', 'TwinLink', 'Igniter', 'Charge'}
INTRO = {'ShortCircuit': 21, 'CrossBlast': 51, 'FixedKnot': 101, 'TwinLink': 151, 'Igniter': 201,
         'BlindCurtain': 251, 'Blackout': 301, 'Charge': 401}
STAGE_KEYS = ['stageID', 'width', 'height', 'startPoint', 'config', 'cells']
STATE_LIMIT = 2_000_000
ABL_LIMIT = 4_000_000


# =============================================================================================== rules
def ttype(c):
    t = c.get('type') or 'Normal'
    return t if t in KNOWN else 'Normal'


def replay(s, strokes):
    """Simple direct simulation of stroke input. Returns dict(ok, reason, moves, blackout_step)."""
    cells = {(c['x'], c['y']): c for c in s['cells'] if c.get('count', 0) > 0}
    cnt = {p: c['count'] for p, c in cells.items()}
    start = (s['startPoint']['x'], s['startPoint']['y'])
    if start not in cells:
        return {'ok': False, 'reason': 'start cell missing'}
    if not strokes or tuple(strokes[0][0]) != start:
        return {'ok': False, 'reason': 'first stroke must start at startPoint'}
    ignited = set()
    entered_fk = set()
    blackout_step = None

    def ignite(p):
        c = cells[p]
        if ttype(c) == 'Igniter' and c.get('targetID'):
            ignited.add(c['targetID'])
    ignite(start)
    cur = start
    step = 0
    total_moves = sum(len(st) - 1 for st in strokes)
    for si, stroke in enumerate(strokes):
        if tuple(stroke[0]) != cur:
            return {'ok': False, 'reason': f'stroke {si} does not start at current cell {cur}'}
        prev = None  # new drag: no previous tile
        for k in range(1, len(stroke)):
            nxt = tuple(stroke[k])
            step += 1
            if nxt not in cells or cnt[nxt] <= 0:
                return {'ok': False, 'reason': f'move {step}: {nxt} is not a live tile'}
            if abs(nxt[0] - cur[0]) + abs(nxt[1] - cur[1]) != 1:
                return {'ok': False, 'reason': f'move {step}: not orthogonally adjacent'}
            tc, cc = cells[nxt], cells[cur]
            if ttype(tc) == 'Hidden' and (tc.get('groupID') or 'default') not in ignited:
                return {'ok': False, 'reason': f'move {step}: Hidden tile still locked'}
            if ttype(cc) == 'ShortCircuit':
                d = SC_DIR.get((cc.get('direction') or 'Down').upper(), (0, -1))
                if (cur[0] + d[0], cur[1] + d[1]) != nxt:
                    return {'ok': False, 'reason': f'move {step}: ShortCircuit must exit toward its arrow'}
            if ttype(tc) == 'ShortCircuit':
                d = SC_DIR.get((tc.get('direction') or 'Down').upper(), (0, -1))
                if (nxt[0] + d[0], nxt[1] + d[1]) == cur:
                    return {'ok': False, 'reason': f'move {step}: ShortCircuit entered from its exit cell'}
            if ttype(tc) == 'FixedKnot' and nxt not in entered_fk:
                order = tc.get('targetOrder') or 0
                order = order if order > 0 else 1
                if step != order:
                    return {'ok': False, 'reason': f'move {step}: FixedKnot only enterable on move {order}'}
            # ---- leave effects
            cnt[cur] -= 1
            if ttype(cc) == 'TwinLink':
                lid = cc.get('linkID') or 101
                for p, c in cells.items():
                    if p != cur and ttype(c) == 'TwinLink' and (c.get('linkID') or 101) == lid:
                        if p == nxt or p == prev:
                            continue
                        if cnt[p] > 0:
                            cnt[p] -= 1
            if ttype(cc) in ('CrossBlast', 'Charge'):
                for dx, dy in DIRS:
                    q = (cur[0] + dx, cur[1] + dy)
                    if q == nxt or q not in cells or cnt[q] <= 0:
                        continue
                    if ttype(cc) == 'CrossBlast':
                        cnt[q] -= 1
                    elif ttype(cells[q]) in CHARGEABLE:
                        cnt[q] += 1
            # ---- enter effects
            prev, cur = cur, nxt
            if ttype(tc) == 'Blackout' and blackout_step is None:
                blackout_step = step
            ignite(cur)
            if ttype(tc) == 'FixedKnot':
                entered_fk.add(cur)
            for p, c in cells.items():
                if ttype(c) == 'FixedKnot' and cnt[p] > 0 and p not in entered_fk:
                    order = c.get('targetOrder') or 0
                    order = order if order > 0 else 1
                    if step >= order:
                        return {'ok': False, 'reason': f'move {step}: missed FixedKnot {p}'}
            total = sum(cnt.values())
            win = False
            if cnt[cur] == 1:
                if total == 1:
                    win = True
                elif ttype(tc) == 'TwinLink':
                    lid = tc.get('linkID') or 101
                    part = [p for p, c in cells.items() if p != cur and ttype(c) == 'TwinLink' and (c.get('linkID') or 101) == lid]
                    if part and all(cnt[p] in (0, 1) for p in part) and total == 1 + sum(cnt[p] for p in part):
                        win = True
            if win:
                if step != total_moves:
                    return {'ok': False, 'reason': f'cleared at move {step} before the end of the solution'}
                return {'ok': True, 'moves': step, 'blackout_step': blackout_step, 'plain': total == 1}
    return {'ok': False, 'reason': 'solution ended without clearing', 'remaining': sum(cnt.values())}


class Limit(Exception):
    pass


class Stage:
    def __init__(self, s, deadline=None):
        self.w, self.h = s['width'], s['height']
        cells = [c for c in s['cells'] if c.get('count', 0) > 0]
        self.pos = [(c['x'], c['y']) for c in cells]
        self.idx = {p: i for i, p in enumerate(self.pos)}
        self.n = len(cells)
        self.type = [ttype(c) for c in cells]
        self.count0 = [c['count'] for c in cells]
        self.start = self.idx[(s['startPoint']['x'], s['startPoint']['y'])]
        # no walls: movement neighbours == effect neighbours
        self.enb = [[self.idx[(x + dx, y + dy)] for dx, dy in DIRS if (x + dx, y + dy) in self.idx] for x, y in self.pos]
        self.nb = self.enb
        self.color = [(x + y) & 1 for x, y in self.pos]
        self.sc_exit = [None] * self.n
        for i, c in enumerate(cells):
            if self.type[i] == 'ShortCircuit':
                dx, dy = SC_DIR.get((c.get('direction') or 'Down').upper(), (0, -1))
                self.sc_exit[i] = self.idx.get((self.pos[i][0] + dx, self.pos[i][1] + dy), -1)
        self.fk = [i for i in range(self.n) if self.type[i] == 'FixedKnot']
        self.fk_bit = {i: 1 << k for k, i in enumerate(self.fk)}
        self.fk_order = {i: max(1, cells[i].get('targetOrder') or 0) for i in self.fk}
        self.dl = []
        if deadline:
            tiles, K = deadline
            b = 1 << len(self.fk)
            self.dl = [(t, K, b) for t in tiles]
        groups = collections.defaultdict(list)
        for i in range(self.n):
            if self.type[i] == 'TwinLink':
                groups[cells[i].get('linkID') or 101].append(i)
        self.partners = [None] * self.n
        for g in groups.values():
            for i in g:
                self.partners[i] = [j for j in g if j != i]
        gnames = {}
        self.hidden_bit = [0] * self.n
        for i in range(self.n):
            if self.type[i] == 'Hidden':
                g = cells[i].get('groupID') or 'default'
                gnames.setdefault(g, len(gnames))
                self.hidden_bit[i] = 1 << gnames[g]
        self.ign_bit = [0] * self.n
        for i in range(self.n):
            if self.type[i] == 'Igniter':
                t = cells[i].get('targetID') or ''
                if t in gnames:
                    self.ign_bit[i] = 1 << gnames[t]
        self.cb = [t == 'CrossBlast' for t in self.type]
        self.charge = [t == 'Charge' for t in self.type]
        self.chargeable = [t in CHARGEABLE for t in self.type]
        self.remote = [i for i in range(self.n) if self.type[i] in ('CrossBlast', 'TwinLink', 'Charge')]

    def legal(self, st):
        cur, prev, cnt, trig, fks, step = st
        res = []
        for n in self.nb[cur]:
            if cnt[n] <= 0 or (self.hidden_bit[n] and not (self.hidden_bit[n] & trig)):
                continue
            if self.sc_exit[n] is not None and self.sc_exit[n] == cur:
                continue
            if self.sc_exit[cur] is not None and self.sc_exit[cur] != n:
                continue
            if n in self.fk_bit and not (fks & self.fk_bit[n]) and step + 1 != self.fk_order[n]:
                continue
            res.append(n)
        return res

    def apply(self, st, n, released):
        cur, prev, cnt, trig, fks, step = st
        c = bytearray(cnt)
        c[cur] -= 1
        if self.partners[cur]:
            pv = -1 if released else prev
            for q in self.partners[cur]:
                if q != n and q != pv and c[q] > 0:
                    c[q] -= 1
        if self.cb[cur]:
            for m in self.enb[cur]:
                if m != n and c[m] > 0:
                    c[m] -= 1
        if self.charge[cur]:
            for m in self.enb[cur]:
                if m != n and c[m] > 0 and self.chargeable[m]:
                    c[m] += 1
        step += 1
        trig |= self.ign_bit[n]
        if n in self.fk_bit:
            fks |= self.fk_bit[n]
        for t, K, b in self.dl:
            if n == t:
                fks |= b
        for k in self.fk:
            if c[k] > 0 and not (fks & self.fk_bit[k]) and step >= self.fk_order[k]:
                return 'dead'
        for t, K, b in self.dl:
            if not (fks & b) and step >= K:
                return 'dead'
        total = sum(c)
        if c[n] == 1:
            if total == 1:
                return 'win'
            if self.partners[n]:
                ok, sm = True, 1
                for q in self.partners[n]:
                    if c[q] > 0:
                        if c[q] != 1:
                            ok = False
                            break
                        sm += c[q]
                if ok and total == sm:
                    return 'win'
        return (n, cur, bytes(c), trig, fks, step)

    def key(self, st):
        cur, prev, cnt, trig, fks, step = st
        p = self.partners[cur]
        if not (p and prev in p and cnt[prev] > 0):
            prev = -1
        pending = any(cnt[k] > 0 and not (fks & self.fk_bit[k]) for k in self.fk) or any(not (fks & b) for _, _, b in self.dl)
        if not pending:
            fks = step = 0
        return (cur, prev, cnt, trig, fks, step)

    def actions(self, st):
        cur, prev, cnt = st[0], st[1], st[2]
        tw = self.partners[cur]
        out = []
        for n in self.legal(st):
            out.append(self.apply(st, n, False))
            if tw and prev in tw and prev != n and cnt[prev] > 0:
                out.append(self.apply(st, n, True))
        return out

    def hopeless(self, st):
        cur, prev, cnt, trig, fks, step = st
        if any(cnt[r] > 0 for r in self.remote):
            return False
        avail = trig
        for i in range(self.n):
            if self.ign_bit[i] and cnt[i] > 0:
                avail |= self.ign_bit[i]
        ok = [cnt[i] > 0 and (not self.hidden_bit[i] or (self.hidden_bit[i] & avail)) for i in range(self.n)]
        seen, stack = {cur}, [cur]
        while stack:
            u = stack.pop()
            for v in self.nb[u]:
                if v not in seen and ok[v]:
                    seen.add(v)
                    stack.append(v)
        R = same = 0
        for i in range(self.n):
            if cnt[i] > 0:
                if i not in seen:
                    return True
                R += cnt[i]
                if self.color[i] == self.color[cur]:
                    same += cnt[i]
        if same != (R + 1) // 2:
            return True
        ends = 0
        for t in range(self.n):
            if t == cur or cnt[t] == 0:
                continue
            us = [v for v in self.nb[t] if cnt[v] > 0]
            if len(us) == 1:
                if cnt[us[0]] < cnt[t]:
                    return True
                if cnt[us[0]] == cnt[t]:
                    ends += 1
                    if ends > 1:
                        return True
        return False


def solve(s, limit=STATE_LIMIT, deadline=None):
    """exhaustive analysis -> dict(complete, states, solvable, solutions, p_random, crit_steps, forced_ratio, min_len)"""
    S = Stage(s, deadline)
    memo = {}
    nodes = [0]
    WIN = (1, 1.0, 0, 0, 0, 0)
    DEAD = (0, 0.0, 0, 0, 0, 10 ** 6)
    sys.setrecursionlimit(100000)

    def rec(st):
        k = S.key(st)
        v = memo.get(k)
        if v is not None:
            return v
        nodes[0] += 1
        if nodes[0] > limit:
            raise Limit()
        if S.hopeless(st):
            memo[k] = DEAD
            return DEAD
        outs = S.actions(st)
        if not outs:
            memo[k] = DEAD
            return DEAD
        ch = [WIN if o == 'win' else DEAD if o == 'dead' else rec(o) for o in outs]
        legal = len(outs)
        good = sum(1 for c in ch if c[0] > 0)
        N = F = Cr = L = 0
        mn = 10 ** 6
        for c in ch:
            if c[0]:
                N += c[0]
                F += c[2] + (c[0] if legal == 1 else 0)
                Cr += c[3] + (c[0] if (legal > 1 and good < legal) else 0)
                L += c[4] + c[0]
                mn = min(mn, c[5] + 1)
        v = (N, sum(c[1] for c in ch) / legal, F, Cr, L, mn)
        memo[k] = v
        return v
    try:
        N, P, F, Cr, L, mn = rec((S.start, -1, bytes(S.count0), S.ign_bit[S.start], 0, 0))
    except Limit:
        return {'complete': False, 'states': nodes[0]}
    return {'complete': True, 'states': nodes[0], 'solvable': N > 0, 'solutions': N, 'p_random': P,
            'crit_steps': Cr / N if N else None, 'forced_ratio': F / L if L else None, 'min_len': mn if N else None}


def ablations(s):
    """(label, kind, ablated stage) for every rule-changing mechanic instance"""
    out = []
    norm = lambda c: {'x': c['x'], 'y': c['y'], 'count': c['count'], 'type': 'Normal'}
    groups = collections.OrderedDict()
    for i, c in enumerate(s['cells']):
        t = ttype(c)
        if c.get('count', 0) <= 0:
            continue
        if t in ('ShortCircuit', 'FixedKnot', 'CrossBlast', 'TwinLink', 'Charge'):
            a = json.loads(json.dumps(s))
            a['cells'][i] = norm(c)
            out.append((f"{t}@{c['x']},{c['y']}", t, a))
        if t == 'Igniter':
            groups.setdefault(c.get('targetID') or '', None)
        if t == 'Hidden':
            groups.setdefault(c.get('groupID') or 'default', None)
    for g in groups:
        a = json.loads(json.dumps(s))
        for i, c in enumerate(a['cells']):
            if (ttype(c) == 'Igniter' and (c.get('targetID') or '') == g) or (ttype(c) == 'Hidden' and (c.get('groupID') or 'default') == g):
                a['cells'][i] = norm(c)
        out.append((f'Igniter/Hidden:{g}', 'Igniter', a))
    return out


def find_path(s, limit=STATE_LIMIT):
    """one winning action sequence as strokes (exhaustive DFS with dead memo), or None"""
    S = Stage(s)
    dead = set()
    nodes = [0]

    def rec(st):
        k = S.key(st)
        if k in dead:
            return None
        nodes[0] += 1
        if nodes[0] > limit:
            raise Limit()
        cur, prev, cnt = st[0], st[1], st[2]
        tw = S.partners[cur]
        for n in S.legal(st):
            for rel in ((False, True) if (tw and prev in tw and prev != n and cnt[prev] > 0) else (False,)):
                o = S.apply(st, n, rel)
                if o == 'win':
                    return [(n, rel)]
                if o != 'dead':
                    r = rec(o)
                    if r is not None:
                        return [(n, rel)] + r
        dead.add(k)
        return None
    try:
        seq = rec((S.start, -1, bytes(S.count0), S.ign_bit[S.start], 0, 0))
    except Limit:
        return None
    if seq is None:
        return None
    strokes = [[list(S.pos[S.start])]]
    for n, rel in seq:
        if rel:
            strokes.append([strokes[-1][-1]])
        strokes[-1].append(list(S.pos[n]))
    return strokes


def matters(kind, orig, ab, s, a, orig_strokes):
    if not ab['complete']:
        return None
    if not ab['solvable']:
        return True
    if ab['solutions'] != orig['solutions']:
        return True
    if kind in ('ShortCircuit', 'FixedKnot', 'Igniter'):
        return False  # restriction removed but solution set unchanged
    if not replay(a, orig_strokes)['ok']:
        return True
    p = find_path(a)
    if p is not None and not replay(s, p)['ok']:
        return True
    return False


def work(args):
    i, s, strokes, do_solve, do_abl = args
    r = {'id': i}
    t0 = time.time()
    r['replay'] = replay(s, strokes)
    if do_solve:
        a = solve(s)
        r['solve'] = a
        if a.get('complete') and a.get('solvable'):
            S = Stage(s)
            bo = [k for k in range(S.n) if S.type[k] == 'Blackout']
            if bo:
                K = int(math.floor(0.6 * a['min_len']))
                d = solve(s, limit=2 * STATE_LIMIT, deadline=(bo, K))
                r['blackout_ok'] = bool(d.get('complete') and d['solutions'] == a['solutions'])
            if do_abl:
                ab = []
                for label, kind, abl_stage in ablations(s):
                    x = solve(abl_stage, limit=ABL_LIMIT)
                    ab.append((label, matters(kind, a, x, s, abl_stage, strokes)))
                r['ablation'] = ab
    r['time'] = round(time.time() - t0, 2)
    return r


# =============================================================================================== checks
def outline(s):
    pts = [(c['x'], c['y']) for c in s['cells'] if c.get('count', 0) > 0]
    best = None
    for sw in (False, True):
        for sx in (1, -1):
            for sy in (1, -1):
                q = [((y if sw else x) * sx, (x if sw else y) * sy) for x, y in pts]
                mx, my = min(p[0] for p in q), min(p[1] for p in q)
                k = tuple(sorted((p[0] - mx, p[1] - my) for p in q))
                best = k if best is None or k < best else best
    return best


def board_key(s):
    """canonical board ignoring start (D4 symmetric): cells+counts+types+params"""
    cells = [c for c in s['cells'] if c.get('count', 0) > 0]
    best = None
    for sw in (False, True):
        for sx in (1, -1):
            for sy in (1, -1):
                tf = lambda p: ((p[1] if sw else p[0]) * sx, (p[0] if sw else p[1]) * sy)
                q = [tf((c['x'], c['y'])) for c in cells]
                mx, my = min(p[0] for p in q), min(p[1] for p in q)
                items = []
                gm = {}
                for c, p in sorted(zip(cells, q), key=lambda z: z[1]):
                    t = ttype(c)
                    ex = ''
                    if t == 'ShortCircuit':
                        ex = tf(SC_DIR.get((c.get('direction') or 'Down').upper(), (0, -1)))
                    elif t == 'FixedKnot':
                        ex = c.get('targetOrder')
                    elif t == 'TwinLink':
                        ex = gm.setdefault(('L', c.get('linkID')), len(gm))
                    elif t == 'Hidden':
                        ex = gm.setdefault(('G', c.get('groupID')), len(gm))
                    elif t == 'Igniter':
                        ex = gm.setdefault(('G', c.get('targetID')), len(gm))
                    items.append(((p[0] - mx, p[1] - my), c['count'], t, str(ex)))
                k = tuple(items)
                best = k if best is None or k < best else best
    return best


def spearman(xs, ys):
    def ranks(v):
        o = sorted(range(len(v)), key=lambda i: v[i])
        r = [0.0] * len(v)
        i = 0
        while i < len(o):
            j = i
            while j + 1 < len(o) and v[o[j + 1]] == v[o[i]]:
                j += 1
            for k in range(i, j + 1):
                r[o[k]] = (i + j) / 2.0
            i = j + 1
        return r
    rx, ry = ranks(xs), ranks(ys)
    mx, my = sum(rx) / len(rx), sum(ry) / len(ry)
    num = sum((a - mx) * (b - my) for a, b in zip(rx, ry))
    den = math.sqrt(sum((a - mx) ** 2 for a in rx) * sum((b - my) ** 2 for b in ry))
    return num / den if den else 0.0


class Report:
    def __init__(self):
        self.rows = []

    def check(self, name, failures, total=None, detail=''):
        ok = not failures
        self.rows.append((name, ok, failures, total, detail))
        status = 'PASS' if ok else 'FAIL'
        cnt = f'{len(failures)} fail' + (f' / {total}' if total is not None else '')
        print(f'[{status}] {name}: {cnt}' + (f' -- {detail}' if detail else '') + ('' if ok else f'  e.g. {failures[:8]}'))


def main():
    args = sys.argv[1:]
    out = os.path.dirname(os.path.abspath(__file__))
    jobs = max(1, (os.cpu_count() or 2) - 1)
    quick = '--quick' in args
    no_abl = '--no-ablation' in args
    pos = [a for a in args if not a.startswith('--')]
    if '--jobs' in args:
        jobs = int(args[args.index('--jobs') + 1])
        pos = [a for a in pos if a != args[args.index('--jobs') + 1]]
    if pos:
        out = pos[0]
    R = Report()
    t0 = time.time()
    sd = os.path.join(out, 'Stages')
    stages = {}
    bad_files = []
    for i in range(1, 1001):
        p = os.path.join(sd, f'stage_{i}.json')
        try:
            stages[i] = json.load(open(p, encoding='utf-8'))
        except Exception as e:
            bad_files.append((i, str(e)))
    R.check('1000 stage files present and parse', bad_files, 1000)
    extra = [f for f in os.listdir(sd) if f.startswith('stage_') and f.endswith('.json') and not (1 <= int(f[6:-5] or 0) <= 1000)]
    R.check('no extra stage files', extra)
    idx = json.load(open(os.path.join(sd, 'index.json')))
    R.check('index.json lists stage_1..stage_1000 in order', [] if idx == [f'stage_{i}.json' for i in range(1, 1001)] else ['mismatch'])
    sols = json.load(open(os.path.join(out, 'solutions.json')))
    mapping = {int(r['stage']): r for r in csv.DictReader(open(os.path.join(out, 'mapping.csv'), encoding='utf-8'))}
    R.check('mapping.csv has a row per stage', [i for i in range(1, 1001) if i not in mapping], 1000)
    R.check('solutions.json has a solution per stage', [i for i in range(1, 1001) if str(i) not in sols], 1000)

    # ---------------- static checks
    fmt, lim, struct, walled = [], [], [], []
    for i, s in stages.items():
        if s.get('stageID') != i:
            fmt.append((i, 'stageID'))
        if list(s.keys()) != STAGE_KEYS:
            fmt.append((i, 'keys'))
        if 'walls' in s:
            walled.append(i)
        cells = [c for c in s['cells'] if c.get('count', 0) > 0]
        xs = [c['x'] for c in cells]; ys = [c['y'] for c in cells]
        w, h = max(xs) - min(xs) + 1, max(ys) - min(ys) + 1
        if s['width'] > 7 or s['height'] > 8 or w > 7 or h > 8 or max(c['count'] for c in cells) > 6 or sum(c['count'] for c in cells) > 48:
            lim.append(i)
        pos_ = {(c['x'], c['y']): c for c in cells}
        if len(pos_) != len(cells) or any(not (0 <= c['x'] < s['width'] and 0 <= c['y'] < s['height']) for c in cells):
            struct.append((i, 'cell coords'))
        sp = (s['startPoint']['x'], s['startPoint']['y'])
        if sp not in pos_ or ttype(pos_[sp]) in ('Hidden', 'FixedKnot'):
            struct.append((i, 'start'))
        for c in cells:
            t = ttype(c)
            if (c.get('type') or 'Normal') not in KNOWN:
                struct.append((i, 'unknown type'))
            if t == 'ShortCircuit' and (c.get('direction') or '').upper() not in SC_DIR:
                struct.append((i, 'SC direction'))
            if t == 'FixedKnot' and not (c.get('targetOrder') or 0) > 0:
                struct.append((i, 'FK order'))
            if t == 'Charge':
                for dx, dy in DIRS:
                    q = pos_.get((c['x'] + dx, c['y'] + dy))
                    if q is not None and ttype(q) in ('FixedKnot', 'Igniter', 'Hidden', 'Charge'):
                        struct.append((i, 'Charge adjacent to FixedKnot/Igniter/Hidden/Charge'))
        tl = collections.Counter(c.get('linkID') or 101 for c in cells if ttype(c) == 'TwinLink')
        if any(v < 2 for v in tl.values()):
            struct.append((i, 'TwinLink group of 1'))
        hid = {c.get('groupID') or 'default' for c in cells if ttype(c) == 'Hidden'}
        ign = {c.get('targetID') for c in cells if ttype(c) == 'Igniter'}
        if hid - ign or ign - hid:
            struct.append((i, 'Igniter/Hidden group mismatch'))
    R.check('no stage contains walls (no `walls` field anywhere)', walled, 1000)
    R.check('stageID == file number, same JSON layout (stageID,width,height,startPoint,config,cells)', fmt, 1000)
    R.check('size limits (w<=7, h<=8, count<=6, sum<=48)', lim, 1000)
    R.check('structural sanity (start, params, Charge adjacency, groups)', struct, 1000)

    # mechanic schedule
    def mechs(s):
        ts = {ttype(c) for c in s['cells'] if c.get('count', 0) > 0}
        if 'Hidden' in ts:
            ts.add('Igniter')
        return ts
    first = {}
    for i in range(1, 1001):
        for t in mechs(stages[i]):
            first.setdefault(t, i)
    sched = [(m, first.get(m), at) for m, at in INTRO.items() if first.get(m) != at]
    R.check('mechanic first appearance == intro schedule', sched, len(INTRO), str({m: first.get(m) for m in INTRO}))
    tiny = [m for m, at in INTRO.items() if not (stages[at]['width'] <= 3 and stages[at]['height'] <= 3 and m in mechs(stages[at]))]
    R.check('intro stages are tiny (<=3x3) and contain the mechanic', tiny, len(INTRO))
    focus = [(m, j) for m, at in INTRO.items() for j in range(at + 1, at + 11) if m not in mechs(stages[j])]
    R.check('10 stages after each intro focus on the new mechanic', focus, 10 * len(INTRO))
    multi = [i for i in range(451, 1001) if len(mechs(stages[i]) & RULE_TYPES) >= 2]
    R.check('from 451 on most stages combine >=2 rule-changing mechanics (>=75%)', [] if len(multi) >= 0.75 * 550 else ['too few'], None, f'{len(multi)}/550 = {len(multi) / 5.5:.1f}%')
    fin = mechs(stages[1000])
    R.check('stage 1000 uses every mechanic', sorted((RULE_TYPES | {'BlindCurtain', 'Blackout'}) - fin), None, f"{stages[1000]['width']}x{stages[1000]['height']}: {sorted(fin - {'Normal'})}")

    # tutorials
    tut = json.load(open(os.path.join(out, 'Tutorials', 'help_tutorial_schedule.json'), encoding='utf-8'))
    sn = json.load(open(os.path.join(out, 'Tutorials', 'stage_snackbar_schedule.json'), encoding='utf-8'))
    tmap = {e['tutorialType'] if e['tutorialType'] != 'BlackOut' else 'Blackout': e['stageIndex'] for e in tut['entries']}
    tt = [(m, tmap.get(m), at) for m, at in INTRO.items() if tmap.get(m) != at]
    smap = {(e.get('tileType') if e.get('tileType') != 'BlackOut' else 'Blackout'): (e['stageIndex'], e['targetStageIndex']) for e in sn['entries']}
    st_ = [(m, smap.get(m)) for m, at in INTRO.items() if smap.get(m) != (at - 2, at)]
    ids = [e['id'] for e in tut['entries']] + [e['id'] for e in sn['entries']]
    R.check('help tutorial entries at intro stages', tt, len(INTRO))
    R.check('snackbar teasers 2 stages before each intro', st_, len(INTRO))
    R.check('tutorial/snackbar ids unique', [x for x, c in collections.Counter(ids).items() if c > 1])
    wall_t = [e['id'] for e in tut['entries'] if e.get('tutorialType') == 'Wall'] + [e['id'] for e in sn['entries'] if e.get('tileType') == 'Wall']
    R.check('no Wall tutorial/snackbar entries', wall_t)

    # duplicates / shapes
    keys = collections.defaultdict(list)
    for i, s in stages.items():
        keys[board_key(s)].append(i)
    dups = [v for v in keys.values() if len(v) > 1]
    R.check('no duplicate boards (rotation/mirror, any start)', dups)
    outl = {i: outline(s) for i, s in stages.items()}

    def cmap(s, sw, sx, sy):
        cs = [c for c in s['cells'] if c.get('count', 0) > 0]
        q = [((c['y'] if sw else c['x']) * sx, (c['x'] if sw else c['y']) * sy) for c in cs]
        mx, my = min(p[0] for p in q), min(p[1] for p in q)
        return {(p[0] - mx, p[1] - my): (c['count'], ttype(c)) for c, p in zip(cs, q)}
    byo = collections.defaultdict(list)
    for i in range(1, 1001):
        byo[outl[i]].append(i)
    near = []
    for v in byo.values():
        for a_i in range(len(v)):
            for b_i in range(a_i + 1, len(v)):
                a, b = v[a_i], v[b_i]
                da = cmap(stages[a], False, 1, 1)
                for sw in (False, True):
                    for sx in (1, -1):
                        for sy in (1, -1):
                            db = cmap(stages[b], sw, sx, sy)
                            if set(db) == set(da) and sum(1 for p in da if da[p] != db[p]) <= 2 and (a, b) not in near:
                                near.append((a, b))
    R.check('no near-duplicates (same outline, <= 2 cells differ in count/type)', near)
    runs = [i for i in range(4, 1001) if outl[i] == outl[i - 1] == outl[i - 2] == outl[i - 3]]
    R.check('no more than 3 consecutive stages with the same outline', runs)
    nonrect = [i for i in range(101, 1001) if len(outl[i]) != (max(x for x, y in outl[i]) + 1) * (max(y for x, y in outl[i]) + 1)]
    R.check('>=40% non-rectangular outlines after stage 100', [] if len(nonrect) >= 0.4 * 900 else ['too few'], None, f'{len(nonrect)}/900 = {len(nonrect) / 9:.1f}%')

    # ---------------- replay + solver (parallel)
    tasks = [(i, stages[i], sols.get(str(i)), not quick, not quick and not no_abl) for i in range(1, 1001)]
    tasks.sort(key=lambda t: -sum(c['count'] for c in t[1]['cells']))
    res = {}
    print(f'running replay{" + exhaustive solver" if not quick else ""}{" + ablation" if not quick and not no_abl else ""} on {jobs} workers ...', flush=True)
    with mp.Pool(jobs) as pool:
        for k, r in enumerate(pool.imap_unordered(work, tasks, chunksize=1)):
            res[r['id']] = r
            if (k + 1) % 100 == 0:
                print(f'  {k + 1}/1000 ({time.time() - t0:.0f}s)', flush=True)
    rp = [(i, res[i]['replay'].get('reason')) for i in range(1, 1001) if not res[i]['replay']['ok']]
    R.check('every provided solution (strokes) replays to a clear under the rules', rp, 1000)
    npl = [i for i in range(1, 1001) if res[i]['replay']['ok'] and not res[i]['replay'].get('plain')]
    R.check('every solution ends with the plain clear (entered tile 1, board total 1) - valid with or without the TwinLink-group clear', npl, 1000)
    multi_stroke = [i for i in range(1, 1001) if sols.get(str(i)) and len(sols[str(i)]) > 1]
    print(f'      solutions needing a finger lift (multi-stroke): {len(multi_stroke)} {multi_stroke[:20]}')
    if quick:
        summarize(R, t0)
        return
    sv = {i: res[i]['solve'] for i in range(1, 1001)}
    R.check('independent exhaustive solver: every stage solvable (complete search)', [i for i in sv if not (sv[i].get('complete') and sv[i].get('solvable'))], 1000)
    R.check('solver states < 2,000,000 for every stage', [i for i in sv if sv[i]['states'] >= 2_000_000], 1000)
    big = [(i, sv[i]['states']) for i in sv if sv[i]['states'] > 1_000_000]
    print(f'      stages with > 1,000,000 states: {len(big)} {big}')
    mm = []
    for i in range(1, 1001):
        a, m = sv[i], mapping.get(i)
        if not a.get('complete') or m is None:
            continue
        if abs(float(m['p_random']) - a['p_random']) > 1e-9 * max(1.0, a['p_random']) or int(m['states']) != a['states']:
            mm.append(i)
    R.check('mapping.csv metrics match the independent solver', mm, 1000)
    bo = [i for i in range(1, 1001) if 'blackout_ok' in res[i] and not res[i]['blackout_ok']]
    nbo = sum(1 for i in range(1, 1001) if 'blackout_ok' in res[i])
    R.check('Blackout triggered with >=40% of moves remaining in every solution', bo, nbo)
    if not no_abl:
        ab = [(i, l, v) for i in range(1, 1001) for l, v in res[i].get('ablation', []) if v is not True]
        nab = sum(len(res[i].get('ablation', [])) for i in range(1, 1001))
        R.check('every rule-changing mechanic instance matters (ablation changes solutions)', ab, nab)
    P = {i: sv[i]['p_random'] for i in sv if sv[i].get('solvable')}
    D = {i: (-math.log10(P[i]) if P[i] > 0 else 30.0) for i in P}
    C = {i: sv[i]['crit_steps'] for i in P}
    intro_set = set(INTRO.values())
    unl = [i for i in range(21, 1001) if P.get(i, 0) >= 0.999999999 and i not in intro_set]
    R.check('no unlosable stage (p_random = 1) after stage 20 except intros', unl)
    easy = [i for i in range(31, 1001) if P.get(i, 0) >= 0.5]
    roles = {i: 'intro' for i in intro_set}
    for c0 in range(0, 1000, 50):
        for k in (1, 2, 3):
            roles.setdefault(c0 + k, 'warm-up')
    bad_easy = [i for i in easy if i not in roles and not (i - 1 in D and D[i] < D[i - 1])]
    spaced = [i for i in easy if any(j in easy for j in range(i - 5, i))]
    R.check('after 30: p_random>=0.5 only on intro/warm-up/breather stages, max 1 per 6', bad_easy + spaced, None, f'{len(easy)} such stages: {easy}')
    fc = min([i for i in range(1, 1001) if (C.get(i) or 0) >= 2] or [9999])
    R.check('first stage with crit_steps >= 2 is <= 8', [] if fc <= 8 else [fc], None, f'first = {fc}')
    R.check('stage 1000 p_random < 1e-4', [] if P.get(1000, 1) < 1e-4 else [P.get(1000)], None, f'p = {P.get(1000):.3g}')
    # difficulty curve
    meds = []
    for c in range(20):
        ids_ = list(range(c * 50 + 1, c * 50 + 51))
        ds = sorted(D[i] for i in ids_)
        cs = sorted(C[i] for i in ids_)
        meds.append((ds[24] + ds[25]) / 2)
        boss = c * 50 + 50
        if D[boss] < max(D[i] for i in ids_) - 1e-9:
            print(f'      note: chapter {c + 1} boss {boss} D={D[boss]:.2f} < chapter max {max(D[i] for i in ids_):.2f}')
        print(f'      chapter {c + 1:2d} ({c * 50 + 1:4d}-{c * 50 + 50:4d}): median -log10(p) {meds[-1]:.2f}  median crit_steps {(cs[24] + cs[25]) / 2:.2f}  boss {D[boss]:.2f}')
    rho_ch = spearman(list(range(20)), meds)
    rho_st = spearman(list(range(1, 1001)), [D[i] for i in range(1, 1001)])
    rho_cr = spearman(list(range(1, 1001)), [C[i] for i in range(1, 1001)])
    R.check('chapter-median difficulty rises (Spearman >= 0.6)', [] if rho_ch >= 0.6 else [rho_ch], None,
            f'chapter-level rho={rho_ch:.3f}; stage-level rho(-log10 p)={rho_st:.3f}, rho(crit_steps)={rho_cr:.3f}')
    bosses = [c * 50 + 50 for c in range(20) if D[c * 50 + 50] < max(D[i] for i in range(c * 50 + 1, c * 50 + 51)) - 1e-9]
    R.check('chapter stage 50 is the hardest of its chapter', bosses, 20)
    summarize(R, t0)


def summarize(R, t0):
    n_ok = sum(1 for r in R.rows if r[1])
    print(f'\nSUMMARY: {n_ok}/{len(R.rows)} checks passed in {time.time() - t0:.0f}s')
    sys.exit(0 if n_ok == len(R.rows) else 1)


if __name__ == '__main__':
    main()
