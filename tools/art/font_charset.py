"""Regenerate the pixel-font prewarm charset (Assets/_Game/Resources/Fonts/FusionPixel12_charset.txt).

Collects printable ASCII plus every non-ASCII character used in game scripts, data assets (including
\\uXXXX escapes in Unity YAML) and map definitions, so UIKit can put all glyphs into the dynamic font
atlas once at startup. Run after adding new Chinese text.

Usage: python3 tools/art/font_charset.py <repo_root>
"""
import glob, os, re, sys

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.path.abspath(os.path.join(os.path.dirname(__file__), "../.."))


def main():
    chars = {chr(c) for c in range(32, 127)}
    pats = ["Assets/_Game/Scripts/**/*.cs", "Assets/_Game/Data/**/*.asset", "Assets/_Game/Data/**/*.json", "tools/map/*.py"]
    for pat in pats:
        for p in glob.glob(os.path.join(ROOT, pat), recursive=True):
            t = open(p, encoding="utf-8", errors="ignore").read()
            chars |= {chr(int(m, 16)) for m in re.findall(r"\\u([0-9a-fA-F]{4})", t)}
            chars |= {c for c in t if ord(c) > 0x7F and c.isprintable()}
    out = os.path.join(ROOT, "Assets/_Game/Resources/Fonts/FusionPixel12_charset.txt")
    with open(out, "w", encoding="utf-8") as f:
        f.write("".join(sorted(chars)))
    print(len(chars), "characters ->", out)


if __name__ == "__main__":
    main()
