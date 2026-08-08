# -*- coding: utf-8 -*-
from __future__ import annotations
from pathlib import Path
from collections import Counter
import json
import numpy as np
from PIL import Image

OUT = Path(r"C:\Unity\DM\LockedBackup\_f1_post_blit_divergence")
UNITY_PATH = Path(
    r"C:\Users\Localghost\AppData\Roaming\Cursor\User\workspaceStorage\empty-window\images"
    r"\Map 1,2 West 224 x 111 px Unity-93ed33be-6167-46f2-90e2-4e0b0a51eb41.png"
)
RAW_PATH = OUT / "raw_BuildExpandedF1Wall.png"
W, H = 224, 111

unity = np.asarray(Image.open(UNITY_PATH).convert("RGB"), dtype=np.uint8)
raw = np.asarray(Image.open(RAW_PATH).convert("RGB"), dtype=np.uint8)
assert unity.shape == (H, W, 3) and raw.shape == (H, W, 3)


def match_pct(a, b):
    eq = np.all(a == b, axis=-1)
    return 100.0 * int(eq.sum()) / eq.size, int(eq.sum()), int((~eq).sum())


results = {}

# 1) UNITY[x]=RAW[x-1] for x>=1, UNITY[0]=black
shifted_r = np.zeros_like(raw)
shifted_r[:, 1:, :] = raw[:, :-1, :]
pct, n, mism = match_pct(unity, shifted_r)
col0_black = bool(np.all(unity[:, 0, :] == 0))
eq_shift_r = np.all(unity == shifted_r, axis=-1)
results["1_shift_right"] = {
    "match_pct": pct,
    "match_n": n,
    "mismatch_n": mism,
    "total": H * W,
    "unity_col0_all_black": col0_black,
    "col0_match_n": int(eq_shift_r[:, 0].sum()),
    "cols_1_223_match_n": int(eq_shift_r[:, 1:].sum()),
    "cols_1_223_total": H * 223,
    "cols_1_223_match_pct": 100.0 * int(eq_shift_r[:, 1:].sum()) / (H * 223),
}

# 2) shift left
shifted_l = np.zeros_like(raw)
shifted_l[:, :-1, :] = raw[:, 1:, :]
pct2, n2, mism2 = match_pct(unity, shifted_l)
results["2_shift_left"] = {
    "match_pct": pct2,
    "match_n": n2,
    "mismatch_n": mism2,
    "total": H * W,
}

# 3) UNITY[1:224] vs RAW[0:223]
u_slice = unity[:, 1:224, :]
r_slice = raw[:, 0:223, :]
eq_cols = np.all(u_slice == r_slice, axis=-1)
col_full_match = np.all(eq_cols, axis=0)
per_col = [int(eq_cols[:, c].sum()) for c in range(223)]
results["3_slice"] = {
    "pixel_match_n": int(eq_cols.sum()),
    "pixel_total": int(eq_cols.size),
    "pixel_match_pct": 100.0 * int(eq_cols.sum()) / eq_cols.size,
    "columns_full_match_n": int(col_full_match.sum()),
    "columns_total": 223,
    "per_col_match_height_top": dict(Counter(per_col).most_common(15)),
}

# 4) regions
regions = {"left": (0, 32), "center": (32, 192), "right": (192, 224)}


def crop(img, a, b):
    return img[:, a:b, :]


def hflip(img):
    return img[:, ::-1, :].copy()


raw_regions = {k: crop(raw, *v) for k, v in regions.items()}
unity_regions = {k: crop(unity, *v) for k, v in regions.items()}
raw_regions_hf = {k: hflip(v) for k, v in raw_regions.items()}
raw_whole_hf = hflip(raw)
raw_whf_regions = {k: crop(raw_whole_hf, *v) for k, v in regions.items()}

region_matrix = {}
for u_name, u_img in unity_regions.items():
    u_w = u_img.shape[1]
    cands = {}
    for r_name, r_img in raw_regions.items():
        if r_img.shape[1] != u_w:
            continue
        p, nn, mm = match_pct(u_img, r_img)
        cands["RAW_" + r_name] = {"match_pct": p, "match_n": nn, "mismatch_n": mm}
        p, nn, mm = match_pct(u_img, raw_regions_hf[r_name])
        cands["hflip(RAW_" + r_name + ")"] = {"match_pct": p, "match_n": nn, "mismatch_n": mm}
    for r_name, r_img in raw_whf_regions.items():
        if r_img.shape[1] != u_w:
            continue
        p, nn, mm = match_pct(u_img, r_img)
        cands["RAW_whole_hflip_" + r_name] = {"match_pct": p, "match_n": nn, "mismatch_n": mm}
    best_name = max(cands.items(), key=lambda kv: kv[1]["match_pct"])
    region_matrix["UNITY_" + u_name] = {
        "best": best_name[0],
        "best_pct": best_name[1]["match_pct"],
        "all": cands,
    }

results["4_region_best"] = region_matrix

# 5) center
uc = unity_regions["center"]
rc = raw_regions["center"]
rc_hf = raw_regions_hf["center"]
rc_whf = raw_whf_regions["center"]
pa, na, ma = match_pct(uc, rc)
pb, nb, mb = match_pct(uc, rc_hf)
pc, nc, mc = match_pct(uc, rc_whf)
results["5_center"] = {
    "UNITY_center_vs_RAW_center": {"match_pct": pa, "match_n": na, "mismatch_n": ma, "pixels": uc.size // 3},
    "UNITY_center_vs_hflip_RAW_center": {"match_pct": pb, "match_n": nb, "mismatch_n": mb, "pixels": uc.size // 3},
    "UNITY_center_vs_RAW_whole_hflip_center": {"match_pct": pc, "match_n": nc, "mismatch_n": mc, "pixels": uc.size // 3},
}

out_path = OUT / "shift_region_tests.json"
out_path.write_text(json.dumps(results, indent=2), encoding="utf-8")

print("=== TEST 1: UNITY = RAW shifted right by 1 (col0 black) ===")
print("match_pct=%.6f" % results["1_shift_right"]["match_pct"])
print("match_n=%d/%d" % (results["1_shift_right"]["match_n"], H * W))
print("mismatch_n=%d" % results["1_shift_right"]["mismatch_n"])
print("cols_1_223_match_pct=%.6f" % results["1_shift_right"]["cols_1_223_match_pct"])
print("cols_1_223_match_n=%d/%d" % (results["1_shift_right"]["cols_1_223_match_n"], H * 223))
print("unity_col0_all_black=%s" % col0_black)

print("=== TEST 2: UNITY = RAW shifted left by 1 ===")
print("match_pct=%.6f" % results["2_shift_left"]["match_pct"])
print("match_n=%d/%d" % (results["2_shift_left"]["match_n"], H * W))
print("mismatch_n=%d" % results["2_shift_left"]["mismatch_n"])

print("=== TEST 3: UNITY[1:224] vs RAW[0:223] ===")
print("pixel_match_n=%d/%d" % (results["3_slice"]["pixel_match_n"], results["3_slice"]["pixel_total"]))
print("pixel_match_pct=%.6f" % results["3_slice"]["pixel_match_pct"])
print("columns_full_match=%d/223" % results["3_slice"]["columns_full_match_n"])
print("per_col_match_height_top=%s" % results["3_slice"]["per_col_match_height_top"])

print("=== TEST 4: region best matches ===")
for uname, info in region_matrix.items():
    print("%s: BEST=%s @%.6f%%" % (uname, info["best"], info["best_pct"]))
    ranked = sorted(info["all"].items(), key=lambda kv: -kv[1]["match_pct"])
    for name, d in ranked:
        print("  %s: %.6f%% (%d/%d)" % (name, d["match_pct"], d["match_n"], d["match_n"] + d["mismatch_n"]))

print("=== TEST 5: center ===")
for k, v in results["5_center"].items():
    print("%s: %.6f%% (%d/%d)" % (k, v["match_pct"], v["match_n"], v["pixels"]))

print("Wrote", out_path)
