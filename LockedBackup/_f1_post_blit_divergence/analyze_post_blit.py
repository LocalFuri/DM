# -*- coding: utf-8 -*-
"""F1 post-blit divergence inspection. Outputs under LockedBackup/_f1_post_blit_divergence only."""
from __future__ import annotations
import json
from pathlib import Path
import numpy as np
from PIL import Image

OUT = Path(r"C:\Unity\DM\LockedBackup\_f1_post_blit_divergence")
OUT.mkdir(parents=True, exist_ok=True)

UNITY_PATH = Path(
    r"C:\Users\Localghost\AppData\Roaming\Cursor\User\workspaceStorage\empty-window\images"
    r"\Map 1,2 West 224 x 111 px Unity-93ed33be-6167-46f2-90e2-4e0b0a51eb41.png"
)
ORIG_PATH = Path(
    r"C:\Users\Localghost\AppData\Roaming\Cursor\User\workspaceStorage\empty-window\images"
    r"\Map 1,2 West 224 x 111 px Orig-54f1a72c-4afd-4870-a078-1edf65f5f6a5.png"
)
FRONT = Path(r"C:\Unity\DM\Assets\Art\Walls\D_TILETYPE_WALL_F1.png")
F1L = Path(r"C:\Unity\DM\Assets\Art\Walls\D_TILETYPE_WALL_F1L.png")
F1R = Path(r"C:\Unity\DM\Assets\Art\Walls\D_TILETYPE_WALL_F1R.png")

W, H = 224, 111


def load_rgb(p: Path) -> np.ndarray:
    im = Image.open(p).convert("RGB")
    arr = np.asarray(im, dtype=np.uint8)
    print(f"  {p.name}: shape={arr.shape}")
    return arr


def lightness(rgb: np.ndarray) -> np.ndarray:
    # float luminance proxy for ranking
    return (
        0.2126 * rgb[..., 0].astype(np.float64)
        + 0.7152 * rgb[..., 1].astype(np.float64)
        + 0.0722 * rgb[..., 2].astype(np.float64)
    )


def palette_rank_image(rgb: np.ndarray) -> np.ndarray:
    """Map each unique RGB to its rank by lightness (stable: sort unique colors by L then RGB)."""
    flat = rgb.reshape(-1, 3)
    # unique colors
    uniq, inv = np.unique(flat, axis=0, return_inverse=True)
    L = lightness(uniq.reshape(-1, 1, 3)).reshape(-1)
    # sort by L then R,G,B for stability
    order = np.lexsort((uniq[:, 2], uniq[:, 1], uniq[:, 0], L))
    ranks = np.empty(len(uniq), dtype=np.int32)
    ranks[order] = np.arange(len(uniq), dtype=np.int32)
    return ranks[inv].reshape(rgb.shape[:2])


def band_mask(x0, x1):
    m = np.zeros((H, W), dtype=bool)
    m[:, x0:x1] = True
    return m


def compare(a: np.ndarray, b: np.ndarray, name: str) -> dict:
    assert a.shape == b.shape == (H, W, 3), (a.shape, b.shape)
    exact = np.all(a == b, axis=-1)
    total = H * W
    match_n = int(exact.sum())
    left = band_mask(0, 32)
    center = band_mask(32, 192)
    right = band_mask(192, 224)

    def mm(mask):
        n = int(mask.sum())
        mism = int((~exact & mask).sum())
        return {
            "pixels": n,
            "mismatch": mism,
            "mismatch_pct": 100.0 * mism / n if n else 0.0,
            "match_pct": 100.0 * (n - mism) / n if n else 0.0,
        }

    ra = palette_rank_image(a)
    rb = palette_rank_image(b)
    # Joint palette: rank by union of colors in both images for fair compare
    # Better: map each image independently then compare ranks — user asked palette-normalized lightness-rank
    # Use shared unique colors across both for comparable ranks
    both = np.concatenate([a.reshape(-1, 3), b.reshape(-1, 3)], axis=0)
    uniq, inv = np.unique(both, axis=0, return_inverse=True)
    L = lightness(uniq.reshape(-1, 1, 3)).reshape(-1)
    order = np.lexsort((uniq[:, 2], uniq[:, 1], uniq[:, 0], L))
    ranks = np.empty(len(uniq), dtype=np.int32)
    ranks[order] = np.arange(len(uniq), dtype=np.int32)
    n = a.size // 3
    ra = ranks[inv[:n]].reshape(H, W)
    rb = ranks[inv[n:]].reshape(H, W)
    rank_eq = ra == rb

    out = {
        "name": name,
        "exact_match_pct": 100.0 * match_n / total,
        "exact_match_n": match_n,
        "exact_mismatch_n": total - match_n,
        "left": mm(left),
        "center": mm(center),
        "right": mm(right),
        "rank_match_pct": 100.0 * int(rank_eq.sum()) / total,
        "rank_mismatch_n": int((~rank_eq).sum()),
        "rank_left_mismatch_pct": 100.0 * int((~rank_eq & left).sum()) / int(left.sum()),
        "rank_center_mismatch_pct": 100.0 * int((~rank_eq & center).sum()) / int(center.sum()),
        "rank_right_mismatch_pct": 100.0 * int((~rank_eq & right).sum()) / int(right.sum()),
    }
    return out


def print_cmp(d: dict):
    print(f"\n=== {d['name']} ===")
    print(f"  exact match: {d['exact_match_pct']:.4f}%  ({d['exact_match_n']}/{H*W}, mism={d['exact_mismatch_n']})")
    for band in ("left", "center", "right"):
        b = d[band]
        print(f"  {band:6s} exact mismatch: {b['mismatch_pct']:.4f}%  ({b['mismatch']}/{b['pixels']})  match={b['match_pct']:.4f}%")
    print(f"  palette-rank match: {d['rank_match_pct']:.4f}%  mism={d['rank_mismatch_n']}")
    print(f"  rank left/center/right mismatch%: {d['rank_left_mismatch_pct']:.4f} / {d['rank_center_mismatch_pct']:.4f} / {d['rank_right_mismatch_pct']:.4f}")


def is_black(c):
    return tuple(int(x) for x in c) == (0, 0, 0)


def col0_stats(rgb: np.ndarray, label: str):
    col = rgb[:, 0, :]
    uniq, counts = np.unique(col, axis=0, return_counts=True)
    black_n = int(np.all(col == 0, axis=-1).sum())
    print(f"\n--- col0 {label} ---")
    print(f"  black pixels: {black_n}/{H} ({100.0*black_n/H:.2f}%)")
    print(f"  unique colors: {len(uniq)}")
    # top few
    order = np.argsort(-counts)
    for i in order[:8]:
        c = tuple(int(x) for x in uniq[i])
        print(f"    {c}: {int(counts[i])}")
    return {
        "black_n": black_n,
        "black_pct": 100.0 * black_n / H,
        "all_black": black_n == H,
        "top": [(tuple(int(x) for x in uniq[i]), int(counts[i])) for i in order[:8]],
    }


# --- load sources ---
print("Loading assets...")
front = load_rgb(FRONT)
f1l = load_rgb(F1L)
f1r = load_rgb(F1R)
unity = load_rgb(UNITY_PATH)
orig = load_rgb(ORIG_PATH)

assert front.shape[0] >= H and front.shape[1] >= 160, front.shape
assert f1l.shape[0] >= H and f1l.shape[1] >= 32, f1l.shape
assert f1r.shape[0] >= H and f1r.shape[1] >= 60, f1r.shape
assert unity.shape == (H, W, 3), unity.shape
assert orig.shape == (H, W, 3), orig.shape

# --- 1) Build RAW composite (PIL top-origin, horizontal-only mapping) ---
# dest[0..31] = hflip(F1R[y, 28..59]) i.e. dest[i]=F1R[59-i]
# dest[32..191]= hflip(Front[y, 0..159]) i.e. dest[32+i]=Front[159-i]
# dest[192..223]= hflip(F1L[y, 0..31]) i.e. dest[192+i]=F1L[31-i]
raw = np.zeros((H, W, 3), dtype=np.uint8)
for y in range(H):
    for i in range(32):
        raw[y, i] = f1r[y, 59 - i]
    for i in range(160):
        raw[y, 32 + i] = front[y, 159 - i]
    for i in range(32):
        raw[y, 192 + i] = f1l[y, 31 - i]

raw_path = OUT / "raw_BuildExpandedF1Wall.png"
Image.fromarray(raw, "RGB").save(raw_path)
print(f"\nSaved {raw_path}")

# --- 2) RAW vs ORIG ---
c_raw_orig = compare(raw, orig, "RAW vs ORIG")
print_cmp(c_raw_orig)
c0_raw = col0_stats(raw, "RAW")

# --- 3) UNITY vs ORIG ---
c_unity_orig = compare(unity, orig, "UNITY vs ORIG")
print_cmp(c_unity_orig)
c0_unity = col0_stats(unity, "UNITY")
c0_orig = col0_stats(orig, "ORIG")

# --- 4) POST-BLIT simulations ---
post_no_mirror = raw.copy()  # dest[x]=raw[x]
post_mirror = np.zeros_like(raw)
for x in range(W):
    post_mirror[:, 223 - x, :] = raw[:, x, :]

Image.fromarray(post_no_mirror, "RGB").save(OUT / "post_no_mirror.png")
Image.fromarray(post_mirror, "RGB").save(OUT / "post_mirror.png")

c_nm_orig = compare(post_no_mirror, orig, "post_no_mirror vs ORIG")
c_m_orig = compare(post_mirror, orig, "post_mirror vs ORIG")
c_nm_unity = compare(post_no_mirror, unity, "post_no_mirror vs UNITY")
c_m_unity = compare(post_mirror, unity, "post_mirror vs UNITY")
print_cmp(c_nm_orig)
print_cmp(c_m_orig)
print_cmp(c_nm_unity)
print_cmp(c_m_unity)

better_unity = "post_mirror" if c_m_unity["exact_match_pct"] > c_nm_unity["exact_match_pct"] else (
    "post_no_mirror" if c_nm_unity["exact_match_pct"] > c_m_unity["exact_match_pct"] else "TIE"
)
print(f"\n*** Which matches UNITY better (exact): {better_unity}")
print(f"    post_no_mirror vs UNITY: {c_nm_unity['exact_match_pct']:.4f}%")
print(f"    post_mirror    vs UNITY: {c_m_unity['exact_match_pct']:.4f}%")

# --- 5) UNITY vs RAW / hflip(RAW) ---
hflip_raw = raw[:, ::-1, :].copy()
Image.fromarray(hflip_raw, "RGB").save(OUT / "hflip_raw.png")
c_u_raw = compare(unity, raw, "UNITY vs RAW")
c_u_hflip = compare(unity, hflip_raw, "UNITY vs hflip(RAW)")
print_cmp(c_u_raw)
print_cmp(c_u_hflip)
print(f"\n*** UNITY closer to: {'hflip(RAW)' if c_u_hflip['exact_match_pct'] > c_u_raw['exact_match_pct'] else 'RAW'}")

# --- 6) col0 blackness attribution ---
print("\n========== COL0 BLACKNESS ATTRIBUTION ==========")
# RAW col0 = F1R[y, 59] for each y (since dest[0]=F1R[59-0])
raw_c0_from = f1r[:H, 59, :]
print(f"RAW col0 == F1R[:,59]? {np.array_equal(raw[:,0,:], raw_c0_from)}")
print(f"F1R[:,59] all black? {np.all(raw_c0_from == 0)}")

# If post_mirror: UNITY col0 would be raw[223] = F1L[y, 0]  (dest[223]=F1L[0] from build: dest[192+i]=F1L[31-i] => i=31 => dest[223]=F1L[0])
# Wait: dest[192+i]=F1L[31-i]; for i=31: dest[223]=F1L[0]
raw_col223 = raw[:, 223, :]
print(f"RAW col223 == F1L[:,0]? {np.array_equal(raw_col223, f1l[:H, 0, :])}")
print(f"F1L[:,0] all black? {np.all(f1l[:H, 0, :] == 0)}")
c0_f1l0 = col0_stats(np.stack([f1l[:H, 0, :]] * W, axis=1)[:, :1, :], "F1L col0 (as 1-wide)")  # hacky print

# Compare unity col0 to candidates
u0 = unity[:, 0, :]
print(f"\nUNITY col0 == RAW col0?     {np.array_equal(u0, raw[:, 0, :])}")
print(f"UNITY col0 == RAW col223?   {np.array_equal(u0, raw[:, 223, :])}")  # post_mirror maps raw[x]->dest[223-x], so dest[0]=raw[223]
print(f"UNITY col0 == black?        {np.all(u0 == 0)}")
print(f"UNITY col0 == (255,0,255)?  {np.all(u0 == (255, 0, 255))}")  # magenta clear
print(f"post_mirror col0 == UNITY col0? {np.array_equal(post_mirror[:,0,:], u0)}")
print(f"post_no_mirror col0 == UNITY col0? {np.array_equal(post_no_mirror[:,0,:], u0)}")

# pixel-wise match of col0
print(f"UNITY col0 match RAW col0 pixels: {int(np.all(u0==raw[:,0,:],axis=-1).sum())}/{H}")
print(f"UNITY col0 match RAW col223 pixels: {int(np.all(u0==raw[:,223,:],axis=-1).sum())}/{H}")
print(f"UNITY col0 match black pixels: {int(np.all(u0==0,axis=-1).sum())}/{H}")
print(f"UNITY col0 match magenta pixels: {int(np.all(u0==(255,0,255),axis=-1).sum())}/{H}")

# Also check if full images: unity == post_mirror etc already reported

# Diff heatmaps (optional saves for inspection)
def save_diff(a, b, name):
    d = np.abs(a.astype(np.int16) - b.astype(np.int16)).max(axis=-1).astype(np.uint8)
    heat = np.stack([d, d, d], axis=-1)
    Image.fromarray(heat, "RGB").save(OUT / name)

save_diff(raw, orig, "diff_raw_vs_orig.png")
save_diff(unity, orig, "diff_unity_vs_orig.png")
save_diff(post_mirror, unity, "diff_post_mirror_vs_unity.png")
save_diff(post_no_mirror, unity, "diff_post_no_mirror_vs_unity.png")
save_diff(hflip_raw, unity, "diff_hflip_raw_vs_unity.png")

report = {
    "raw_vs_orig": c_raw_orig,
    "unity_vs_orig": c_unity_orig,
    "post_no_mirror_vs_orig": c_nm_orig,
    "post_mirror_vs_orig": c_m_orig,
    "post_no_mirror_vs_unity": c_nm_unity,
    "post_mirror_vs_unity": c_m_unity,
    "unity_vs_raw": c_u_raw,
    "unity_vs_hflip_raw": c_u_hflip,
    "better_vs_unity": better_unity,
    "col0_raw": c0_raw,
    "col0_unity": c0_unity,
    "col0_orig": c0_orig,
    "raw_col0_is_F1R_59": bool(np.array_equal(raw[:, 0, :], f1r[:H, 59, :])),
    "F1R_59_all_black": bool(np.all(f1r[:H, 59, :] == 0)),
    "raw_col223_is_F1L_0": bool(np.array_equal(raw[:, 223, :], f1l[:H, 0, :])),
    "F1L_0_all_black": bool(np.all(f1l[:H, 0, :] == 0)),
    "unity_col0_eq_raw_col0": bool(np.array_equal(u0, raw[:, 0, :])),
    "unity_col0_eq_raw_col223": bool(np.array_equal(u0, raw[:, 223, :])),
    "unity_col0_all_black": bool(np.all(u0 == 0)),
    "unity_col0_all_magenta": bool(np.all(u0 == (255, 0, 255))),
}
(OUT / "report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print(f"\nWrote {OUT / 'report.json'}")
print("DONE")
