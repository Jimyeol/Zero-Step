"""Random candidate construction from a recipe (walk first, mechanics consistent with the walk)."""
import random
import gen, zs

SHAPES = gen.art_shapes()
SHAPE_NAMES = sorted(SHAPES)


def pick_shape(rng, fam, nmin, nmax):
    for _ in range(80):
        if fam == 'rect':
            w, h = rng.randint(2, 7), rng.randint(2, 8)
            cells = gen.rect(w, h)
        elif fam == 'holed':
            w, h = rng.randint(3, 7), rng.randint(3, 8)
            cells = gen.holed_rect(rng, w, h, rng.randint(1, max(1, w * h // 4)))
        elif fam == 'art':
            cells = SHAPES[rng.choice(SHAPE_NAMES)]
            cells = gen.transform(cells, rng.choice(gen.ALL_T))
        elif fam == 'sym':
            cells = gen.sym_poly(rng, rng.randint(3, 7), rng.randint(3, 8), rng.uniform(0.55, 0.85))
        elif fam == 'ring':
            w, h = rng.randint(3, 7), rng.randint(3, 8)
            t = rng.choice([1, 1, 2])
            cells = frozenset((x, y) for x in range(w) for y in range(h) if x < t or y < t or x >= w - t or y >= h - t)
            if rng.random() < 0.5:
                cells = frozenset(cells | {(rng.randrange(w), rng.randrange(h))})
            cells = gen.normalize(cells)
        elif fam == 'tiny':
            w, h = rng.choice([(2, 2), (2, 3), (3, 2), (3, 3), (3, 3), (3, 3)])
            cells = gen.rect(w, h)
            if w * h == 9 and rng.random() < 0.4:
                cells = gen.holed_rect(rng, 3, 3, 1)
        else:  # poly
            cells = gen.random_poly(rng, rng.randint(nmin, nmax))
        if gen.fits(cells) and nmin <= len(cells) <= nmax and gen.connected(cells):
            return set(cells)
    return None


def nb4(c):
    return [(c[0] + dx, c[1] + dy) for dx, dy in gen.DIRS]


def build(rng, R):
    fam = rng.choice(R['shapes'])
    cells = pick_shape(rng, fam, *R['n'])
    if not cells:
        return None
    n = len(cells)
    mult = rng.uniform(*R['mult'])
    L = max(n, int(round(n * mult)))
    L = min(L, R.get('maxlen', 47))
    cap = rng.choice(R.get('cap', [3]))
    walk = gen.random_walk(rng, cells, L, cap=cap, straight=rng.choice(R.get('straight', [0, 1])),
                           avoid_back=rng.choice([0.3, 0.6, 0.9]))
    if not walk or len(walk) - 1 > R.get('maxlen', 47) + 6:
        return None
    M = len(walk) - 1
    start = walk[0]
    final = walk[-1]
    types, extra = {}, {}
    fe = gen.first_entry(walk)
    mech = R['mechs']
    want = {k: rng.randint(*v) for k, v in mech.items()}
    visits = {}
    for c in walk:
        visits[c] = visits.get(c, 0) + 1

    def free(c):
        return c not in types

    def near_charge(c):
        return any(types.get(q) == 'Charge' for q in nb4(c))

    # Charge first (others avoid adjacency)
    last_app = {}
    for i, c in enumerate(walk):
        last_app[c] = i
    def charge_weight(c):
        w = 0
        for i in range(M):
            if walk[i] == c:
                for q in nb4(c):
                    if q in cells and q != walk[i + 1] and last_app.get(q, -1) > i:
                        w += 1
        return w
    for _ in range(want.get('CHG', 0)):
        cs = [c for c in cells if free(c) and not near_charge(c) and visits.get(c, 0) >= 1]
        if not cs:
            break
        cs.sort()
        wts = [0.2 + charge_weight(c) for c in cs]
        types[rng.choices(cs, wts)[0]] = 'Charge'
    # ShortCircuit
    scc = gen.sc_candidates(cells, walk)
    for _ in range(want.get('SC', 0)):
        cs = [c for c in scc if free(c)]
        if not cs:
            break
        cs.sort()
        wts = [1 + visits.get(c, 0) + 2 * (sum(1 for q in nb4(c) if q in cells) - 2) for c in cs]
        wts = [max(0.3, w) for w in wts]
        c = rng.choices(cs, wts)[0]
        types[c] = 'ShortCircuit'
        extra[c] = {'direction': gen.DNAME[scc[c]]}
    # FixedKnot
    for _ in range(want.get('FK', 0)):
        cs = [c for c in cells if free(c) and c != start and c in fe and 2 <= fe[c] <= M - 1 and not near_charge(c)]
        if not cs:
            break
        cs.sort()
        c = rng.choice(cs)
        types[c] = 'FixedKnot'
        extra[c] = {'targetOrder': fe[c]}
    # Igniter / Hidden groups
    gid = rng.randint(10, 99)
    for g in range(want.get('IGN', 0)):
        cs = [c for c in cells if free(c) and not near_charge(c) and (c == start or (c in fe and fe[c] <= M * 0.6))]
        if not cs:
            break
        cs.sort()
        ig = rng.choices(cs, [0.5 if c == start else (1 + 3 * (fe[c] >= 0.15 * M)) for c in cs])[0]
        t0 = 0 if ig == start else fe[ig]
        hs = [c for c in cells if free(c) and c != ig and c != start and c in fe and fe[c] > t0 and not near_charge(c)]
        if not hs:
            break
        hs.sort()
        k = min(len(hs), rng.randint(1, R.get('hidden_max', 4)))
        name = f'G{gid + g}'
        types[ig] = 'Igniter'
        extra[ig] = {'targetID': name}
        # prefer contiguous-ish hidden groups: grow from a random seed
        def tempt(c):
            m = min([fe.get(q, M) if q != start else 0 for q in nb4(c) if q in cells] or [M])
            return 1 + max(0, fe[c] - m) / 3.0
        seed = rng.choices(hs, [tempt(c) for c in hs])[0]
        grp = [seed]
        pool = set(hs) - {seed}
        while len(grp) < k:
            adj = [c for c in sorted(pool) if any(q in grp for q in nb4(c))]
            pick = rng.choice(adj) if adj and rng.random() < 0.8 else (rng.choice(sorted(pool)) if pool else None)
            if pick is None:
                break
            grp.append(pick)
            pool.discard(pick)
        for c in grp:
            types[c] = 'Hidden'
            extra[c] = {'groupID': name}
    # CrossBlast
    for _ in range(want.get('CB', 0)):
        cs = [c for c in cells if free(c)]
        if not cs:
            break
        cs.sort()
        c = rng.choice(cs)
        types[c] = 'CrossBlast'
        extra[c] = {'beamColor': rng.choice(gen.BEAMS)}
    # TwinLink groups
    for g in range(want.get('TL', 0)):
        k = rng.choice(R.get('tl_size', [2, 2, 3]))
        cs = [c for c in cells if free(c)]
        if len(cs) < k:
            break
        cs.sort()
        grp = rng.sample(cs, k)
        lid = rng.randint(100, 999)
        col = gen.TWIN_COLORS[g % len(gen.TWIN_COLORS)] if g else rng.choice(gen.TWIN_COLORS)
        for c in grp:
            types[c] = 'TwinLink'
            extra[c] = {'linkID': lid, 'color': col}
    counts = gen.derive_counts(cells, walk, types, extra, rng, prefer_hits=R.get('prefer_hits', 0.5))
    if not counts:
        return None
    if max(counts.values()) > 6 or sum(counts.values()) > 48:
        return None
    # informational
    for _ in range(want.get('BC', 0)):
        cs = sorted(c for c in cells if free(c) and counts[c] >= 1)
        if not cs:
            break
        types[rng.choice(cs)] = 'BlindCurtain'
    if want.get('BO', 0):
        cs = sorted(c for c in cells if free(c) and c != start and c in fe and fe[c] <= 0.45 * M)
        if cs:
            types[rng.choice(cs)] = 'Blackout'
    # walls on uncrossed edges (never on a ShortCircuit exit edge)
    walls = set()
    nw = want.get('WALL', 0)
    if nw:
        unc = gen.all_edges(cells) - gen.crossed_edges(walk)
        for c, t in types.items():
            if t == 'ShortCircuit':
                d = {'Up': (0, 1), 'Down': (0, -1), 'Left': (-1, 0), 'Right': (1, 0)}[extra[c]['direction']]
                unc.discard(frozenset((c, (c[0] + d[0], c[1] + d[1]))))
        unc = sorted(unc, key=lambda e: sorted(e))
        walls = set(rng.sample(unc, min(nw, len(unc))))
    s = gen.make_stage(cells, start, counts, types, extra, walls)
    rp = zs.replay(s, [list(p) for p in walk])
    if not rp['ok']:
        return None
    return s
