# -*- coding: utf-8 -*-
from PIL import Image
from collections import Counter
import os, json, copy

SRC_PATH = r"C:\Users\Localghost\AppData\Roaming\Cursor\User\workspaceStorage\empty-window\images\Wall 1 Original 160 x 111 px-9f16d0c5-ed42-4ec1-b49a-58b4d7e372ed.png"
REF_PATH = r"C:\Users\Localghost\AppData\Roaming\Cursor\User\workspaceStorage\empty-window\images\Map 1,2 West 224 x 111 px Orig-3c54cb56-3047-44e9-9867-65e43a23121a.png"
OUT = r"C:\Unity\DM\LockedBackup\_f1_attached_pair_analysis"
os.makedirs(OUT, exist_ok=True)

src_im = Image.open(SRC_PATH).convert("RGB")
ref_im = Image.open(REF_PATH).convert("RGB")
Ws, Hs = src_im.size
Wr, Hr = ref_im.size
src_px = src_im.load()
ref_px = ref_im.load()

def unique_rgb(im):
    return len(set(im.getdata()))

print("=== SIZE / COLORS ===")
print(f"SRC: {Ws}x{Hs} unique_RGB={unique_rgb(src_im)}")
print(f"REF: {Wr}x{Hr} unique_RGB={unique_rgb(ref_im)}")
assert (Ws, Hs) == (160, 111), (Ws, Hs)
assert (Wr, Hr) == (224, 111), (Wr, Hr)
src_im.save(os.path.join(OUT, "src_160x111.png"))
ref_im.save(os.path.join(OUT, "ref_224x111.png"))

BANDS = [("0-27", 0, 28), ("28-54", 28, 55), ("55-82", 55, 83), ("83-110", 83, 111)]

def new_dst():
    return Image.new("RGB", (224, 111), (0, 0, 0))

def copy_col(dst, dx, s, sx):
    dp = dst.load(); sp = s.load()
    for y in range(111):
        dp[dx, y] = sp[sx, y]

def copy_range(dst, dx0, dx1, s, sx0):
    # copy width = dx1-dx0 from sx0
    dp = dst.load(); sp = s.load()
    w = dx1 - dx0
    for y in range(111):
        for i in range(w):
            dp[dx0 + i, y] = sp[sx0 + i, y]

def hflip_im(im):
    return im.transpose(Image.FLIP_LEFT_RIGHT)

def build_A_wrap(s):
    d = new_dst()
    copy_range(d, 0, 31, s, 128)      # dst 0..30 <- src 128..158
    copy_range(d, 31, 191, s, 0)      # dst 31..190 <- src 0..159
    copy_range(d, 191, 224, s, 1)     # dst 191..223 <- src 1..33
    return d

def build_B_brick(s):
    d = new_dst()
    dp = d.load(); sp = s.load()
    for y in range(111):
        if (28 <= y <= 54) or (83 <= y <= 110):
            for i in range(32):
                dp[i, y] = sp[i, y]
            for i in range(64):
                dp[32 + i, y] = sp[32 + i, y]
                dp[96 + i, y] = sp[32 + i, y]
            for i in range(64):
                dp[160 + i, y] = sp[96 + i, y]
        else:
            for i in range(64):
                dp[i, y] = sp[i, y]
            for i in range(64):
                dp[64 + i, y] = sp[64 + i, y]
                dp[128 + i, y] = sp[64 + i, y]
            for i in range(32):
                dp[192 + i, y] = sp[128 + i, y]
    return d

def build_C_5to7(s):
    d = new_dst()
    mapping = [0, 1, 1, 2, 3, 3, 4]
    for di, si in enumerate(mapping):
        copy_range(d, di * 32, (di + 1) * 32, s, si * 32)
    return d

def build_D_nearest(s):
    d = new_dst()
    for dx in range(224):
        sx = (dx * 160) // 224
        if sx > 159: sx = 159
        copy_col(d, dx, s, sx)
    return d

def build_F_center160_at_32_unfilled(s):
    d = new_dst()
    copy_range(d, 32, 192, s, 0)
    return d

def build_F_center160_at_32_wrap_ends(s):
    # center SRC at 32..191; sides filled from wrap ends
    d = new_dst()
    copy_range(d, 32, 192, s, 0)
    copy_range(d, 0, 32, s, 128)   # left 32 from src 128..159
    copy_range(d, 192, 224, s, 2)  # right 32 approximating A right (src 2..33)
    return d

def build_F_center160_at_32_tile(s):
    d = new_dst()
    copy_range(d, 32, 192, s, 0)
    copy_range(d, 0, 32, s, 128)
    copy_range(d, 192, 224, s, 0)  # first 32 of SRC
    return d

def build_G_center160_at_31(s):
    d = new_dst()
    copy_range(d, 31, 191, s, 0)
    return d

def score(cand, name):
    cp = cand.load()
    match = differ = 0
    mism_l = mism_m = mism_r = 0
    band_tot = {lab: 0 for lab, _, _ in BANDS}
    band_mis = {lab: 0 for lab, _, _ in BANDS}
    for y in range(111):
        for x in range(224):
            ok = cp[x, y] == ref_px[x, y]
            if ok:
                match += 1
            else:
                differ += 1
                if x <= 31: mism_l += 1
                elif x <= 191: mism_m += 1
                else: mism_r += 1
            for lab, y0, y1 in BANDS:
                if y0 <= y < y1:
                    band_tot[lab] += 1
                    if not ok:
                        band_mis[lab] += 1
    total = match + differ
    nL, nM, nR = 32 * 111, 160 * 111, 32 * 111
    r = {
        "name": name,
        "match": match,
        "differ": differ,
        "pct_match": 100.0 * match / total,
        "mm_x0_31": 100.0 * mism_l / nL,
        "mm_x32_191": 100.0 * mism_m / nM,
        "mm_x192_223": 100.0 * mism_r / nR,
    }
    for lab, _, _ in BANDS:
        r[f"mm_rows_{lab}"] = 100.0 * band_mis[lab] / band_tot[lab]
    cand.save(os.path.join(OUT, f"cand_{name}.png"))
    # simple diff image
    diff = Image.new("RGB", (224, 111), (0, 0, 0))
    dp = diff.load()
    for y in range(111):
        for x in range(224):
            if cp[x, y] != ref_px[x, y]:
                dp[x, y] = (255, 0, 0)
    diff.save(os.path.join(OUT, f"diff_{name}.png"))
    return r

builders = [
    ("A_wrap", build_A_wrap),
    ("B_brick", build_B_brick),
    ("C_5to7", build_C_5to7),
    ("D_nearest", build_D_nearest),
    ("F_center160_at_32_unfilled", build_F_center160_at_32_unfilled),
    ("F_center160_at_32_wrap_ends", build_F_center160_at_32_wrap_ends),
    ("F_center160_at_32_tile", build_F_center160_at_32_tile),
    ("G_center160_at_31", build_G_center160_at_31),
]

results = []
for name, fn in builders:
    arr = fn(src_im)
    results.append(score(arr, name))
    results.append(score(hflip_im(arr), name + "_mirror"))

ranked = sorted(results, key=lambda r: (-r["match"], r["name"]))
print("\n=== RECONSTRUCTION SCORES ===")
for r in ranked:
    print(
        f"{r['name']:36s} match={r['match']:6d} differ={r['differ']:6d} "
        f"pct={r['pct_match']:7.3f}%  "
        f"mmL={r['mm_x0_31']:6.2f} mmM={r['mm_x32_191']:6.2f} mmR={r['mm_x192_223']:6.2f}  "
        + " ".join(f"{lab}={r[f'mm_rows_{lab}']:5.2f}" for lab, _, _ in BANDS)
    )

# Best src x for each dest x
print("\n=== BEST SRC-X PER DEST-X ===")
votes_left = Counter()
votes_right = Counter()
col_best = []
for dx in range(224):
    counts = [0] * 160
    for sx in range(160):
        n = 0
        for y in range(111):
            if ref_px[dx, y] == src_px[sx, y]:
                n += 1
        counts[sx] = n
    best_sx = max(range(160), key=lambda i: counts[i])
    best_n = counts[best_sx]
    top3 = sorted(range(160), key=lambda i: counts[i], reverse=True)[:3]
    top3 = [(i, counts[i]) for i in top3]
    col_best.append((dx, best_sx, best_n, top3))
    if dx <= 31:
        votes_left[best_sx] += 1
    if dx >= 192:
        votes_right[best_sx] += 1

print("Left strip 0..31 top src votes:")
for sx, c in votes_left.most_common(15):
    print(f"  src_x={sx:3d} best_for {c} dest cols")
print("Right strip 192..223 top src votes:")
for sx, c in votes_right.most_common(15):
    print(f"  src_x={sx:3d} best_for {c} dest cols")

print("\nLeft strip detail:")
for dx, best_sx, best_n, top3 in col_best[:32]:
    print(f"  dx={dx:3d} -> sx={best_sx:3d} n={best_n:3d}/111 top3={top3}")
print("Right strip detail:")
for dx, best_sx, best_n, top3 in col_best[192:]:
    print(f"  dx={dx:3d} -> sx={best_sx:3d} n={best_n:3d}/111 top3={top3}")

id31 = sum(1 for dx in range(31, 191) if col_best[dx][1] == dx - 31)
id32 = sum(1 for dx in range(32, 192) if col_best[dx][1] == dx - 32)
print(f"\nIdentity sx=dx-31 on dx31..190: {id31}/160")
print(f"Identity sx=dx-32 on dx32..191: {id32}/160")

# avg match count for those identities
def avg_n(dxs, sx_fn):
    vals = []
    for dx in dxs:
        sx = sx_fn(dx)
        # recount
        n = sum(1 for y in range(111) if ref_px[dx, y] == src_px[sx, y])
        vals.append(n)
    return sum(vals) / len(vals), min(vals), max(vals)

a31, mn31, mx31 = avg_n(range(31, 191), lambda dx: dx - 31)
a32, mn32, mx32 = avg_n(range(32, 192), lambda dx: dx - 32)
print(f"Row-match if sx=dx-31 on mid: avg={a31:.2f} min={mn31} max={mx31} /111")
print(f"Row-match if sx=dx-32 on mid: avg={a32:.2f} min={mn32} max={mx32} /111")

# Same-row closure
print("\n=== SAME-ROW SOURCE CLOSURE ===")
not_in_row = 0
total_pix = 224 * 111
matchable = 0
for y in range(111):
    row_set = {src_px[x, y] for x in range(160)}
    for x in range(224):
        if ref_px[x, y] in row_set:
            matchable += 1
        else:
            not_in_row += 1
print(f"REF pixels NOT in SRC same-row palette: {not_in_row}/{total_pix} ({100.0*not_in_row/total_pix:.4f}%)")
print(f"REF pixels that ARE in SRC same-row palette: {matchable}/{total_pix}")

# Exact reconstructible? any perfect?
perfect = [r for r in ranked if r["differ"] == 0]
print(f"Perfect reconstructions: {len(perfect)} -> {[p['name'] for p in perfect]}")

report = {
    "src_path": SRC_PATH,
    "ref_path": REF_PATH,
    "src_size": [Ws, Hs],
    "ref_size": [Wr, Hr],
    "src_unique_rgb": unique_rgb(src_im),
    "ref_unique_rgb": unique_rgb(ref_im),
    "results_ranked": ranked,
    "votes_left": votes_left.most_common(),
    "votes_right": votes_right.most_common(),
    "left_detail": [{"dx": dx, "best_sx": bs, "n": n, "top3": t3} for dx, bs, n, t3 in col_best[:32]],
    "right_detail": [{"dx": dx, "best_sx": bs, "n": n, "top3": t3} for dx, bs, n, t3 in col_best[192:]],
    "not_in_same_row": not_in_row,
    "same_row_matchable": matchable,
    "id31": id31,
    "id32": id32,
    "avg_id31": [a31, mn31, mx31],
    "avg_id32": [a32, mn32, mx32],
}
with open(os.path.join(OUT, "report.json"), "w", encoding="utf-8") as f:
    json.dump(report, f, indent=2)

lines = []
lines.append("F1 ATTACHED PAIR ANALYSIS (inspection only)")
lines.append(f"SRC: {Ws}x{Hs} unique_RGB={unique_rgb(src_im)}")
lines.append(f"REF: {Wr}x{Hr} unique_RGB={unique_rgb(ref_im)}")
lines.append(f"SRC: {SRC_PATH}")
lines.append(f"REF: {REF_PATH}")
lines.append("")
lines.append("RANKED (best->worst by exact match):")
for i, r in enumerate(ranked, 1):
    lines.append(
        f"{i:2d}. {r['name']}: match={r['match']} differ={r['differ']} ({r['pct_match']:.3f}%) | "
        f"mm% L={r['mm_x0_31']:.2f} M={r['mm_x32_191']:.2f} R={r['mm_x192_223']:.2f} | "
        + " ".join(f"{lab}={r[f'mm_rows_{lab}']:.2f}%" for lab, _, _ in BANDS)
    )
lines.append("")
lines.append(f"Same-row palette misses: {not_in_row}/{total_pix}")
lines.append(f"id sx=dx-31 mid: {id31}/160; sx=dx-32: {id32}/160")
lines.append("Left votes: " + ", ".join(f"sx{sx}:{c}" for sx, c in votes_left.most_common(12)))
lines.append("Right votes: " + ", ".join(f"sx{sx}:{c}" for sx, c in votes_right.most_common(12)))
text = "\n".join(lines)
with open(os.path.join(OUT, "FINDINGS.txt"), "w", encoding="utf-8") as f:
    f.write(text)
print("\n=== FINDINGS ===")
print(text)
