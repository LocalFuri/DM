from PIL import Image
from collections import defaultdict, Counter
import os

SRC_PATH = r"C:\Users\Localghost\AppData\Roaming\Cursor\User\workspaceStorage\empty-window\images\Wall 1 Original 160 x 111 px-caea4a8f-e2a7-4d8e-999e-eefccb6a142e.png"
REF_PATH = r"C:\Users\Localghost\AppData\Roaming\Cursor\User\workspaceStorage\empty-window\images\Map 1,2 West 224 x 111 px Orig-de653cd3-8a11-4b3d-8af7-aa92efa94752.png"
OUT = r"C:\Unity\DM\LockedBackup\_f1_palette_normalized_center_test"

def load_gray(path):
    im = Image.open(path).convert("RGB")
    w, h = im.size
    pix = list(im.getdata())
    # verify grayscale
    for r,g,b in pix:
        if r!=g or g!=b:
            raise SystemExit("not grayscale")
    gray = [p[0] for p in pix]
    return gray, w, h  # row-major

def unique_sorted(gray):
    return sorted(set(gray))

def to_rank(gray):
    u = unique_sorted(gray)
    mapping = {v:i for i,v in enumerate(u)}
    return [mapping[v] for v in gray], u, mapping

def get(arr, w, x, y):
    return arr[y*w + x]

def setv(arr, w, x, y, v):
    arr[y*w + x] = v

src_g, sw, sh = load_gray(SRC_PATH)
ref_g, rw, rh = load_gray(REF_PATH)
print(f"SRC shape={sw}x{sh} unique={unique_sorted(src_g)}")
print(f"REF shape={rw}x{rh} unique={unique_sorted(ref_g)}")

src_idx, src_u, src_map = to_rank(src_g)
ref_idx, ref_u, ref_map = to_rank(ref_g)
print(f"SRC rank map (gray->rank): {src_map}")
print(f"REF rank map (gray->rank): {ref_map}")
print(f"SRC has {len(src_u)} colors, REF has {len(ref_u)} colors")

H, Ws, W = 111, 160, 224
assert sh==H and sw==Ws and rh==H and rw==W

# flip SRC horizontal -> list
src_flip = [0]*(Ws*H)
for y in range(H):
    for x in range(Ws):
        src_flip[y*Ws + x] = get(src_idx, Ws, Ws-1-x, y)

def col_match(flipped, flip_col, ref_arr, ref_w, dest_x):
    m = 0
    for y in range(H):
        if flipped[y*Ws + flip_col] == get(ref_arr, ref_w, dest_x, y):
            m += 1
    return m

# Center compare
center_matches = 0
center_mismatches = []
for y in range(H):
    for fx in range(Ws):
        abs_x = fx + 32
        pred = src_flip[y*Ws + fx]
        refv = get(ref_idx, W, abs_x, y)
        if pred == refv:
            center_matches += 1
        else:
            center_mismatches.append((y, abs_x, fx, pred, refv))

center_total = H * Ws
cmm = len(center_mismatches)
cpct = 100.0 * center_matches / center_total

print("\n=== 1. OVERALL ===")
# sides unmatched: only center compared
print(f"Full 224x111 with sides UNMATCHED (only center compared): matches={center_matches} mismatches={cmm} match%={cpct:.6f}% of compared={center_total} pixels (of {H*W} total)")
full_strict = center_matches  # sides count as mismatch
print(f"Full canvas treating unmatched sides as mismatch: matches={full_strict}/{H*W} = {100.0*full_strict/(H*W):.6f}%")

# best per-col sides
left_best = []
right_best = []
for x in range(0, 32):
    best_c, best_m = -1, -1
    for c in range(Ws):
        m = col_match(src_flip, c, ref_idx, W, x)
        if m > best_m:
            best_m, best_c = m, c
    left_best.append((best_c, best_m))
for x in range(192, 224):
    best_c, best_m = -1, -1
    for c in range(Ws):
        m = col_match(src_flip, c, ref_idx, W, x)
        if m > best_m:
            best_m, best_c = m, c
    right_best.append((best_c, best_m))

left_match_sum = sum(m for _, m in left_best)
right_match_sum = sum(m for _, m in right_best)
left_total = 32 * H
right_total = 32 * H
print(f"LEFT best-per-col reconstruction: {left_match_sum}/{left_total} = {100.0*left_match_sum/left_total:.6f}%")
print(f"RIGHT best-per-col reconstruction: {right_match_sum}/{right_total} = {100.0*right_match_sum/right_total:.6f}%")
full_best = center_matches + left_match_sum + right_match_sum
print(f"Full 224x111 with BEST-PER-COL side reconstruction: matches={full_best}/{H*W} = {100.0*full_best/(H*W):.6f}% mismatches={H*W-full_best}")

print("\n=== 2-5. CENTER x=32..191 ===")
print(f"Structural mismatching pixels (palette-index): {cmm}")
print(f"Center match: {center_matches}/{center_total} = {cpct:.6f}%")
print(f"Center 100% exact? {'YES' if cmm==0 else 'NO'}")

if cmm > 0:
    row_counts = defaultdict(int)
    x_counts = defaultdict(int)
    rel_counts = defaultdict(int)
    for y, xa, fx, pred, refv in center_mismatches:
        row_counts[y] += 1
        x_counts[xa] += 1
        rel_counts[fx] += 1
    print("Row mismatch counts (row: count):")
    for r in sorted(row_counts):
        print(f"  y={r}: {row_counts[r]}")
    rows = sorted(row_counts.keys())
    bands = []
    start = prev = rows[0]
    for r in rows[1:]:
        if r == prev+1:
            prev = r
        else:
            bands.append((start, prev, sum(row_counts[rr] for rr in range(start, prev+1))))
            start = prev = r
    bands.append((start, prev, sum(row_counts[rr] for rr in range(start, prev+1))))
    print("Contiguous row bands (y0..y1, mismatch pixels):")
    for a,b,c in bands:
        print(f"  y={a}..{b}: {c} pixels")
    print("X absolute mismatch counts (only cols with mismatches):")
    for x in sorted(x_counts):
        print(f"  x={x}: {x_counts[x]}")
    print("X relative-to-center (flipped src col) mismatch counts:")
    for x in sorted(rel_counts):
        print(f"  flip_col={x} (abs x={x+32}): {rel_counts[x]}")
    print("First 30 mismatches (y, abs_x, flip_col, pred_idx, ref_idx):")
    for y, xa, fx, pred, refv in sorted(center_mismatches, key=lambda t:(t[0],t[1]))[:30]:
        print(f"  y={y} x={xa} flip_col={fx} pred={pred} ref={refv}")

print("\n=== 6. LEFT 0..31 / RIGHT 192..223 independent ===")
print("--- Per dest column best flipped-SRC column ---")
print("LEFT:")
for i, x in enumerate(range(0,32)):
    c, m = left_best[i]
    print(f"  dest_x={x}: best_flip_col={c} match_rows={m}/111 ({100.0*m/111:.4f}%)")
print("RIGHT:")
for i, x in enumerate(range(192,224)):
    c, m = right_best[i]
    print(f"  dest_x={x}: best_flip_col={c} match_rows={m}/111 ({100.0*m/111:.4f}%)")

def span_match(a0, dest_x0, reverse=False):
    m = 0
    for i in range(32):
        fx = a0 + (31-i if reverse else i)
        dx = dest_x0 + i
        for y in range(H):
            if src_flip[y*Ws + fx] == get(ref_idx, W, dx, y):
                m += 1
    return m

print("\n--- Contiguous span search (left 32 cols = flipped_src[a:a+32]) ---")
left_span_scores = []
for a in range(0, Ws - 32 + 1):
    m = span_match(a, 0, False)
    left_span_scores.append((m, a, a+32))
left_span_scores.sort(reverse=True)
m,a,b = left_span_scores[0]
print(f"Best left contiguous: flipped_src[{a}:{b}] match={m}/{32*H} = {100.0*m/(32*H):.6f}%")
print("Top 5 left contiguous:")
for m,a,b in left_span_scores[:5]:
    print(f"  [{a}:{b}] {m}/{32*H} = {100.0*m/(32*H):.6f}%")

print("\n--- Contiguous span search (right 32 cols = flipped_src[a:a+32]) ---")
right_span_scores = []
for a in range(0, Ws - 32 + 1):
    m = span_match(a, 192, False)
    right_span_scores.append((m, a, a+32))
right_span_scores.sort(reverse=True)
m,a,b = right_span_scores[0]
print(f"Best right contiguous: flipped_src[{a}:{b}] match={m}/{32*H} = {100.0*m/(32*H):.6f}%")
print("Top 5 right contiguous:")
for m,a,b in right_span_scores[:5]:
    print(f"  [{a}:{b}] {m}/{32*H} = {100.0*m/(32*H):.6f}%")

print("\n--- Wrap-style hypotheses ---")
for label, a0, dest0 in [
    ("left = flipped_src[128:160] (right end)", 128, 0),
    ("left = flipped_src[0:32] (left end)", 0, 0),
    ("right = flipped_src[0:32] (left end)", 0, 192),
    ("right = flipped_src[128:160] (right end)", 128, 192),
]:
    m = span_match(a0, dest0, False)
    print(f"{label}: {m}/{32*H} = {100.0*m/(32*H):.6f}%")

print("\n--- Circular wrap from center placement (center is flip@32) ---")
def wrap_side_match(x0, x1):
    m = 0
    for x in range(x0, x1):
        fx = (x - 32) % 160
        for y in range(H):
            if src_flip[y*Ws + fx] == get(ref_idx, W, x, y):
                m += 1
    return m
ml = wrap_side_match(0, 32)
mr = wrap_side_match(192, 224)
print(f"wrap (x-32)%160 for LEFT: {ml}/{32*H} = {100.0*ml/(32*H):.6f}%")
print(f"wrap (x-32)%160 for RIGHT: {mr}/{32*H} = {100.0*mr/(32*H):.6f}%")
# full wrap
mw = center_matches + ml + mr
print(f"wrap full canvas: {mw}/{H*W} = {100.0*mw/(H*W):.6f}%")

print("\n--- Repeated column analysis ---")
left_src_cols = [c for c,_ in left_best]
right_src_cols = [c for c,_ in right_best]
print(f"LEFT best-src-col sequence: {left_src_cols}")
print(f"LEFT unique best cols: {sorted(set(left_src_cols))} count={len(set(left_src_cols))}")
print(f"LEFT Counter: {Counter(left_src_cols)}")
print(f"RIGHT best-src-col sequence: {right_src_cols}")
print(f"RIGHT unique best cols: {sorted(set(right_src_cols))} count={len(set(right_src_cols))}")
print(f"RIGHT Counter: {Counter(right_src_cols)}")
ld = [left_src_cols[i+1]-left_src_cols[i] for i in range(len(left_src_cols)-1)]
rd = [right_src_cols[i+1]-right_src_cols[i] for i in range(len(right_src_cols)-1)]
print(f"LEFT consecutive diffs: {ld}")
print(f"LEFT step+1 count={sum(1 for d in ld if d==1)}/{len(ld)}, repeat count={sum(1 for d in ld if d==0)}/{len(ld)}")
print(f"RIGHT consecutive diffs: {rd}")
print(f"RIGHT step+1 count={sum(1 for d in rd if d==1)}/{len(rd)}, repeat count={sum(1 for d in rd if d==0)}/{len(rd)}")

print("\n--- Reversed contiguous spans ---")
left_rev = []
right_rev = []
for a in range(0, Ws-32+1):
    left_rev.append((span_match(a, 0, True), a, a+32))
    right_rev.append((span_match(a, 192, True), a, a+32))
left_rev.sort(reverse=True)
right_rev.sort(reverse=True)
m,a,b = left_rev[0]
print(f"Best left REVERSED contiguous flipped_src[{a}:{b}][:,::-1]: {m}/{32*H} = {100.0*m/(32*H):.6f}%")
m,a,b = right_rev[0]
print(f"Best right REVERSED contiguous flipped_src[{a}:{b}][:,::-1]: {m}/{32*H} = {100.0*m/(32*H):.6f}%")

print("\n--- Single repeated column for entire side ---")
best = (-1, -1)
for c in range(Ws):
    m = 0
    for i in range(32):
        m += col_match(src_flip, c, ref_idx, W, i)
    if m > best[0]:
        best = (m, c)
print(f"LEFT all cols = flip_col[{best[1]}]: {best[0]}/{32*H} = {100.0*best[0]/(32*H):.6f}%")
best = (-1, -1)
for c in range(Ws):
    m = 0
    for i in range(32):
        m += col_match(src_flip, c, ref_idx, W, 192+i)
    if m > best[0]:
        best = (m, c)
print(f"RIGHT all cols = flip_col[{best[1]}]: {best[0]}/{32*H} = {100.0*best[0]/(32*H):.6f}%")

print("\n=== 7. PALETTE vs GEOMETRY ===")
print(f"After lightness-rank normalization, center mismatches={cmm}")
print(f"Center pure geometry match? {'YES' if cmm==0 else 'NO'}")
print("\nDone.")