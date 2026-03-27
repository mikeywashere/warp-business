#!/usr/bin/env python3
"""
Warp Business Icon Generator
Generates PNG icons at standard sizes from the SVG master source.

Requirements:
    pip install cairosvg pillow

Usage:
    python scripts/generate-icons.py

Output:
    src/WarpBusiness.MarketingSite/wwwroot/icons/icon-16.png
    src/WarpBusiness.MarketingSite/wwwroot/icons/icon-32.png
    src/WarpBusiness.MarketingSite/wwwroot/icons/icon-48.png
    src/WarpBusiness.MarketingSite/wwwroot/icons/icon-192.png
    src/WarpBusiness.MarketingSite/wwwroot/icons/icon-512.png
    src/WarpBusiness.MarketingSite/wwwroot/apple-touch-icon.png (180x180)
    src/WarpBusiness.MarketingSite/wwwroot/favicon.ico (multi-size: 16,32,48)
    [copies to WarpBusiness.Web/wwwroot/ and WarpBusiness.CustomerPortal/wwwroot/]
"""

import os
import shutil
from pathlib import Path

try:
    import cairosvg
    from PIL import Image
    import io
    HAS_DEPS = True
except ImportError:
    HAS_DEPS = False

REPO_ROOT = Path(__file__).parent.parent
SVG_SOURCE = REPO_ROOT / "src/WarpBusiness.MarketingSite/wwwroot/favicon.svg"
MARKETING_WWW = REPO_ROOT / "src/WarpBusiness.MarketingSite/wwwroot"
WEB_WWW = REPO_ROOT / "src/WarpBusiness.Web/wwwroot"
PORTAL_WWW = REPO_ROOT / "src/WarpBusiness.CustomerPortal/wwwroot"

SIZES = [16, 32, 48, 180, 192, 512]


def generate_png(svg_path: Path, size: int, output_path: Path):
    png_data = cairosvg.svg2png(url=str(svg_path), output_width=size, output_height=size)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with open(output_path, "wb") as f:
        f.write(png_data)
    print(f"  ✅ {output_path.name} ({size}x{size})")


def generate_ico(svg_path: Path, output_path: Path, sizes=[16, 32, 48]):
    images = []
    for size in sizes:
        png_data = cairosvg.svg2png(url=str(svg_path), output_width=size, output_height=size)
        img = Image.open(io.BytesIO(png_data)).convert("RGBA")
        images.append(img)
    images[0].save(output_path, format="ICO", sizes=[(s, s) for s in sizes], append_images=images[1:])
    print(f"  ✅ favicon.ico (16, 32, 48)")


def copy_icons_to(target_dir: Path, source_dir: Path):
    target_dir.mkdir(parents=True, exist_ok=True)
    for f in ["favicon.svg", "favicon.ico", "apple-touch-icon.png", "site.webmanifest"]:
        src = source_dir / f
        if src.exists():
            shutil.copy2(src, target_dir / f)
    print(f"  📁 Copied to {target_dir.relative_to(REPO_ROOT)}")


if __name__ == "__main__":
    if not HAS_DEPS:
        print("❌ Missing dependencies. Install with:")
        print("   pip install cairosvg pillow")
        exit(1)

    print("🚀 Generating Warp Business icons...")

    # Generate PNGs
    icons_dir = MARKETING_WWW / "icons"
    for size in SIZES:
        name = "apple-touch-icon.png" if size == 180 else f"icon-{size}.png"
        out = MARKETING_WWW / name if size == 180 else icons_dir / f"icon-{size}.png"
        generate_png(SVG_SOURCE, size, out)

    # Generate ICO
    generate_ico(SVG_SOURCE, MARKETING_WWW / "favicon.ico")

    # Copy to other projects
    print("\n📦 Distributing to other web projects...")
    copy_icons_to(WEB_WWW, MARKETING_WWW)
    copy_icons_to(PORTAL_WWW, MARKETING_WWW)

    print("\n✅ Done! All icons generated and distributed.")
    print("   Run this script after any icon changes to regenerate all sizes.")
