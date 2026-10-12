"""ZeroStep rules engine + reference (Python) solver, extended with Wall and Charge.

Derived from the verified analysis solver, semantics unchanged for old mechanics:
- Move: orthogonal step into a live neighbour (count>0; Hidden only when its group is triggered).
  Walls (stage-level) remove the edge for PLAYER MOVEMENT only.
- ShortCircuit: exits only toward its direction cell; cannot be entered from that cell (Up = y+1).
- FixedKnot: first entry only when move number == targetOrder; after each step a FixedKnot with
  count>0, never entered and step >= targetOrder => game over.
- Leave effects of A -> T: A-1; TwinLink partners (same linkID, count>0) -1 except T and the previous
  tile of the current drag; CrossBlast: every orthogonal neighbour (count>0, walls ignored) -1 except T;
  Charge: every orthogonal neighbour N != T with count>0 whose type is Normal/ShortCircuit/CrossBlast/
  TwinLink/BlindCurtain/Blackout/Charge gains +1 (walls ignored, no chaining).
- Enter effects: Igniter triggers its group; FixedKnot marked entered. Missed-FixedKnot check, then win:
  entered tile count==1 and total==1 (or the TwinLink-group variant present in GameManager.cs).
- Finger release only matters for TwinLink 'previous tile' exclusion (modelled as an extra action).
"""
import json, sys, time

DIRS = [(1, 0), (-1, 0), (0, 1), (0, -1)]
SC_DIR = {'UP': (0, 1), 'DOWN': (0, -1), 'RIGHT': (1, 0), 'LEFT': (-1, 0)}
WALL_DIR = {'Up': (0, 1), 'Down': (0, -1), 'Left': (-1, 0), 'Right': (1, 0)}
CHARGEABLE = {'Normal', 'ShortCircuit', 'CrossBlast', 'TwinLink', 'BlindCurtain', 'Blackout', 'Charge'}
KNOWN = {'Normal', 'ShortCircuit', 'CrossBlast', 'FixedKnot', 'TwinLink', 'Igniter', 'Hidden',
         'BlindCurtain', 'Blackout', 'Charge'}


class Limit(Exception):
    pass


def wall_edges(s):
    """set of frozenset({a,b}) cell pairs separated by a wall."""
    out = set()
    for w in s.get('walls') or []:
        d = WALL_DIR[w['dir']]
        a = (w['x'], w['y'])
        b = (a[0] + d[0], a[1] + d[1])
        out.add(frozenset((a, b)))
    return out


class Stage:
    def __init__(self, s, bo_deadline=None, norelease=False, plainwin=False):
        self.plainwin = plainwin
        if isinstance(s, str):
            s = json.load(open(s))
        self.src = s
        self.norelease = norelease
        self.w, self.h = s['width'], s['height']
        cells = [c for c in s['cells'] if c.get('count', 0) > 0]
        self.cells = cells
        self.pos = [(c['x'], c['y']) for c in cells]
        self.idx = {p: i for i, p in enumerate(self.pos)}
        self.n = len(cells)
        self.type = [(c.get('type') or 'Normal') for c in cells]
        self.type = [t if t in KNOWN else 'Normal' for t in self.type]
        self.count0 = [c['count'] for c in cells]
        sp = (s['startPoint']['x'], s['startPoint']['y'])
        self.start = self.idx.get(sp, self.idx.get((0, 0)))
        walls = wall_edges(s)
        self.nb = []   # movement neighbours (walls respected)
        self.enb = []  # effect neighbours (walls ignored)
        for i, (x, y) in enumerate(self.pos):
            e = [self.idx[(x + dx, y + dy)] for dx, dy in DIRS if (x + dx, y + dy) in self.idx]
            self.enb.append(e)
            self.nb.append([j for j in e if frozenset((self.pos[i], self.pos[j])) not in walls])
        self.color = [(x + y) & 1 for x, y in self.pos]
        self.sc_exit = [None] * self.n
        for i, c in enumerate(cells):
            if self.type[i] == 'ShortCircuit':
                dx, dy = SC_DIR.get((c.get('direction') or 'Down').upper(), (0, -1))
                x, y = self.pos[i]
                self.sc_exit[i] = self.idx.get((x + dx, y + dy), -1)
        self.fk = [i for i in range(self.n) if self.type[i] == 'FixedKnot']
        self.fk_bit = {i: 1 << k for k, i in enumerate(self.fk)}
        self.fk_order = {i: (cells[i].get('targetOrder') or 0) if (cells[i].get('targetOrder') or 0) > 0 else 1 for i in self.fk}
        # pseudo deadline (analysis only): tile must be entered by step K (used for Blackout timing)
        self.dl = []
        if bo_deadline is not None:
            tiles, K = bo_deadline  # any of `tiles` must be entered by step K
            if isinstance(tiles, int):
                tiles = [tiles]
            b = 1 << len(self.fk)
            for t in tiles:
                self.dl.append((t, K, b))
        groups = {}
        for i in range(self.n):
            if self.type[i] == 'TwinLink':
                lid = cells[i].get('linkID') or 101
                groups.setdefault(lid, []).append(i)
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
                if t and t in gnames:
                    self.ign_bit[i] = 1 << gnames[t]
        self.cb = [self.type[i] == 'CrossBlast' for i in range(self.n)]
        self.charge = [self.type[i] == 'Charge' for i in range(self.n)]
        self.chargeable = [self.type[i] in CHARGEABLE for i in range(self.n)]
        self.remote = [i for i in range(self.n) if self.type[i] in ('CrossBlast', 'TwinLink', 'Charge')]

    def initial(self):
        return (self.start, -1, bytes(self.count0), self.ign_bit[self.start], 0, 0)

    def live(self, i, cnt, trig):
        return cnt[i] > 0 and (not self.hidden_bit[i] or (self.hidden_bit[i] & trig))

    def legal_targets(self, st):
        cur, prev, cnt, trig, fks, step = st
        res = []
        for n in self.nb[cur]:
            if not self.live(n, cnt, trig):
                continue
            if self.sc_exit[n] is not None and self.sc_exit[n] == cur:
                continue
            if self.sc_exit[cur] is not None and self.sc_exit[cur] != n:
                continue
            if n in self.fk_bit and not (fks & self.fk_bit[n]) and step + 1 != self.fk_order[n]:
                continue
            res.append(n)
        return res

    def apply(self, st, n, released=False):
        cur, prev, cnt, trig, fks, step = st
        c = bytearray(cnt)
        c[cur] -= 1
        p = self.partners[cur]
        if p:
            pv = -1 if released else prev
            for q in p:
                if q == n or q == pv:
                    continue
                if c[q] > 0:
                    c[q] -= 1
        if self.cb[cur]:
            for m in self.enb[cur]:
                if m != n and c[m] > 0:
                    c[m] -= 1
        if self.charge[cur]:
            for m in self.enb[cur]:
                if m != n and c[m] > 0 and self.chargeable[m]:
                    if c[m] >= 250:
                        raise OverflowError('charge overflow')
                    c[m] += 1
        step += 1
        if self.ign_bit[n]:
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
            pn = self.partners[n]
            if pn:
                ok = True
                s = 1
                for q in pn:
                    if c[q] > 0:
                        if c[q] != 1:
                            ok = False
                            break
                        s += c[q]
                if ok and total == s:
                    return 'dead' if self.plainwin else 'win'
        return (n, cur, bytes(c), trig, fks, step)

    def canon(self, st):
        cur, prev, cnt, trig, fks, step = st
        p = self.partners[cur]
        if not (p and prev in p and cnt[prev] > 0):
            prev = -1
        pending = False
        for k in self.fk:
            if cnt[k] > 0 and not (fks & self.fk_bit[k]):
                pending = True
                break
        for t, K, b in self.dl:
            if not (fks & b):
                pending = True
        if not pending:
            fks = 0
            step = 0
        return (cur, prev, cnt, trig, fks, step)

    def actions(self, st):
        """list of (target, released, outcome) for every distinct legal action, in canonical order."""
        cur, prev, cnt, trig, fks, step = st
        out = []
        tw = self.partners[cur]
        for n in self.legal_targets(st):
            out.append((n, False, self.apply(st, n)))
            if not self.norelease and tw and prev in tw and prev != n and cnt[prev] > 0:
                out.append((n, True, self.apply(st, n, released=True)))
        return out

    def quick_dead(self, st):
        cur, prev, cnt, trig, fks, step = st
        for r in self.remote:
            if cnt[r] > 0:
                return False
        ign_avail = trig
        for i in range(self.n):
            if self.ign_bit[i] and cnt[i] > 0:
                ign_avail |= self.ign_bit[i]
        passable = [cnt[i] > 0 and (not self.hidden_bit[i] or (self.hidden_bit[i] & ign_avail)) for i in range(self.n)]
        seen = {cur}
        stack = [cur]
        while stack:
            u = stack.pop()
            for v in self.nb[u]:
                if v not in seen and passable[v]:
                    seen.add(v)
                    stack.append(v)
        R = 0
        same = 0
        c0 = self.color[cur]
        for i in range(self.n):
            if cnt[i] > 0:
                if i not in seen:
                    return True
                R += cnt[i]
                if self.color[i] == c0:
                    same += cnt[i]
        if same != (R + 1) // 2:
            return True
        ends = 0
        for t in range(self.n):
            if t == cur or cnt[t] == 0:
                continue
            us = [v for v in self.nb[t] if cnt[v] > 0]
            if len(us) == 1:
                u = us[0]
                if cnt[u] < cnt[t]:
                    return True
                if cnt[u] == cnt[t]:
                    ends += 1
                    if ends > 1:
                        return True
        return False


BIG = 10 ** 6


def analyze(s, state_limit=2_000_000, time_limit=1e9, bo_deadline=None, norelease=False, plainwin=False):
    S = s if isinstance(s, Stage) else Stage(s, bo_deadline=bo_deadline, norelease=norelease, plainwin=plainwin)
    memo = {}
    t0 = time.time()
    nodes = [0]
    WIN = (1, 1.0, 0, 0, 0, 0, 0, 0, 0)
    DEAD = (0, 0.0, 0, 0, 0, 0, 0, BIG, -1)

    def rec(st):
        key = S.canon(st)
        v = memo.get(key)
        if v is not None:
            return v
        nodes[0] += 1
        if nodes[0] > state_limit or (nodes[0] & 4095) == 0 and time.time() - t0 > time_limit:
            raise Limit()
        if S.quick_dead(st):
            memo[key] = DEAD
            return DEAD
        outs = S.actions(st)
        legal = len(outs)
        if legal == 0:
            memo[key] = DEAD
            return DEAD
        child = []
        for _, _, o in outs:
            if o == 'win':
                child.append(WIN)
            elif o == 'dead':
                child.append(DEAD)
            else:
                child.append(rec(o))
        good = sum(1 for c in child if c[0] > 0)
        P = sum(c[1] for c in child) / legal
        N = 0; F = 0; Fr = 0; Cr = 0; L = 0; LS = 0; mn = BIG; mx = -1
        forced = legal == 1
        free = legal > 1 and good == legal
        crit = legal > 1 and good < legal
        for c in child:
            if c[0] == 0:
                continue
            N += c[0]
            F += c[2] + (c[0] if forced else 0)
            Fr += c[3] + (c[0] if free else 0)
            Cr += c[4] + (c[0] if crit else 0)
            L += c[5] + c[0]
            LS += c[6] + c[0] * legal
            mn = min(mn, c[7] + 1)
            mx = max(mx, c[8] + 1)
        v = (N, P, F, Fr, Cr, L, LS, mn, mx)
        memo[key] = v
        return v

    root = S.initial()
    old = sys.getrecursionlimit()
    sys.setrecursionlimit(max(old, 100000))
    try:
        r = rec(root)
        complete = True
    except Limit:
        r = None
        complete = False
    res = {'n_tiles': S.n, 'sum': sum(S.count0), 'w': S.w, 'h': S.h,
           'types': sorted(set(S.type) - {'Normal'}), 'complete': complete,
           'states': nodes[0], 'time': round(time.time() - t0, 2)}
    if complete:
        N, P, F, Fr, Cr, L, LS, mn, mx = r
        res.update({'solvable': N > 0, 'solutions': N, 'p_random': P,
                    'forced_ratio': F / L if L else None, 'free_ratio': Fr / L if L else None,
                    'crit_ratio': Cr / L if L else None, 'avg_len': L / N if N else None,
                    'avg_branch': LS / L if L else None,
                    'min_len': mn if N else None, 'max_len': mx if N else None})
        res['crit_steps'] = (Cr / N) if N else None
        if N > 0:
            path = [list(S.pos[S.start])]
            rel = []
            st = root
            while True:
                nxt = None
                for n, released, o in S.actions(st):
                    if o == 'win' or (o != 'dead' and memo.get(S.canon(o), (0,))[0] > 0):
                        nxt = (n, released, o)
                        break
                path.append(list(S.pos[nxt[0]]))
                rel.append(1 if nxt[1] else 0)
                if nxt[2] == 'win':
                    break
                st = nxt[2]
            res['path'] = path
            res['released'] = rel
    return res


# ----------------------------------------------------------------------------------------------
# Independent straightforward replay (continuous drag, no finger lift) used for validation.
def replay(s, path, release_flags=None):
    """Replays a list of [x,y] cells (first = start) under the rules. Returns dict(ok, reason, ...)."""
    cells = {(c['x'], c['y']): dict(c) for c in s['cells'] if c.get('count', 0) > 0}
    for c in cells.values():
        t = c.get('type') or 'Normal'
        c['type'] = t if t in KNOWN else 'Normal'
    cnt = {p: c['count'] for p, c in cells.items()}
    walls = wall_edges(s)
    start = (s['startPoint']['x'], s['startPoint']['y'])
    if tuple(path[0]) != start:
        return {'ok': False, 'reason': 'path does not begin at startPoint'}
    if start not in cells:
        return {'ok': False, 'reason': 'start cell missing'}
    triggered = set()
    entered_fk = set()
    blackout_step = None

    def unlock(p):
        c = cells[p]
        if c['type'] == 'Igniter' and c.get('targetID'):
            triggered.add(c['targetID'])
    unlock(start)
    cur = start
    prev = None
    nmoves = len(path) - 1
    for k in range(1, len(path)):
        nxt = tuple(path[k])
        step = k
        released = bool(release_flags[k - 1]) if release_flags else False
        if released:
            prev = None
        if nxt not in cells or cnt[nxt] <= 0:
            return {'ok': False, 'reason': f'step {k}: target {nxt} not live'}
        if abs(nxt[0] - cur[0]) + abs(nxt[1] - cur[1]) != 1:
            return {'ok': False, 'reason': f'step {k}: not adjacent'}
        if frozenset((cur, nxt)) in walls:
            return {'ok': False, 'reason': f'step {k}: wall between {cur} and {nxt}'}
        tc = cells[nxt]
        if tc['type'] == 'Hidden' and (tc.get('groupID') or 'default') not in triggered:
            return {'ok': False, 'reason': f'step {k}: hidden tile locked'}
        cc = cells[cur]
        if cc['type'] == 'ShortCircuit':
            d = SC_DIR.get((cc.get('direction') or 'Down').upper(), (0, -1))
            if (cur[0] + d[0], cur[1] + d[1]) != nxt:
                return {'ok': False, 'reason': f'step {k}: ShortCircuit exit violated'}
        if tc['type'] == 'ShortCircuit':
            d = SC_DIR.get((tc.get('direction') or 'Down').upper(), (0, -1))
            if (nxt[0] + d[0], nxt[1] + d[1]) == cur:
                return {'ok': False, 'reason': f'step {k}: ShortCircuit entered from its exit'}
        if tc['type'] == 'FixedKnot' and nxt not in entered_fk:
            order = tc.get('targetOrder') or 0
            order = order if order > 0 else 1
            if step != order:
                return {'ok': False, 'reason': f'step {k}: FixedKnot needs order {order}'}
        # leave effects
        cnt[cur] -= 1
        if cc['type'] == 'TwinLink':
            lid = cc.get('linkID') or 101
            for p, c in cells.items():
                if p != cur and c['type'] == 'TwinLink' and (c.get('linkID') or 101) == lid:
                    if p == nxt or p == prev:
                        continue
                    if cnt[p] > 0:
                        cnt[p] -= 1
        if cc['type'] in ('CrossBlast', 'Charge'):
            for dx, dy in DIRS:
                q = (cur[0] + dx, cur[1] + dy)
                if q == nxt or q not in cells or cnt[q] <= 0:
                    continue
                if cc['type'] == 'CrossBlast':
                    cnt[q] -= 1
                elif cells[q]['type'] in CHARGEABLE:
                    cnt[q] += 1
        # enter effects
        prev, cur = cur, nxt
        if tc['type'] == 'Blackout' and blackout_step is None:
            blackout_step = step
        unlock(cur)
        if tc['type'] == 'FixedKnot':
            entered_fk.add(cur)
        for p, c in cells.items():
            if c['type'] == 'FixedKnot' and cnt[p] > 0 and p not in entered_fk:
                order = c.get('targetOrder') or 0
                order = order if order > 0 else 1
                if step >= order:
                    return {'ok': False, 'reason': f'step {k}: missed FixedKnot at {p}'}
        total = sum(cnt.values())
        win = False
        if cnt[cur] == 1:
            if total == 1:
                win = True
            elif tc['type'] == 'TwinLink':
                lid = tc.get('linkID') or 101
                part = [p for p, c in cells.items() if p != cur and c['type'] == 'TwinLink' and (c.get('linkID') or 101) == lid]
                if all(cnt[p] in (0, 1) for p in part) and total == 1 + sum(cnt[p] for p in part):
                    win = True
        if win:
            if k != nmoves:
                return {'ok': False, 'reason': f'step {k}: cleared before end of path'}
            return {'ok': True, 'moves': nmoves, 'blackout_step': blackout_step}
    return {'ok': False, 'reason': 'path ended without clear', 'remaining': sum(cnt.values())}


# ----------------------------------------------------------------------------------------------
def dump_stage(s, path):
    with open(path, 'w') as f:
        json.dump(s, f, indent=2, ensure_ascii=False)


if __name__ == '__main__':
    import pprint
    for p in sys.argv[1:]:
        r = analyze(json.load(open(p)))
        r.pop('path', None)
        pprint.pprint(r)


def to_strokes(path, released):
    """path: [[x,y],...] (start first); released[k]=1 means finger lifted before move k+1."""
    strokes = [[list(path[0])]]
    for k in range(1, len(path)):
        if released and released[k - 1]:
            strokes.append([list(path[k - 1])])
        strokes[-1].append(list(path[k]))
    return strokes


def from_strokes(strokes):
    path = [list(strokes[0][0])]
    rel = []
    for si, st in enumerate(strokes):
        if si > 0 and list(st[0]) != path[-1]:
            raise ValueError('stroke does not start where previous ended')
        for k in range(1, len(st)):
            path.append(list(st[k]))
            rel.append(1 if (si > 0 and k == 1) else 0)
    return path, rel


def replay_strokes(s, strokes):
    path, rel = from_strokes(strokes)
    return replay(s, path, rel)
