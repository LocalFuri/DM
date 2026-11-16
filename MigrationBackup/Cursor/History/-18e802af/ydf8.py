"""Final 1:1 F1 wall pixel comparison. Analysis only — no layout edits."""
from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageChops, ImageDraw

OUT = Path(__file__).resolve().parent / "_wall_compare"
OUT.mkdir(exist_ok=True)
ROOT = Path(__file__).resolve().parents[2]
WALLS = ROOT / "Art" / "Walls"
FLOOR = ROOT / "Art" / "Floors" / "D_TILETYPE_FLOOR.png"
CEIL = ROOT / "Art" / "Ceilings" / "D_TILETYPE_CEILING.png"

ASSETS = Path(r"C:\Users\Localghost\.cursor\projects\c-Unity-DM\assets")
CSB_PATH = ASSETS / (
    "c__Users_Localghost_AppData_Roaming_Cursor_User_workspaceStorage_"
    "empty-window_images_MapShot-38171c3c-ac02-4740-95dd-cc28a87c75ae.png"
)
UNITY_PATH = ASSETS / (
    "c__Users_Localghost_AppData_Roaming_Cursor_User_workspaceStorage_"
    "empty-window_images_Screenshot-42264664-6dfa-4d41-afb1-66bc119eaef4.png"
)

f1 = Image.open(WALLS / "D_TILETYPE_WALL_F1.png").convert("RGBA")
f1l = Image.open(WALLS / "D_TILETYPE_WALL_F1L.png").convert("RGBA")
f1r = Image.open(WALLS / "D_TILETYPE_WALL_F1R.png").convert("RGBA")
floor = Image.open(FLOOR).convert("RGBA")
ceil = Image.open(CEIL).convert("RGBA")
mask_l = Image.open(WALLS / "D_MASK_WALL_F1L.png").convert("L")


def portrait_mask(img: Image.Image) -> Image.Image:
    m = Image.new("L", img.size, 255)
    mp = m.load()
    px = img.load()
    for y in range(img.size[1]):
        for x in range(img.size[0]):
            r, g, b = px[x, y]
            if abs(r - g) > 28 or abs(g - b) > 28 or (r > 55 and g < 95 and b < 85):
                mp[x, y] = 0
    return m


def diff(a, b, mask=None, thr=25):
    d = ImageChops.difference(a, b)
    px = d.load()
    mp = mask.load() if mask is not None else None
    regions = {}
    ch = 0
    n = 0
    heat = Image.new("RGB", a.size, (15, 15, 15))
    hp = heat.load()
    ap = a.load()
    for y in range(a.size[1]):
        for x in range(a.size[0]):
            if mp is not None and mp[x, y] == 0:
                continue
            n += 1
            mag = max(px[x, y])
            if mag >= thr:
                ch += 1
                hp[x, y] = (255, min(255, mag), 0)
                key = (
                    "L"
                    if x < 32
                    else "Lov"
                    if x < 60
                    else "C"
                    if x < 164
                    else "Rov"
                    if x < 192
                    else "R"
                )
                regions[key] = regions.get(key, 0) + 1
                yk = "ceil" if y < 16 else "wall" if y <= 126 else "floor"
                regions[yk] = regions.get(yk, 0) + 1
            else:
                hp[x, y] = tuple(v // 3 for v in ap[x, y])
    return ch, n, regions, heat


def compose(rx, order, join_fix=False, use_mask=False):
    view = Image.new("RGBA", (224, 136), (0, 0, 0, 255))
    view.alpha_composite(ceil, (0, 97))
    view.alpha_composite(floor, (0, 0))

    def masked(tex, flip=False):
        if not use_mask:
            return tex
        mm = mask_l.transpose(Image.Transpose.FLIP_LEFT_RIGHT) if flip else mask_l
        out = tex.copy()
        out.putalpha(mm)
        return out

    left = masked(f1l)
    right = masked(f1r, True)
    if join_fix:
        view.alpha_composite(left, (0, 16))
        view.alpha_composite(right.crop((28, 0, 60, 111)), (rx + 28, 16))
        view.alpha_composite(f1.crop((1, 0, 160, 111)), (33, 16))
    elif order == "L_R_F":
        view.alpha_composite(left, (0, 16))
        view.alpha_composite(right, (rx, 16))
        view.alpha_composite(f1, (32, 16))
    elif order == "F_L_R":
        view.alpha_composite(f1, (32, 16))
        view.alpha_composite(left, (0, 16))
        view.alpha_composite(right, (rx, 16))
    elif order == "L_F_R":
        view.alpha_composite(left, (0, 16))
        view.alpha_composite(f1, (32, 16))
        view.alpha_composite(right, (rx, 16))
    return view.convert("RGB")


def main():
    csb_full = Image.open(CSB_PATH).convert("RGB")
    unity_full = Image.open(UNITY_PATH).convert("RGB")

    print("=== Unity origin search vs runtime join_fix+mask ===")
    comp = compose(165, "L_R_F", join_fix=True, use_mask=True)
    best = []
    for x0 in range(260, 290):
        for y0 in range(145, 175):
            crop = unity_full.crop((x0, y0, x0 + 224, y0 + 136))
            ch, n, _, _ = diff(crop, comp)
            best.append((ch, x0, y0))
    best.sort()
    for row in best[:8]:
        print(row, f"{100 * row[0] / 30464:.1f}%")
    ux, uy = best[0][1], best[0][2]
    unity = unity_full.crop((ux, uy, ux + 224, uy + 136))
    unity.save(OUT / "unity_224x136.png")
    print("Unity crop origin", ux, uy)

    print("=== CSB origin search vs L_R_F r164 ===")
    comp_plain = compose(164, "L_R_F", False, False)
    best_csb = []
    for x0 in range(0, 5):
        for y0 in range(55, 75):
            crop = csb_full.crop((x0, y0, x0 + 224, y0 + 136))
            m = portrait_mask(crop)
            ch, n, _, _ = diff(crop, comp_plain, m)
            best_csb.append((ch / max(n, 1), ch, n, x0, y0))
    best_csb.sort()
    for row in best_csb[:10]:
        print(f"{100 * row[0]:.1f}% ch={row[1]}/{row[2]} origin=({row[3]},{row[4]})")

    print("=== CSB vs all compositions ===")
    results = []
    variants = [
        ("L_R_F_r164", dict(rx=164, order="L_R_F", join_fix=False, use_mask=False)),
        ("L_R_F_r165", dict(rx=165, order="L_R_F", join_fix=False, use_mask=False)),
        ("F_L_R_r164", dict(rx=164, order="F_L_R", join_fix=False, use_mask=False)),
        ("L_F_R_r164", dict(rx=164, order="L_F_R", join_fix=False, use_mask=False)),
        ("join_r165_mask", dict(rx=165, order="L_R_F", join_fix=True, use_mask=True)),
        ("join_r164_mask", dict(rx=164, order="L_R_F", join_fix=True, use_mask=True)),
        ("L_R_F_r164_mask", dict(rx=164, order="L_R_F", join_fix=False, use_mask=True)),
    ]
    for y0 in [59, 61, 63, 65, 74, 75]:
        csb = csb_full.crop((0, y0, 224, y0 + 136))
        m = portrait_mask(csb)
        for name, kwargs in variants:
            img = compose(**kwargs)
            ch, n, reg, heat = diff(csb, img, m)
            results.append((ch / max(n, 1), ch, n, y0, name, reg))
        # front-only
        view = Image.new("RGBA", (224, 136), (0, 0, 0, 255))
        view.alpha_composite(ceil, (0, 97))
        view.alpha_composite(floor, (0, 0))
        view.alpha_composite(f1, (32, 16))
        img = view.convert("RGB")
        ch, n, reg, heat = diff(csb, img, m)
        results.append((ch / max(n, 1), ch, n, y0, "F_only", reg))
    results.sort()
    for row in results[:18]:
        reg = row[5]
        print(
            f"{100 * row[0]:.1f}% y0={row[3]} {row[4]} "
            f"wall={reg.get('wall', 0)} C={reg.get('C', 0)} "
            f"Lov={reg.get('Lov', 0)} Rov={reg.get('Rov', 0)}"
        )

    best_y0 = results[0][3]
    csb = csb_full.crop((0, best_y0, 224, best_y0 + 136))
    csb.save(OUT / "csb_224x136.png")
    pm = portrait_mask(csb)
    pm.save(OUT / "portrait_exclude_mask.png")
    print("CSB crop origin (0, %d)" % best_y0)

    print("=== Unity vs compositions ===")
    for name, kwargs in [
        ("join_r165_mask", dict(rx=165, order="L_R_F", join_fix=True, use_mask=True)),
        ("L_R_F_r165", dict(rx=165, order="L_R_F", join_fix=False, use_mask=False)),
        ("L_R_F_r164", dict(rx=164, order="L_R_F", join_fix=False, use_mask=False)),
        ("F_L_R_r165", dict(rx=165, order="F_L_R", join_fix=False, use_mask=False)),
        ("L_F_R_r165", dict(rx=165, order="L_F_R", join_fix=False, use_mask=False)),
    ]:
        img = compose(**kwargs)
        img.save(OUT / f"composed_{name}.png")
        ch, n, reg, heat = diff(unity, img)
        heat.save(OUT / f"diff_unity_vs_{name}.png")
        print(f"{name}: {ch}/{n} ({100 * ch / n:.1f}%) {reg}")

    ch, n, reg, heat = diff(csb, unity, pm)
    heat.save(OUT / "diff_csb_vs_unity.png")
    print(f"CSB vs Unity: {ch}/{n} ({100 * ch / max(n, 1):.1f}%) {reg}")

    # Save best CSB-vs-compose heat
    best_name = results[0][4]
    variant_map = dict(variants)
    if best_name == "F_only":
        view = Image.new("RGBA", (224, 136), (0, 0, 0, 255))
        view.alpha_composite(ceil, (0, 97))
        view.alpha_composite(floor, (0, 0))
        view.alpha_composite(f1, (32, 16))
        best_img = view.convert("RGB")
    else:
        best_img = compose(**variant_map[best_name])
    ch, n, reg, heat = diff(csb, best_img, pm)
    heat.save(OUT / "diff_csb_vs_best_compose.png")
    best_img.save(OUT / "composed_best_for_csb.png")

    print("=== CSB strips vs assets ===")
    wy = 16
    strips = [
        ("L0_32", 0, 32, f1l, 0),
        ("L32_60", 32, 60, f1l, 0),
        ("F32_192", 32, 192, f1, 32),
        ("R164_192", 164, 192, f1r, 164),
        ("R192_224", 192, 224, f1r, 164),
        ("R165_192", 165, 192, f1r, 165),
        ("R192_224@165", 192, 224, f1r, 165),
        ("F0_28_vs_L32", 32, 60, f1, 32),  # front left edge vs where L overlaps
    ]
    for label, x0, x1, asset, dx in strips:
        region = csb.crop((x0, wy, x1, wy + 111))
        ar = asset.convert("RGB").crop((x0 - dx, 0, x1 - dx, 111))
        m = pm.crop((x0, wy, x1, wy + 111)) if asset is f1 else None
        ch, n, _, _ = diff(region, ar, m)
        print(f"{label}: {ch}/{n} ({100 * ch / max(n, 1):.1f}%)")

    print("=== Unity strips vs assets ===")
    for label, x0, x1, asset, dx in [
        ("L0_32", 0, 32, f1l, 0),
        ("L32_60", 32, 60, f1l, 0),
        ("F32_192", 32, 192, f1, 32),
        ("R192_224@165join", 192, 224, f1r, 165),  # full R would be dest 165; join uses src28@193
    ]:
        region = unity.crop((x0, wy, x1, wy + 111))
        # For join-fix right outer: src 28..59 at dest 193..224 when rx=165
        if label.startswith("R192"):
            ar = f1r.convert("RGB").crop((28, 0, 60, 111))
            # dest 193..224 is 32 wide; compare 192..224 (32px) to src 27..59? 
            # runtime: sourceX=28, destX=165+28=193. So dest 193..223 = src 28..58
            region = unity.crop((193, wy, 224, wy + 111))
            ar = f1r.convert("RGB").crop((28, 0, 59, 111))
            if region.size != ar.size:
                print(label, "size", region.size, ar.size)
                continue
        else:
            ar = asset.convert("RGB").crop((x0 - dx, 0, x1 - dx, 111))
        ch, n, _, _ = diff(region, ar)
        print(f"{label}: {ch}/{n} ({100 * ch / max(n, 1):.1f}%)")

    # Overlap geometry note
    left_ov = f1l.convert("RGB").crop((32, 0, 60, 111))
    front_ov = f1.convert("RGB").crop((0, 0, 28, 111))
    ch, n, _, heat = diff(left_ov, front_ov)
    heat.save(OUT / "diff_asset_overlap_L_vs_F.png")
    print(f"Asset overlap F1L[32:60] vs F1[0:28]: {ch}/{n} ({100 * ch / n:.1f}%)")

    runtime = compose(165, "L_R_F", True, True)
    plain = compose(164, "L_R_F", False, False)
    side = Image.new("RGB", (224 * 4 + 24, 156), (25, 25, 25))
    side.paste(csb, (0, 0))
    side.paste(unity, (232, 0))
    side.paste(runtime, (464, 0))
    side.paste(plain, (696, 0))
    d = ImageDraw.Draw(side)
    d.text((4, 140), "CSB", fill=(200, 200, 200))
    d.text((236, 140), "Unity", fill=(200, 200, 200))
    d.text((468, 140), "Runtime join+mask", fill=(200, 200, 200))
    d.text((700, 140), "L_R_F r164 plain", fill=(200, 200, 200))
    side.save(OUT / "side_by_side.png")

    g = csb.copy()
    d = ImageDraw.Draw(g)
    for x in (32, 60, 164, 192):
        d.line((x, 0, x, 135), fill=(255, 0, 0))
    d.line((0, 16, 223, 16), fill=(0, 255, 0))
    d.line((0, 127, 223, 127), fill=(0, 255, 0))
    g.save(OUT / "csb_guides.png")

    # Mortar rows
    def dark_rows(im, x0, x1):
        px = im.load()
        av = []
        for y in range(im.size[1]):
            s = sum(sum(px[x, y][:3]) for x in range(x0, x1)) / ((x1 - x0) * 3)
            av.append(s)
        out = []
        for y in range(2, len(av) - 2):
            if av[y] < av[y - 1] - 8 and av[y] < av[y + 1] - 8:
                out.append(y)
        return out

    print("Mortar/dark rows CSB wall band", dark_rows(csb.crop((32, 16, 192, 127)), 10, 140))
    print("Mortar/dark rows Unity wall band", dark_rows(unity.crop((32, 16, 192, 127)), 10, 140))
    print("Mortar/dark rows F1 asset", dark_rows(f1.convert("RGB"), 20, 140))
    print("done", OUT)


if __name__ == "__main__":
    main()
