// Fast exhaustive ZeroStep solver; mirrors zs.py analyze() exactly (same canonicalisation, pruning,
// move order and statistics).  Input: stage blocks prepared by zs_c.py.  Output: one JSON line each.
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>

#define MAXN 64
#define MAXDL 16
#define BIGL 1000000

typedef struct { double N, P, F, Fr, Cr, L, LS; int mn, mx; } Val;

static int n, start_tile, norelease, plainwin, KL;
static long long state_limit;
static int cnt0[MAXN];
static int mnb[MAXN][4], mnbc[MAXN];
static int enb[MAXN][4], enbc[MAXN];
static int scexit[MAXN];      // -2: not SC, -1: SC exit cell missing, else index
static int fkorder[MAXN];     // >0 if FixedKnot (entry restricted)
static int fkbitv[MAXN];
static int fk_list[MAXDL], nfk;
static int dl_tile[MAXDL], dl_K[MAXDL], dl_bit[MAXDL], ndl;  // pseudo deadlines (no entry restriction)
static int partner[MAXN][MAXN], npartner[MAXN];
static int hidbit[MAXN], ignbit[MAXN];
static int iscb[MAXN], ischarge[MAXN], chargeable[MAXN];
static int remote_list[MAXN], nremote;
static int color[MAXN];
static int overflow_flag;

// ---------------- memo -----------------
static uint8_t *karena; static size_t kcap;
static Val *vals; static size_t vcap;
static uint32_t *table; static size_t tcap, nent;

static uint64_t hashk(const uint8_t *k) {
    uint64_t h = 1469598103934665603ULL;
    for (int i = 0; i < KL; i++) { h ^= k[i]; h *= 1099511628211ULL; }
    h ^= h >> 29; h *= 0xbf58476d1ce4e5b9ULL; h ^= h >> 32;
    return h;
}
static void memo_reset(void) {
    if (table) free(table);
    tcap = 1 << 16; nent = 0;
    table = calloc(tcap, sizeof(uint32_t));
    if (!karena) { kcap = 1 << 20; karena = malloc(kcap); }
    if (!vals) { vcap = 1 << 14; vals = malloc(vcap * sizeof(Val)); }
}
static void grow(void) {
    size_t nc = tcap * 2;
    uint32_t *nt = calloc(nc, sizeof(uint32_t));
    for (size_t i = 0; i < tcap; i++) {
        uint32_t e = table[i];
        if (!e) continue;
        uint64_t h = hashk(karena + (size_t)(e - 1) * KL);
        size_t j = h & (nc - 1);
        while (nt[j]) j = (j + 1) & (nc - 1);
        nt[j] = e;
    }
    free(table); table = nt; tcap = nc;
}
static long memo_find(const uint8_t *k) {
    uint64_t h = hashk(k);
    size_t j = h & (tcap - 1);
    while (table[j]) {
        uint32_t e = table[j];
        if (memcmp(karena + (size_t)(e - 1) * KL, k, KL) == 0) return (long)(e - 1);
        j = (j + 1) & (tcap - 1);
    }
    return -1;
}
static void memo_put(const uint8_t *k, Val v) {
    if ((nent + 1) * 2 > tcap) grow();
    size_t need = (nent + 1) * KL;
    if (need > kcap) { while (need > kcap) kcap *= 2; karena = realloc(karena, kcap); }
    if (nent + 1 > vcap) { vcap *= 2; vals = realloc(vals, vcap * sizeof(Val)); }
    memcpy(karena + nent * KL, k, KL);
    vals[nent] = v;
    uint64_t h = hashk(k);
    size_t j = h & (tcap - 1);
    while (table[j]) j = (j + 1) & (tcap - 1);
    table[j] = (uint32_t)(nent + 1);
    nent++;
}

// --------------- state ------------------
// layout: [0]=cur [1]=prev(255=-1) [2]=trig [3]=fks lo [4]=fks hi [5]=step [6..]=counts
#define CUR(s) ((s)[0])
#define PRV(s) ((s)[1])
#define TRIG(s) ((s)[2])
#define FKS(s) ((int)(s)[3] | ((int)(s)[4] << 8))
#define STEP(s) ((s)[5])
#define CNT(s) ((s) + 6)

static int is_partner(int a, int b) {
    for (int i = 0; i < npartner[a]; i++) if (partner[a][i] == b) return 1;
    return 0;
}

static void canon(const uint8_t *s, uint8_t *k) {
    memcpy(k, s, KL);
    int cur = s[0], prev = s[1] == 255 ? -1 : s[1];
    const uint8_t *c = CNT(s);
    if (!(npartner[cur] && prev >= 0 && is_partner(cur, prev) && c[prev] > 0)) k[1] = 255;
    int fks = FKS(s), pending = 0;
    for (int i = 0; i < nfk; i++) { int t = fk_list[i]; if (c[t] > 0 && !(fks & fkbitv[t])) { pending = 1; break; } }
    for (int i = 0; i < ndl; i++) if (!(fks & dl_bit[i])) pending = 1;
    if (!pending) { k[3] = 0; k[4] = 0; k[5] = 0; }
}

static int live(const uint8_t *s, int i) {
    const uint8_t *c = CNT(s);
    return c[i] > 0 && (!hidbit[i] || (hidbit[i] & TRIG(s)));
}

static int legal_targets(const uint8_t *s, int *res) {
    int cur = CUR(s), k = 0, fks = FKS(s), step = STEP(s);
    for (int a = 0; a < mnbc[cur]; a++) {
        int t = mnb[cur][a];
        if (!live(s, t)) continue;
        if (scexit[t] != -2 && scexit[t] == cur) continue;
        if (scexit[cur] != -2 && scexit[cur] != t) continue;
        if (fkorder[t] > 0 && !(fks & fkbitv[t]) && step + 1 != fkorder[t]) continue;
        res[k++] = t;
    }
    return k;
}

// returns 0 dead, 1 win, 2 state (written to o)
static int apply(const uint8_t *s, int t, int released, uint8_t *o) {
    memcpy(o, s, KL);
    int cur = CUR(s), prev = PRV(s) == 255 ? -1 : PRV(s);
    uint8_t *c = CNT(o);
    c[cur] -= 1;
    if (npartner[cur]) {
        int pv = released ? -1 : prev;
        for (int i = 0; i < npartner[cur]; i++) {
            int q = partner[cur][i];
            if (q == t || q == pv) continue;
            if (c[q] > 0) c[q] -= 1;
        }
    }
    if (iscb[cur]) {
        for (int a = 0; a < enbc[cur]; a++) { int m = enb[cur][a]; if (m != t && c[m] > 0) c[m] -= 1; }
    }
    if (ischarge[cur]) {
        for (int a = 0; a < enbc[cur]; a++) {
            int m = enb[cur][a];
            if (m != t && c[m] > 0 && chargeable[m]) { if (c[m] >= 250) { overflow_flag = 1; return 0; } c[m] += 1; }
        }
    }
    int step = STEP(s) + 1;
    if (step > 255) { overflow_flag = 1; return 0; }
    int fks = FKS(s);
    int trig = TRIG(s) | ignbit[t];
    if (fkorder[t] > 0) fks |= fkbitv[t];
    for (int i = 0; i < ndl; i++) if (dl_tile[i] == t) fks |= dl_bit[i];
    o[0] = (uint8_t)t; o[1] = (uint8_t)cur; o[2] = (uint8_t)trig; o[3] = fks & 255; o[4] = (fks >> 8) & 255; o[5] = (uint8_t)step;
    for (int i = 0; i < nfk; i++) {
        int k = fk_list[i];
        if (c[k] > 0 && !(fks & fkbitv[k]) && step >= fkorder[k]) return 0;
    }
    for (int i = 0; i < ndl; i++) if (!(fks & dl_bit[i]) && step >= dl_K[i]) return 0;
    int total = 0;
    for (int i = 0; i < n; i++) total += c[i];
    if (c[t] == 1) {
        if (total == 1) return 1;
        if (npartner[t]) {
            int ok = 1, sum = 1;
            for (int i = 0; i < npartner[t]; i++) {
                int q = partner[t][i];
                if (c[q] > 0) { if (c[q] != 1) { ok = 0; break; } sum += c[q]; }
            }
            if (ok && total == sum) return plainwin ? 0 : 1;
        }
    }
    return 2;
}

static int quick_dead(const uint8_t *s) {
    const uint8_t *c = CNT(s);
    for (int i = 0; i < nremote; i++) if (c[remote_list[i]] > 0) return 0;
    int ign_avail = TRIG(s);
    for (int i = 0; i < n; i++) if (ignbit[i] && c[i] > 0) ign_avail |= ignbit[i];
    uint64_t passable = 0;
    for (int i = 0; i < n; i++) if (c[i] > 0 && (!hidbit[i] || (hidbit[i] & ign_avail))) passable |= 1ULL << i;
    int cur = CUR(s);
    uint64_t seen = 1ULL << cur;
    int stack[MAXN], sp = 0; stack[sp++] = cur;
    while (sp) {
        int u = stack[--sp];
        for (int a = 0; a < mnbc[u]; a++) {
            int v = mnb[u][a];
            if (!(seen >> v & 1) && (passable >> v & 1)) { seen |= 1ULL << v; stack[sp++] = v; }
        }
    }
    int R = 0, same = 0, c0 = color[cur];
    for (int i = 0; i < n; i++) if (c[i] > 0) {
        if (!(seen >> i & 1)) return 1;
        R += c[i];
        if (color[i] == c0) same += c[i];
    }
    if (same != (R + 1) / 2) return 1;
    int ends = 0;
    for (int t = 0; t < n; t++) {
        if (t == cur || c[t] == 0) continue;
        int nu = 0, u = -1;
        for (int a = 0; a < mnbc[t]; a++) { int v = mnb[t][a]; if (c[v] > 0) { nu++; u = v; } }
        if (nu == 1) {
            if (c[u] < c[t]) return 1;
            if (c[u] == c[t]) { ends++; if (ends > 1) return 1; }
        }
    }
    return 0;
}

static long long nodes; static int aborted;
static const Val WINV = {1, 1.0, 0, 0, 0, 0, 0, 0, 0};
static const Val DEADV = {0, 0.0, 0, 0, 0, 0, 0, BIGL, -1};

// enumerate actions: targets + released flags + outcomes
static int actions(const uint8_t *s, int *tg, int *rl, int *oc, uint8_t *obuf) {
    int res[4];
    int k = legal_targets(s, res), m = 0;
    int cur = CUR(s), prev = PRV(s) == 255 ? -1 : PRV(s);
    const uint8_t *c = CNT(s);
    for (int i = 0; i < k; i++) {
        int t = res[i];
        tg[m] = t; rl[m] = 0; oc[m] = apply(s, t, 0, obuf + m * KL); m++;
        if (!norelease && npartner[cur] && prev >= 0 && is_partner(cur, prev) && prev != t && c[prev] > 0) {
            tg[m] = t; rl[m] = 1; oc[m] = apply(s, t, 1, obuf + m * KL); m++;
        }
    }
    return m;
}

static Val rec(const uint8_t *s) {
    uint8_t key[MAXN + 8];
    canon(s, key);
    long f = memo_find(key);
    if (f >= 0) return vals[f];
    if (aborted) return DEADV;
    nodes++;
    if (nodes > state_limit) { aborted = 1; return DEADV; }
    if (quick_dead(s)) { memo_put(key, DEADV); return DEADV; }
    int tg[8], rl[8], oc[8];
    uint8_t obuf[8 * (MAXN + 8)];
    int legal = actions(s, tg, rl, oc, obuf);
    if (legal == 0) { memo_put(key, DEADV); return DEADV; }
    Val ch[8];
    for (int i = 0; i < legal; i++) {
        if (oc[i] == 1) ch[i] = WINV;
        else if (oc[i] == 0) ch[i] = DEADV;
        else ch[i] = rec(obuf + i * KL);
        if (aborted) return DEADV;
    }
    int good = 0;
    for (int i = 0; i < legal; i++) if (ch[i].N > 0) good++;
    double P = 0;
    for (int i = 0; i < legal; i++) P += ch[i].P;
    P = P / legal;
    Val v = {0, P, 0, 0, 0, 0, 0, BIGL, -1};
    int forced = legal == 1, fr = legal > 1 && good == legal, cr = legal > 1 && good < legal;
    for (int i = 0; i < legal; i++) {
        Val c = ch[i];
        if (c.N == 0) continue;
        v.N += c.N;
        v.F += c.F + (forced ? c.N : 0);
        v.Fr += c.Fr + (fr ? c.N : 0);
        v.Cr += c.Cr + (cr ? c.N : 0);
        v.L += c.L + c.N;
        v.LS += c.LS + c.N * legal;
        if (c.mn + 1 < v.mn) v.mn = c.mn + 1;
        if (c.mx + 1 > v.mx) v.mx = c.mx + 1;
    }
    memo_put(key, v);
    return v;
}

static int read_ints(int *a, int k) {
    for (int i = 0; i < k; i++) if (scanf("%d", &a[i]) != 1) return 0;
    return 1;
}

int main(void) {
    char word[64];
    while (scanf("%63s", word) == 1) {
        if (strcmp(word, "STAGE") != 0) { fprintf(stderr, "bad input %s\n", word); return 1; }
        char sid[128];
        long long lim;
        if (scanf("%127s %d %d %d %lld", sid, &n, &start_tile, &norelease, &lim) != 5) return 1;
        state_limit = lim;
        plainwin = (norelease >> 1) & 1;
        norelease = norelease & 1;
        KL = n + 6;
        nfk = 0; ndl = 0; nremote = 0; overflow_flag = 0;
        int fkcount = 0;
        for (int i = 0; i < n; i++) {
            int v[12];
            // count type(code) scexit fkorder color hidbit ignbit iscb ischarge chargeable npartner
            if (!read_ints(v, 11)) return 1;
            cnt0[i] = v[0]; scexit[i] = v[2]; fkorder[i] = v[3]; color[i] = v[4];
            hidbit[i] = v[5]; ignbit[i] = v[6]; iscb[i] = v[7]; ischarge[i] = v[8]; chargeable[i] = v[9];
            npartner[i] = v[10];
            if (!read_ints(partner[i], npartner[i])) return 1;
            if (!read_ints(&mnbc[i], 1) || !read_ints(mnb[i], mnbc[i])) return 1;
            if (!read_ints(&enbc[i], 1) || !read_ints(enb[i], enbc[i])) return 1;
            fkbitv[i] = 0;
            if (fkorder[i] > 0) { fkbitv[i] = 1 << fkcount; fk_list[nfk++] = i; fkcount++; }
            if (v[1] == 2 || v[1] == 4 || v[1] == 9) remote_list[nremote++] = i;
        }
        int nd;
        if (scanf("%d", &nd) != 1) return 1;
        for (int i = 0; i < nd; i++) {
            int bi;
            if (scanf("%d %d %d", &dl_tile[i], &dl_K[i], &bi) != 3) return 1;
            dl_bit[i] = 1 << bi;
        }
        ndl = nd;
        memo_reset();
        nodes = 0; aborted = 0;
        uint8_t root[MAXN + 8];
        memset(root, 0, sizeof root);
        root[0] = start_tile; root[1] = 255; root[2] = ignbit[start_tile];
        for (int i = 0; i < n; i++) root[6 + i] = cnt0[i];
        Val r = rec(root);
        printf("{\"id\": \"%s\", \"states\": %lld, \"overflow\": %d", sid, nodes, overflow_flag);
        if (aborted) {
            printf(", \"complete\": false}\n");
        } else {
            printf(", \"complete\": true, \"solvable\": %s, \"solutions\": %.17g, \"p_random\": %.17g", r.N > 0 ? "true" : "false", r.N, r.P);
            if (r.N > 0) {
                printf(", \"forced_ratio\": %.17g, \"free_ratio\": %.17g, \"crit_ratio\": %.17g, \"avg_len\": %.17g, \"avg_branch\": %.17g, \"crit_steps\": %.17g, \"min_len\": %d, \"max_len\": %d",
                       r.F / r.L, r.Fr / r.L, r.Cr / r.L, r.L / r.N, r.LS / r.L, r.Cr / r.N, r.mn, r.mx);
                // path
                printf(", \"path\": [%d", start_tile);
                uint8_t st[MAXN + 8], key[MAXN + 8];
                memcpy(st, root, KL);
                int rels[1024], nr = 0;
                for (int guard = 0; guard < 1000; guard++) {
                    int tg[8], rl[8], oc[8];
                    uint8_t obuf[8 * (MAXN + 8)];
                    int m = actions(st, tg, rl, oc, obuf), pick = -1;
                    for (int i = 0; i < m; i++) {
                        if (oc[i] == 1) { pick = i; break; }
                        if (oc[i] == 2) { canon(obuf + i * KL, key); long f = memo_find(key); if (f >= 0 && vals[f].N > 0) { pick = i; break; } }
                    }
                    if (pick < 0) { printf(", -1"); break; }
                    printf(", %d", tg[pick]);
                    rels[nr++] = rl[pick];
                    if (oc[pick] == 1) break;
                    memcpy(st, obuf + pick * KL, KL);
                }
                printf("], \"released\": [");
                for (int i = 0; i < nr; i++) printf(i ? ", %d" : "%d", rels[i]);
                printf("]");
            }
            printf("}\n");
        }
        fflush(stdout);
    }
    return 0;
}
