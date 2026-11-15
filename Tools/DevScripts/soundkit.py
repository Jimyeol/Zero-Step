"""Sound synthesis kit for Zero Step (pure Python, no external audio except the bundled piano notes).

One "palette" = one instrument family used for every sound in the game, so nothing sounds out of place:
  wood    - marimba/kalimba-like mallet (warm, round; suits the clay theme)
  bubble  - soft sine "pop" with a tiny pitch drop (toy / soap-bubble)
  piano   - the grand-piano notes in samples/piano (CC0 VCSL Steinway B), shortened
Rules (see plan): only the melody is pitched; tile effects are unpitched (thump, whoosh, sparkle, click);
celebrations are chords in C major built from the palette instrument.
"""
import math, os, random, struct, wave

RATE = 44100
HERE = os.path.dirname(os.path.abspath(__file__))
_rng = random.Random(11)

NOTE_NAMES = ['c', 'cs', 'd', 'ds', 'e', 'f', 'fs', 'g', 'gs', 'a', 'as', 'b', 'co']


def hz(midi):
    return 440.0 * 2 ** ((midi - 69) / 12)


def silence(seconds):
    return [0.0] * int(seconds * RATE)


def mix(parts, total):
    """parts: [(start_seconds, signal, gain)]"""
    buf = [0.0] * int(total * RATE)
    for start, sig, gain in parts:
        o = int(start * RATE)
        for i, v in enumerate(sig):
            if 0 <= o + i < len(buf):
                buf[o + i] += v * gain
    return buf


def normalize(x, peak=0.7):
    m = max(1e-9, max(abs(v) for v in x))
    return [v * peak / m for v in x]


def fade_out(x, seconds=0.02):
    n = min(len(x), int(seconds * RATE))
    x = list(x)
    for i in range(n):
        x[len(x) - 1 - i] *= i / max(1, n)
    return x


def write(path, x, peak=None):
    if peak is not None:
        x = normalize(x, peak)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, 'wb') as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(RATE)
        frames = bytearray()
        for v in x:
            s = max(-32767, min(32767, int(v * 32767)))
            frames += struct.pack('<hh', s, s)
        w.writeframes(bytes(frames))
    return len(x) / RATE


def read(path):
    with wave.open(path) as w:
        ch, sw, rate, n = w.getnchannels(), w.getsampwidth(), w.getframerate(), w.getnframes()
        raw = w.readframes(n)
    out = []
    for i in range(n):
        s = 0.0
        for c in range(ch):
            b = raw[(i * ch + c) * sw:(i * ch + c + 1) * sw]
            s += int.from_bytes(b, 'little', signed=True) / float(1 << (8 * sw - 1))
        out.append(s / ch)
    if rate != RATE:
        out = _resample(out, rate / RATE)
    return out


def _resample(x, step):
    n = int((len(x) - 2) / step)
    out = []
    for i in range(n):
        p = i * step
        j = int(p)
        f = p - j
        out.append(x[j] + (x[j + 1] - x[j]) * f)
    return out


# ------------------------------------------------------------------------------------------- pitched instruments
def wood(midi, length=0.42, bright=1.0):
    """Marimba-like mallet: fundamental + 4th/10th partials that die fast, soft mallet knock."""
    f = hz(midi)
    n = int(length * RATE)
    parts = [(1.0, 1.0, 6.5), (3.93, 0.32 * bright, 26.0), (9.2, 0.09 * bright, 60.0)]
    out = []
    lp = 0.0
    for i in range(n):
        t = i / RATE
        v = 0.0
        for ratio, amp, decay in parts:
            if f * ratio < RATE / 2.2:
                v += amp * math.sin(2 * math.pi * f * ratio * t) * math.exp(-t * decay)
        noise = _rng.random() * 2 - 1
        lp += (noise - lp) * 0.25
        v += lp * 0.5 * math.exp(-t * 260)
        attack = min(1.0, t / 0.0025)
        out.append(v * attack)
    return fade_out(out, 0.03)


def bubble(midi, length=0.3):
    """Soft sine 'pop': starts a little sharp and settles, gentle 2nd harmonic."""
    f = hz(midi)
    n = int(length * RATE)
    out, ph = [], 0.0
    for i in range(n):
        t = i / RATE
        glide = 1.0 + 0.12 * math.exp(-t * 90)
        ph += 2 * math.pi * f * glide / RATE
        v = math.sin(ph) + 0.22 * math.sin(2 * ph) * math.exp(-t * 30)
        env = min(1.0, t / 0.003) * math.exp(-t * 8.5)
        out.append(v * env)
    return fade_out(out, 0.03)


_piano_cache = {}


def piano(midi, length=0.32):
    """Bundled piano notes (C4..C5 = midi 60..72), shifted by octaves/semitones outside that range."""
    base = midi
    while base > 72:
        base -= 12
    while base < 60:
        base += 12
    name = NOTE_NAMES[base - 60]
    if name not in _piano_cache:
        _piano_cache[name] = read(os.path.join(HERE, 'samples', 'piano', f'piano_{name}.wav'))
    x = _piano_cache[name]
    step = 2 ** ((midi - base) / 12)
    n = int(length * RATE)
    out = []
    for i in range(n):
        p = i * step
        j = int(p)
        if j + 1 >= len(x):
            break
        out.append(x[j] + (x[j + 1] - x[j]) * (p - j))
    return fade_out(out, 0.06)


PALETTES = {
    # name: (instrument, melody octave offset: block 'c' = midi 60 + offset)
    'wood': (wood, 0),
    'bubble': (bubble, 12),
    'piano': (piano, 0),
}


def note(palette, midi, length=None):
    inst, _ = PALETTES[palette]
    return inst(midi) if length is None else inst(midi, length)


def block_midi(palette, name):
    return 60 + PALETTES[palette][1] + NOTE_NAMES.index(name)


def chord(palette, midis, stagger=0.0, length=None, total=None):
    sigs = [note(palette, m, length) for m in midis]
    total = total or (stagger * len(midis) + max(len(s) for s in sigs) / RATE)
    return mix([(k * stagger, s, 1.0) for k, s in enumerate(sigs)], total)


# ------------------------------------------------------------------------------------------- unpitched effects
def thump(length=0.32, f0=150.0, f1=48.0, sweep=30.0, body_decay=11.0, puff=0.6, puff_lp=0.12, puff_decay=20.0):
    """Low soft impact + filtered noise puff (bomb, landing). No stable pitch: the sine sweeps down."""
    n = int(length * RATE)
    out, ph, lp = [], 0.0, 0.0
    for i in range(n):
        t = i / RATE
        f = f1 + (f0 - f1) * math.exp(-t * sweep)
        ph += 2 * math.pi * f / RATE
        body = math.sin(ph) * math.exp(-t * body_decay)
        noise = _rng.random() * 2 - 1
        lp += (noise - lp) * puff_lp
        out.append(body + lp * puff * math.exp(-t * puff_decay) * 3.0)
    return fade_out(out, 0.03)


def whoosh(length=0.22, rising=True, lo=0.02, hi=0.45, gain_shape=1.0):
    """Filtered-noise sweep (charge: rising, blackout: falling)."""
    n = int(length * RATE)
    out, lp, lp2 = [], 0.0, 0.0
    for i in range(n):
        u = i / n
        a = lo + (hi - lo) * (u if rising else 1 - u)
        noise = _rng.random() * 2 - 1
        lp += (noise - lp) * a
        lp2 += (lp - lp2) * a
        env = math.sin(math.pi * u) ** gain_shape
        out.append(lp2 * env)
    return fade_out(out, 0.02)


def sparkle(length=0.3, grains=9, seed=3):
    """Short bright noise ticks (hidden tiles waking up). High-passed, no pitch."""
    r = random.Random(seed)
    n = int(length * RATE)
    out = [0.0] * n
    for g in range(grains):
        o = int(r.random() * (n - 900))
        amp = 0.4 + 0.6 * (1 - o / n)
        prev = 0.0
        for i in range(700):
            noise = r.random() * 2 - 1
            hp = noise - prev
            prev = noise
            out[o + i] += hp * amp * math.exp(-i / RATE * 260)
    return out


def click(length=0.05, lp_coef=0.5, decay=180.0, body=0.0, body_f=900.0):
    """Tiny tick (buttons, order tile). Optional short tonal body for a wooden knock."""
    n = int(length * RATE)
    out, lp = [], 0.0
    for i in range(n):
        t = i / RATE
        noise = _rng.random() * 2 - 1
        lp += (noise - lp) * lp_coef
        v = lp * math.exp(-t * decay)
        if body:
            v += body * math.sin(2 * math.pi * body_f * t) * math.exp(-t * decay * 0.7)
        out.append(v)
    return out


# ------------------------------------------------------------------------------------------- fever pop
PENTA_A_MINOR = [57, 60, 62, 64, 67, 69, 72, 74, 76, 79, 81, 84]


def fever_pop(step):
    """'뿅': short blip that slides up into an A-minor pentatonic note; higher with the combo step."""
    midi = PENTA_A_MINOR[min(step, len(PENTA_A_MINOR) - 1)] + 12
    f = hz(midi)
    n = int(0.11 * RATE)
    out, ph = [], 0.0
    for i in range(n):
        t = i / RATE
        glide = 0.7 + 0.3 * (1 - math.exp(-t * 70))
        ph += 2 * math.pi * f * glide / RATE
        v = math.sin(ph) + 0.18 * math.sin(3 * ph)
        env = min(1.0, t / 0.002) * math.exp(-t * 26)
        out.append(v * env)
    return fade_out(out, 0.015)


# ------------------------------------------------------------------------------------------- fever background loop
def fever_bgm(palette, bars=8, bpm=150, seed=7):
    """150 BPM loop, Am-F-C-G-Am-F-G-E: kick, clap, hats, pumping octave bass, 16th arpeggio in the palette
    instrument, snare roll + riser into the loop point, crash on bar 1. Loops seamlessly."""
    r = random.Random(seed)
    step = 60.0 / bpm / 4
    loop = bars * 16 * step
    total = loop + 1.0
    n = int(total * RATE)
    buf = [0.0] * n
    kicks = []

    def add(start, sig, gain):
        o = int(start * RATE)
        for i, v in enumerate(sig):
            if o + i < n:
                buf[o + i] += v * gain

    def kick():
        out, ph = [], 0.0
        for i in range(int(0.3 * RATE)):
            t = i / RATE
            ph += 2 * math.pi * (46 + 120 * math.exp(-t * 32)) / RATE
            out.append(math.sin(ph) * math.exp(-t * 8.5) + 0.4 * math.exp(-t * 400) * (r.random() * 2 - 1))
        return out

    def clap():
        out, lp = [], 0.0
        for i in range(int(0.2 * RATE)):
            t = i / RATE
            noise = r.random() * 2 - 1
            lp += (noise - lp) * 0.35
            burst = sum(math.exp(-(t - d) * 90) for d in (0.0, 0.011, 0.022) if t >= d)
            out.append((noise - lp) * (0.35 * burst + math.exp(-t * 16)) * 0.7)
        return out

    def hat(length, decay):
        out, prev = [], 0.0
        for i in range(int(length * RATE)):
            noise = r.random() * 2 - 1
            out.append((noise - prev) * math.exp(-i / RATE * decay))
            prev = noise
        return out

    def bass(m, length):
        f = hz(m)
        out, ph, lp = [], 0.0, 0.0
        for i in range(int(length * RATE)):
            t = i / RATE
            ph = (ph + f / RATE) % 1.0
            raw = 0.7 * (2 * ph - 1) + 0.3 * (1.0 if ph < 0.5 else -1.0)
            lp += (raw - lp) * (0.06 + 0.25 * math.exp(-t * 18))
            out.append(lp * min(1.0, t / 0.004) * math.exp(-t * 5.5))
        return out

    chords = [(45, [57, 60, 64]), (41, [53, 57, 60]), (48, [55, 60, 64]), (43, [55, 59, 62]),
              (45, [57, 60, 64]), (41, [53, 57, 60]), (43, [55, 59, 62]), (40, [56, 59, 64])]
    arp = [0, 1, 2, 3, 2, 1, 2, 3, 4, 3, 2, 1, 2, 3, 2, 1]
    octave = 12 if palette == 'bubble' else 0
    K, CL, HC, HO = kick(), clap(), hat(0.045, 95), hat(0.16, 22)
    crash, prev = [], 0.0
    for i in range(int(1.4 * RATE)):
        noise = r.random() * 2 - 1
        crash.append((noise - prev * 0.6) * math.exp(-i / RATE * 3.2))
        prev = noise
    add(0.0, crash, 0.2)
    note_cache = {}
    for bar in range(bars):
        root, tones = chords[bar % len(chords)]
        ext = tones + [t + 12 for t in tones] + [t + 24 for t in tones]
        last = bar == bars - 1
        for s in range(16):
            t = (bar * 16 + s) * step
            if s % 4 == 0 and not (last and s >= 12):
                add(t, K, 0.95)
                kicks.append(t)
            if s in (4, 12) and not (last and s >= 8):
                add(t, CL, 0.5)
            if s % 4 == 2:
                add(t, HO, 0.16)
            elif s % 2 == 1:
                add(t, HC, 0.11)
            if s % 2 == 0:
                add(t, bass(root + (12 if (s // 2) % 2 else 0), step * 2), 0.42)
            m = (ext[arp[s] + 1] if bar % 2 else ext[arp[s]]) + octave
            if m not in note_cache:
                note_cache[m] = note(palette, m, 0.3)
            add(t, note_cache[m], 0.7 if s % 4 == 0 else 0.5)
    roll = (bars - 1) * 16 * step + 8 * step
    for k in range(8):
        add(roll + k * step, CL, 0.22 + 0.05 * k)
    rise, lp = [], 0.0
    m = int(16 * step * RATE)
    for i in range(m):
        u = i / m
        lp += ((r.random() * 2 - 1) - lp) * (0.05 + 0.6 * u)
        rise.append(lp * u * u)
    add((bars - 1) * 16 * step, rise, 0.32)
    duck = [1.0] * n
    for kt in kicks:
        o = int(kt * RATE)
        for i in range(int(0.25 * RATE)):
            if o + i < n:
                duck[o + i] = min(duck[o + i], 1 - 0.35 * math.exp(-i / RATE * 14))
    buf = [v * d for v, d in zip(buf, duck)]
    L = int(loop * RATE)
    out = buf[:L]
    for i in range(n - L):
        out[i] += buf[L + i]
    peak = max(abs(v) for v in out)
    return [math.tanh(1.3 * v / peak) / math.tanh(1.3) * 0.82 for v in out]
