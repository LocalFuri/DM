from PIL import Image
from pathlib import Path

SRC = Path(r"C:\Unity\DM\Assets\Art\Walls\D_TILETYPE_WALL_F1.png")
REF = Path(r"C:\Users\Localghost\AppData\Roaming\Cursor\User\workspaceStorage\empty-window\images\Map 1,2 West 224 x 111 px Orig-0219c790-db6d-458e-83cc-2124ccb3d1af.png")
OUT = Path(r"C:\Unity\DM\LockedBackup\_f1_recon_candidates_vs_orig")
OUT.mkdir(parents=True, exist_ok=True)

src = Image.open(SRC).convert("RGB")
ref = Image.open(REF).convert("RGB")
assert src.size == (160, 111), src.size
assert ref.size == (224, 111), ref.size
W, H = 224, 111
SW = 160

def copy_row_segments(src_row, mapping):
    out = [None] * W
    for d0, d1, s0 in mapping:
        for i, dx in enumerate(range(d0, d1)):
            out[dx] = src_row[s0 + i]
    return out

def recon_A(src_img):
    px = src_img.load()
    out = Image.new("RGB", (W, H))
    op = out.load()
    for y in range(H):
        row = [px[x, y] for x in range(SW)]
        mapping = [(0, 31, 128), (31, 191, 0), (191, 224, 1)]
        dst = copy_row_segments(row, mapping)
        for x in range(W):
            op[x, y] = dst[x]
    return out

def recon_B(src_img):
    px = src_img.load()
    out = Image.new("RGB", (W, H))
    op = out.load()
    for y in range(H):
        topY = y
        row = [px[x, y] for x in range(SW)]
        odd = (topY % 2 == 1)
        brick = odd and ((28 <= topY <= 54) or (83 <= topY <= 110))
        dst = [None] * W
        if brick:
            for i in range(32):
                dst[i] = row[i]
            for i in range(64):
                dst[32 + i] = row[32 + i]
                dst[96 + i] = row[32 + i]
            for i in range(64):
                dst[160 + i] = row[96 + i]
        else:
            for i in range(64):
                dst[i] = row[i]
            for i in range(64):
                dst[64 + i] = row[64 + i]
                dst[128 + i] = row[64 + i]
            for i in range(32):
                dst[192 + i] = row[128 + i]
        for x in range(W):
            op[x, y] = dst[x]
    return out

def recon_C(src_img):
    px = src_img.load()
    out = Image.new("RGB", (W, H))
    op = out.load()
    sx_map = []
    for g in range(32):
        base = g * 5
        for off in [0, 1, 1, 2, 3, 3, 4]:
            sx_map.append(base + off)
    assert len(sx_map) == 224
    for y in range(H):
        for x in range(W):
            op[x, y] = px[sx_map[x], y]
    return out

def recon_D(src_img):
    px = src_img.load()
    out = Image.new("RGB", (W, H))
    op = out.load()
    for y in range(H):
        for dx in range(W):
            sx = (dx * 160) // 224
            if sx < 0: sx = 0
            if sx > 159: sx = 159
            op[dx, y] = px[sx, y]
    return out

def hflip(img):
    return img.transpose(Image.FLIP_LEFT_RIGHT)

def compare(cand, ref_img, name):
    cp = cand.load()
    rp = ref_img.load()
    match = 0
    differ = 0
    left_m = mid_m = right_m = 0
    left_n = mid_n = right_n = 0
    band_m = [0, 0, 0, 0]
    band_n = [0, 0, 0, 0]
    bands = [(0, 27), (28, 54), (55, 82), (83, 110)]
    diff_img = Image.new("RGB", (W, H))
    dp = diff_img.load()
    for y in range(H):
        for x in range(W):
            a = cp[x, y]
            b = rp[x, y]
            ok = (a == b)
            if ok:
                match += 1
                dp[x, y] = (0, 0, 0)
            else:
                differ += 1
                dp[x, y] = (255, 0, 0)
            if x <= 31:
                left_n += 1
                if not ok: left_m += 1
            elif x <= 191:
                mid_n += 1
                if not ok: mid_m += 1
            else:
                right_n += 1
                if not ok: right_m += 1
            for bi, (y0, y1) in enumerate(bands):
                if y0 <= y <= y1:
                    band_n[bi] += 1
                    if not ok: band_m[bi] += 1
                    break
    total = W * H
    pct = 100.0 * match / total
    def mm(c, n):
        return 100.0 * c / n if n else 0.0
    cand.save(OUT / f"{name}_recon.png")
    diff_img.save(OUT / f"{name}_diff.png")
    return {
        "name": name,
        "match": match,
        "differ": differ,
        "pct_match": pct,
        "mm_left": mm(left_m, left_n),
        "mm_mid": mm(mid_m, mid_n),
        "mm_right": mm(right_m, right_n),
        "mm_bands": [mm(band_m[i], band_n[i]) for i in range(4)],
    }

A = recon_A(src)
B = recon_B(src)
C = recon_C(src)
D = recon_D(src)
candidates = [
    ("A_current_mapping", A),
    ("B_old_brick_band", B),
    ("C_5to7_dup", C),
    ("D_nearest", D),
    ("E_A_hflip", hflip(A)),
    ("E_B_hflip", hflip(B)),
    ("E_C_hflip", hflip(C)),
    ("E_D_hflip", hflip(D)),
]

ref.save(OUT / "reference_orig_224x111.png")
src.save(OUT / "source_160x111.png")

results = []
for name, img in candidates:
    results.append(compare(img, ref, name))

results.sort(key=lambda r: -r["pct_match"])

print("REF:", REF)
print("SRC:", SRC, src.size)
print("OUT:", OUT)
print("TOTAL_PIXELS:", W*H)
print()
hdr = "RANK | NAME | match | differ | %match | mm x0-31 | mm x32-191 | mm x192-223 | mm rows0-27 | 28-54 | 55-82 | 83-110"
print(hdr)
for i, r in enumerate(results, 1):
    bb = r["mm_bands"]
    print(f"{i} | {r['name']} | {r['match']} | {r['differ']} | {r['pct_match']:.4f}% | {r['mm_left']:.4f}% | {r['mm_mid']:.4f}% | {r['mm_right']:.4f}% | {bb[0]:.4f}% | {bb[1]:.4f}% | {bb[2]:.4f}% | {bb[3]:.4f}%")

with open(OUT / "report.txt", "w", encoding="utf-8") as f:
    f.write(f"REF: {REF}\nSRC: {SRC}\nOUT: {OUT}\nTOTAL: {W*H}\n\n")
    for i, r in enumerate(results, 1):
        bb = r["mm_bands"]
        f.write(f"{i}. {r['name']}\n")
        f.write(f"  match={r['match']} differ={r['differ']} pct_match={r['pct_match']:.6f}%\n")
        f.write(f"  mismatch% x0-31={r['mm_left']:.6f}% x32-191={r['mm_mid']:.6f}% x192-223={r['mm_right']:.6f}%\n")
        f.write(f"  mismatch% bands rows0-27={bb[0]:.6f}% 28-54={bb[1]:.6f}% 55-82={bb[2]:.6f}% 83-110={bb[3]:.6f}%\n\n")
print("\nWrote report and PNGs to", OUT)
