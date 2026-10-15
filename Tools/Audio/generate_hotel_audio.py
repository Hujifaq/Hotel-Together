#!/usr/bin/env python3
"""Offline synthesiser for the Hotel Together sound set.

Run:  python generate_hotel_audio.py [output_dir]
Writes 16-bit WAVs (SFX mono 32 kHz, music stereo 32 kHz) into
Assets/HotelTogether/Resources/HotelTogetherAudio by default.
Replace any file with your own recording using the same name and it will be used instead.
Needs numpy + scipy.
"""
import os, sys, wave
import numpy as np
from scipy import signal

SR = 32000
RNG = np.random.default_rng(7)
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "HotelTogether", "Resources", "HotelTogetherAudio")
os.makedirs(OUT, exist_ok=True)

def t_(d): return np.arange(int(SR * d)) / SR
def decay(n, tau, attack=.003):
    t = np.arange(n) / SR
    return np.minimum(1, t / attack) * np.exp(-t / tau)
def sine(f, d, ph=0): t = t_(d); f = np.broadcast_to(f, t.shape); return np.sin(2 * np.pi * np.cumsum(f) / SR + ph)
def noise(d): return RNG.uniform(-1, 1, int(SR * d))
def lp(x, fc, o=2): return signal.lfilter(*signal.butter(o, min(fc, SR / 2 - 100) / (SR / 2), "low"), x)
def hp(x, fc, o=2): return signal.lfilter(*signal.butter(o, fc / (SR / 2), "high"), x)
def bp(x, lo, hi, o=2): return signal.lfilter(*signal.butter(o, [lo / (SR / 2), min(hi, SR / 2 - 100) / (SR / 2)], "band"), x)
def sweep(f0, f1, d, curve=1.0):
    t = t_(d); k = (t / d) ** curve; return f0 + (f1 - f0) * k
def pad(x, d): out = np.zeros(int(SR * d)); out[:min(len(x), len(out))] = x[:len(out)]; return out
def mix(*parts, d=None):
    n = max(len(p[0]) + int(p[1] * SR) for p in parts) if d is None else int(d * SR)
    out = np.zeros(n)
    for p in parts:
        x, off = p[0], int(p[1] * SR); g = p[2] if len(p) > 2 else 1
        m = min(len(x), n - off)
        if m > 0: out[off:off + m] += x[:m] * g
    return out
def reverb(x, amt=.2, rt=.35):
    n = int(SR * rt); ir = RNG.normal(size=n) * np.exp(-np.arange(n) / SR / (rt / 5)); ir = lp(ir, 5000)
    ir[0] = 0; wet = signal.fftconvolve(x, ir)[:len(x) + n]; out = np.zeros(len(wet)); out[:len(x)] += x
    return out + wet * amt * 0.1
def tanh_(x, g=1.0): return np.tanh(x * g)
def fm_bell(f, d, ratio=3.5, idx=4, tau=None):
    t = t_(d); tau = tau or d / 4; e = np.exp(-t / tau)
    return np.sin(2 * np.pi * f * t + idx * e * np.sin(2 * np.pi * f * ratio * t)) * e
def pluck(f, d, bright=3000, tau=.18):
    t = t_(d); e = np.exp(-t / tau)
    x = sum(np.sin(2 * np.pi * f * k * t) / k ** 1.6 * np.exp(-t / (tau / (1 + .5 * k))) for k in range(1, 7))
    return lp(x, bright) * np.minimum(1, t / .002)
def marimba(f, d=.5):
    t = t_(d); return (np.sin(2 * np.pi * f * t) * np.exp(-t / .12) + .35 * np.sin(2 * np.pi * f * 4 * t) * np.exp(-t / .04)) * np.minimum(1, t / .002)
def fade_edges(x, ms=3):
    n = int(SR * ms / 1000); x = x.copy(); x[:n] *= np.linspace(0, 1, n); x[-n:] *= np.linspace(1, 0, n); return x
def norm(x, peak=.8): m = np.max(np.abs(x)) + 1e-9; return x / m * peak
def save(name, x, peak=.8, stereo=False):
    x = norm(x, peak) if peak else x
    if not stereo: x = fade_edges(x)
    data = (np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(2 if stereo else 1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(data.tobytes())
    print("wrote", name, round(len(x) / SR, 2), "s")

def midi(n): return 440 * 2 ** ((n - 69) / 12)

# ---------------------------------------------------------------- UI
save("ui_click", mix((marimba(midi(84), .12), 0), (lp(noise(.02), 6000) * decay(int(.02 * SR), .005), 0, .3)), .7)
save("ui_hover", marimba(midi(91), .08) * .8, .35)
save("ui_back", mix((marimba(midi(76), .14), 0), (marimba(midi(71), .16), .06)), .7)
save("ui_open", mix(*[(marimba(midi(n), .3), i * .055) for i, n in enumerate([72, 76, 79, 84])]), .7)
save("ui_close", mix(*[(marimba(midi(n), .25), i * .05) for i, n in enumerate([84, 79, 74])]), .65)
save("toast", mix((fm_bell(midi(88), .35, 2, 1.5, .09), 0), (fm_bell(midi(95), .3, 2, 1.2, .07), .07, .7)), .55)
a = fm_bell(midi(81), .22, 1.0, 2, .1); b = fm_bell(midi(81), .22, 1.0, 2, .1)
save("toast_urgent", mix((a, 0), (b, .16), (lp(a, 1800), .32, .9)), .7)
_d = sweep(240, 140, .3); _w = sine(_d, .3)
save("deny", lp(_w * decay(int(.3 * SR), .15) + .35 * np.sign(_w) * decay(int(.3 * SR), .1), 1400), .6)

# ---------------------------------------------------------------- economy
def coin(f1=2093, f2=2794, d=.45):
    return mix((fm_bell(f1, d, 3.01, 1.2, .1), 0), (fm_bell(f2, d, 3.01, 1.0, .18), .07), (hp(noise(.03), 5000) * decay(int(.03 * SR), .006), 0, .25))
save("coin", coin(), .75)
save("coin_big", mix(*[(coin(f, f * 1.333, .4), i * .07, .8) for i, f in enumerate([1760, 2093, 2349, 2637])], (marimba(midi(60), .5), 0, .3)), .8)
save("coin_loss", mix((sine(sweep(700, 300, .3), .3) * decay(int(.3 * SR), .12), 0), (fm_bell(300, .3, 1.4, 2, .1), .02, .5)), .6)
save("buy", mix((coin(), 0), *[(marimba(midi(n), .4), .09 + i * .07, .8) for i, n in enumerate([72, 76, 79, 84])]), .8)
def fanfare(notes, step, tail=.5):
    parts = []
    for i, n in enumerate(notes):
        parts += [(pluck(midi(n), tail, 4500, .2), i * step), (marimba(midi(n + 12), tail), i * step, .45)]
    return mix(*parts)
save("goal", reverb(fanfare([72, 76, 79, 84, 88], .085, .7), 1.2), .8)
save("checkin", reverb(mix((fm_bell(midi(84), .5, 3.5, 2, .2), 0), (fm_bell(midi(88), .6, 3.5, 2, .25), .12), (marimba(midi(72), .5), 0, .5)), 1), .7)
save("cha_ching", mix((hp(noise(.05), 3000) * decay(int(.05 * SR), .015), 0), (fm_bell(midi(96), .6, 5.43, 2, .25), .05), (fm_bell(midi(103), .7, 5.43, 2, .3), .17), (lp(noise(.12), 900) * decay(int(.12 * SR), .03), 0, .4)), .8)

# ---------------------------------------------------------------- hotel / world
def thump(f=120, d=.14, tau=.04):
    return sine(sweep(f * 1.6, f * .6, d), d) * decay(int(d * SR), tau)
save("pickup", mix((sine(sweep(500, 900, .1), .1) * decay(int(.1 * SR), .04), 0), (lp(noise(.05), 3000) * decay(int(.05 * SR), .012), 0, .35)), .6)
save("drop", mix((thump(150, .18, .05), 0), (lp(noise(.08), 1600) * decay(int(.08 * SR), .02), 0, .5)), .75)
save("throw", mix((bp(noise(.28), 400, 3000) * np.hanning(int(.28 * SR)) , 0)) , .5)
for i in range(4):
    save(f"step_{i}", mix((thump(95 + i * 9, .1, .03), 0), (lp(noise(.06), 1100 + i * 140) * decay(int(.06 * SR), .014), 0, .8)), .55)
save("land", mix((thump(70, .22, .07), 0), (lp(noise(.12), 900) * decay(int(.12 * SR), .03), 0, .8)), .75)
save("impact_soft", mix((thump(130, .16, .04), 0), (lp(noise(.09), 2400) * decay(int(.09 * SR), .02), 0, .7)), .7)
hard = mix((thump(90, .3, .08), 0), (hp(noise(.15), 1200) * decay(int(.15 * SR), .03), 0, .6), (fm_bell(900, .3, 2.7, 3, .05), .01, .25), (fm_bell(1370, .25, 2.7, 3, .04), .02, .2))
save("impact_hard", hard, .85)
save("door_open", mix((lp(noise(.35), 800) * np.hanning(int(.35 * SR)) * .5, 0), (thump(190, .08, .02), .0, .6), (sine(sweep(260, 300, .3), .3) * np.hanning(int(.3 * SR)) * .1, 0)), .55)
save("door_close", mix((lp(noise(.3), 700) * np.hanning(int(.3 * SR)) * .35, 0), (thump(100, .22, .07), .22, 1.0), (lp(noise(.06), 2000) * decay(int(.06 * SR), .015), .22, .6)), .7)
bell1 = mix(*[(fm_bell(f, 1.4, 1.0, .6, .45) * g, 0) for f, g in [(2093, 1), (2093 * 2.76, .35), (2093 * 5.4, .18)]], (hp(noise(.02), 3000) * decay(int(.02 * SR), .005), 0, .4))
save("bell", reverb(bell1, 1, .4), .85)
save("elevator", reverb(mix((fm_bell(midi(79), .9, 1.0, .5, .35), 0), (fm_bell(midi(74), 1.1, 1.0, .5, .4), .4)), 1), .7)
save("knock", mix(*[(mix((thump(210, .1, .02), 0), (lp(noise(.05), 1300) * decay(int(.05 * SR), .01), 0, .6)), i * .17) for i in range(3)]), .8)
save("hammer", mix((thump(260, .12, .025), 0), (hp(noise(.05), 2500) * decay(int(.05 * SR), .01), 0, .6), (fm_bell(1500, .25, 3.2, 2, .04), 0, .25)), .8)
save("gunshot", mix((lp(noise(.4), 2800) * decay(int(.4 * SR), .06, .0005), 0), (thump(100, .3, .09), 0, 1.2), (hp(noise(.03), 4000) * decay(int(.03 * SR), .006), 0, .8)), .9)
save("reload", mix((thump(300, .06, .015), 0, .8), (hp(noise(.04), 3500) * decay(int(.04 * SR), .01), .02, .6), (thump(240, .07, .02), .35), (hp(noise(.05), 3500) * decay(int(.05 * SR), .012), .37, .6)), .75)

# ---------------------------------------------------------------- zombies
def groan(d, f0, seed):
    r = np.random.default_rng(seed); t = t_(d)
    vib = 1 + .03 * np.sin(2 * np.pi * (5 + r.uniform(-1, 1)) * t)
    f = (f0 * (1 + .18 * np.sin(np.pi * t / d)) ) * vib
    ph = 2 * np.pi * np.cumsum(f) / SR
    x = sum(np.sin(ph * k) / k ** 1.1 for k in range(1, 14)) + .6 * noise(d) * .5
    f1 = bp(x, 450, 900) * 1.0 + bp(x, 1100, 1900) * .5 + lp(x, 250) * .5
    return tanh_(f1 * 1.6) * np.sin(np.pi * np.clip(t / d, 0, 1)) ** .7
for i, (d, f0) in enumerate([(1.1, 78), (1.4, 64), (.9, 92)]):
    save(f"zombie_groan_{i}", reverb(groan(d, f0, 11 + i), .6, .3), .8)
save("zombie_attack", mix((groan(.35, 120, 5) * 1.0, 0), (bp(noise(.3), 300, 2200) * np.hanning(int(.3 * SR)) * .6, 0)), .85)
save("zombie_hit", mix((thump(110, .18, .05), 0), (lp(noise(.12), 1800) * decay(int(.12 * SR), .03), 0, .9), (groan(.28, 140, 3), .02, .5)), .85)
save("zombie_die", mix((groan(.8, 100, 2) * np.linspace(1, 0, int(.8 * SR)), 0), (thump(70, .35, .1), .35, 1.0), (lp(noise(.2), 800) * decay(int(.2 * SR), .06), .38, .5)), .85)
save("player_hurt", mix((sine(sweep(420, 260, .22), .22) * decay(int(.22 * SR), .1), 0, .5), (thump(120, .2, .06), 0), (lp(noise(.1), 1500) * decay(int(.1 * SR), .03), 0, .6)), .85)

# ---------------------------------------------------------------- stings
def swell(d, f, g=1):
    t = t_(d); e = np.sin(np.pi * t / d) ** 2
    return sum(np.sin(2 * np.pi * f * k * t + .3 * k) / k for k in (1, 2, 3)) * e * g
night_sting = mix((swell(2.6, midi(45)), 0), (swell(2.6, midi(51)), 0, .8), (fm_bell(midi(69), 2.5, 1.41, 4, .9), .0, .5), (fm_bell(midi(63), 2.5, 1.41, 4, .9), .35, .4), (thump(55, .9, .3), 1.2, 1.0))
save("sting_night", reverb(night_sting, 1.3, .8), .9)
dawn = fanfare([67, 72, 76, 79, 84], .13, 1.2)
dawn = mix((dawn, 0), (swell(2.0, midi(60), .5), 0, .5))
save("sting_dawn", reverb(dawn, 1.3, .7), .85)
save("sting_day", reverb(fanfare([72, 79, 76, 84], .1, .9), 1, .5), .8)

# ---------------------------------------------------------------- music

def build_stereo(bpm, bars, builder):
    beat = 60 / bpm; n = int(bars * 4 * beat * SR); tail = int(2.5 * SR)
    L = np.zeros(n + tail); R = np.zeros(n + tail)
    def put(x, when, pan=0, g=1):
        i = int(when * SR); m = min(len(x), len(L) - i)
        if m <= 0: return
        L[i:i + m] += x[:m] * g * (1 - max(0, pan)); R[i:i + m] += x[:m] * g * (1 + min(0, pan))
    builder(put, beat, bars)
    L2 = L[:n].copy(); R2 = R[:n].copy(); L2[:tail] += L[n:n + tail]; R2[:tail] += R[n:n + tail]
    L2 = reverb_loop(L2); R2 = reverb_loop(np.roll(R2, 37))
    st = np.stack([L2, R2], 1); st = st / np.max(np.abs(st)) * .55
    return st

def reverb_loop(x, amt=.5):
    n = len(x); ir_n = int(SR * .6); ir = RNG.normal(size=ir_n) * np.exp(-np.arange(ir_n) / SR / .11); ir = lp(ir, 4500); ir[0] = 0
    wet = signal.fftconvolve(np.tile(x, 2), ir)[n:2 * n]
    return x + wet * amt * .08

def day_music(put, beat, bars):
    prog = [(48, [60, 64, 67]), (45, [57, 60, 64]), (41, [57, 60, 65]), (43, [55, 59, 62])]
    pent = [72, 74, 76, 79, 81, 84, 86]
    r = np.random.default_rng(3)
    for bar in range(bars):
        root, chord = prog[bar % 4]; t0 = bar * 4 * beat
        for b in range(4):  # walking marimba bass
            note = [root, root + 7, root + 12, root + 7][b] if bar % 2 == 0 else [root, root + 12, root + 7, root + 5][b]
            put(marimba(midi(note), .5) * 1.1, t0 + b * beat, 0, .9)
        for b in range(8):  # off-beat ukulele chops
            if b % 2 == 1:
                for k, c in enumerate(chord):
                    put(pluck(midi(c), .22, 3500, .09), t0 + b * beat / 2 + k * .008, -.4 if k % 2 else .4, .22)
        for b in range(16):  # shaker
            put(hp(noise(.05), 6000) * decay(int(.05 * SR), .012), t0 + b * beat / 4, .3 if b % 2 else -.3, .12 if b % 4 else .2)
        put(thump(70, .2, .08) * .9, t0, 0, .7); put(thump(70, .2, .08) * .9, t0 + 2 * beat, 0, .6)
        put(hp(noise(.12), 1500) * decay(int(.12 * SR), .03) * .6 + lp(noise(.12), 900) * decay(int(.12 * SR), .03) * .3, t0 + beat, 0, .35)
        put(hp(noise(.12), 1500) * decay(int(.12 * SR), .03) * .6 + lp(noise(.12), 900) * decay(int(.12 * SR), .03) * .3, t0 + 3 * beat, 0, .35)
        if bar >= 4:  # bell melody
            pos = 0.0
            while pos < 4:
                step = r.choice([.5, .5, 1, .75]) ; 
                if r.random() < .78:
                    n = int(r.choice(pent)); n = n if (n % 12) in [(c % 12) for c in chord] or r.random() < .5 else min(pent, key=lambda p: abs(p - n) + (0 if (p % 12) in [(c % 12) for c in chord] else 5))
                    put(fm_bell(midi(n), .6, 3.0, 1.0, .16) * .8, t0 + pos * beat, .25 if int(pos * 2) % 2 else -.25, .25)
                pos += step
        if bar % 8 == 7:
            for k, n in enumerate([84, 88, 91, 96]): put(marimba(midi(n), .35), t0 + 3 * beat + k * beat / 4, 0, .25)

def night_music(put, beat, bars):
    prog = [(33, [57, 60, 64]), (29, [53, 57, 60]), (26, [50, 53, 57]), (28, [52, 56, 59])]
    r = np.random.default_rng(9)
    for bar in range(bars):
        root, chord = prog[bar % 4]; t0 = bar * 4 * beat
        saw = lambda f, d: lp(signal.sawtooth(2 * np.pi * f * t_(d)), 380, 2) * decay(int(d * SR), d / 2.2, .004)
        for b in range(8):  # pulsing neon bass
            put(saw(midi(root), beat / 2) * 1.4 + .3 * sine(midi(root), beat / 2) * decay(int(beat / 2 * SR), .2), t0 + b * beat / 2, 0, .75 if b % 2 == 0 else .5)
        t = t_(4 * beat); padg = sum(np.sin(2 * np.pi * midi(c) * t + k) for k, c in enumerate(chord)) * np.sin(np.pi * t / (4 * beat)) ** 1.5
        put(lp(padg, 1400) * .35, t0, 0, .5)
        for b in range(4):
            put(thump(55, .3, .1) * 1.1, t0 + b * beat, 0, .7 if b % 2 == 0 else .0)
        for b in range(8):
            put(hp(noise(.04), 7000) * decay(int(.04 * SR), .008), t0 + b * beat / 2 + (beat / 4 if b % 2 else 0) * 0, .3 if b % 2 else -.3, .14)
        put(hp(noise(.2), 1200) * decay(int(.2 * SR), .05) * .6 + lp(noise(.2), 800) * decay(int(.2 * SR), .06) * .3, t0 + beat, 0, .35)
        put(hp(noise(.2), 1200) * decay(int(.2 * SR), .05) * .6 + lp(noise(.2), 800) * decay(int(.2 * SR), .06) * .3, t0 + 3 * beat, 0, .35)
        for k, c in enumerate(chord):  # staccato stabs on the "and" of 2
            put(pluck(midi(c + 12), .3, 2200, .1), t0 + 1.5 * beat, .4 if k % 2 else -.4, .18)
        if bar >= 4 and bar % 2 == 0:  # eerie theremin lead
            n = [chord[2] + 12, chord[1] + 24, chord[0] + 24, chord[2] + 19][(bar // 2) % 4]; d = 2.6 * beat; t = t_(d)
            f = midi(n) * (1 + .012 * np.sin(2 * np.pi * 5.2 * t) * np.minimum(1, t / .5)); lead = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.sin(np.pi * t / d) ** 1.4
            put(lead, t0 + .5 * beat, .1, .28)
        if bar % 4 == 3:
            for k, n in enumerate([81, 76, 72, 69]): put(fm_bell(midi(n), 1.0, 1.41, 3, .3), t0 + 3 * beat + k * beat / 4 - .02, -.2, .18)

save("music_day", build_stereo(122, 16, day_music), 0, True)
save("music_night", build_stereo(104, 16, night_music), 0, True)

# ---------------------------------------------------------------- footsteps v4 (Roblox-style short plastic "tuk")
def tuk(f, lo, hi, d, seed, tick):
    r = np.random.default_rng(seed); n = int(d * SR)
    thud = sine(sweep(f * 1.4, f * .75, d), d) * decay(n, .022, .0008)
    body = bp(r.uniform(-1, 1, n), lo, hi, 2) * decay(n, .016, .0006)
    t = bp(r.uniform(-1, 1, int(.01 * SR)), 2200, 5200) * decay(int(.01 * SR), .002, .0004) * tick
    return mix((thud, 0, .85), (body, 0, 1.0), (t, .001), d=d)
for i in range(4):  # walking: soft, hollow
    save(f"walk_{i}", tuk(105 + i * 9, 250 + i * 40, 2200 + i * 150, .1, 40 + i, .25), .55)
for i in range(4):  # running: same character, a bit brighter and punchier
    save(f"run_{i}", tuk(130 + i * 10, 350 + i * 40, 3000 + i * 200, .09, 60 + i, .4), .7)
