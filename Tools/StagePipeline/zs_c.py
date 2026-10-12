"""Bridge to the C solver (zsolve). Serialises zs.Stage derived structures; runs batches in parallel."""
import os, json, subprocess, concurrent.futures as cf
import zs

HERE = os.path.dirname(os.path.abspath(__file__))
BIN = os.path.join(HERE, 'zsolve')
TCODE = {'Normal': 0, 'ShortCircuit': 1, 'CrossBlast': 2, 'FixedKnot': 3, 'TwinLink': 4, 'Igniter': 5,
         'Hidden': 6, 'BlindCurtain': 7, 'Blackout': 8, 'Charge': 9}


def build():
    src = os.path.join(HERE, 'zsolve.c')
    if not os.path.exists(BIN) or os.path.getmtime(BIN) < os.path.getmtime(src):
        subprocess.check_call(['cc', '-O2', '-o', BIN, src])


def block(sid, s, limit=2_500_000, norelease=False, bo_deadline=None, plainwin=False):
    S = zs.Stage(s, bo_deadline=bo_deadline, norelease=norelease, plainwin=plainwin)
    assert S.n <= 60 and len(S.fk) + len(S.dl) <= 16
    assert all(b < 256 for b in S.hidden_bit)
    out = [f'STAGE {sid} {S.n} {S.start} {(1 if norelease else 0) | (2 if plainwin else 0)} {limit}']
    for i in range(S.n):
        sc = -2 if S.sc_exit[i] is None else S.sc_exit[i]
        fko = S.fk_order.get(i, 0)
        part = S.partners[i] or []
        row = [S.count0[i], TCODE[S.type[i]], sc, fko, S.color[i], S.hidden_bit[i], S.ign_bit[i],
               int(S.cb[i]), int(S.charge[i]), int(S.chargeable[i]), len(part)] + part
        row += [len(S.nb[i])] + S.nb[i] + [len(S.enb[i])] + S.enb[i]
        out.append(' '.join(map(str, row)))
    out.append(str(len(S.dl)) + ''.join(f' {t} {K} {b.bit_length() - 1}' for t, K, b in S.dl))
    return '\n'.join(out) + '\n', S


def _run_chunk(chunk):
    txt = ''.join(b for _, b, _ in chunk)
    p = subprocess.run([BIN], input=txt, capture_output=True, text=True)
    if p.returncode != 0:
        raise RuntimeError('zsolve failed: ' + p.stderr[:500])
    res = {}
    lines = [l for l in p.stdout.splitlines() if l.strip()]
    for (sid, _, S), line in zip(chunk, lines):
        r = json.loads(line)
        if 'path' in r:
            if -1 in r['path']:
                raise RuntimeError(f'path extraction failed for {sid}')
            r['path'] = [list(S.pos[i]) for i in r['path']]
        if r.get('complete') and r.get('solvable') is not None:
            r['n_solutions'] = r['solutions']
        res[sid] = r
    if len(lines) != len(chunk):
        raise RuntimeError('zsolve output count mismatch')
    return res


def run(items, nproc=None, chunk=None):
    nproc = nproc or int(os.environ.get('ZS_NPROC', '14'))
    """items: list of (sid, stage_dict, opts_dict). returns {sid: result}."""
    build()
    blocks = []
    for sid, s, opts in items:
        b, S = block(sid, s, **(opts or {}))
        blocks.append((sid, b, S))
    if not blocks:
        return {}
    if chunk is None:
        chunk = max(1, min(40, len(blocks) // (nproc * 3) or 1))
    chunks = [blocks[i:i + chunk] for i in range(0, len(blocks), chunk)]
    res = {}
    with cf.ThreadPoolExecutor(nproc) as ex:
        for r in ex.map(_run_chunk, chunks):
            res.update(r)
    return res


def solve1(s, **opts):
    return run([('x', s, opts)], nproc=1)['x']
