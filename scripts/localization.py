#!/usr/bin/env python3
"""Regenerates Resources/Locale/en_US.xml from the Loc.T("key", "English") calls in the sources and
reports what is still untranslated. English always lives in the code; en_US.xml is derived from it.

    python3 scripts/localization.py            # rewrite en_US.xml, list missing keys per locale
    python3 scripts/localization.py --check    # exit 1 if en_US.xml is stale (no write)
"""
import re, sys, pathlib
from xml.sax.saxutils import escape

ROOT = pathlib.Path(__file__).resolve().parent.parent
LOCALE_DIR = ROOT / "Resources" / "Locale"
SKIP = {"PerfectComms.Tests", "PerfectComms.Starlight.Tests", "native", "Libs", "docs", "wiki", "cloudflare", "bin", "obj"}
CALL = re.compile(r'Loc\.[TL]\(\s*"(?P<key>(?:[^"\\]|\\.)*)"\s*,\s*(?P<english>"(?:[^"\\]|\\.)*"|[^\s"][^,)]*)')
ESC = {"n": "\n", "t": "\t", '"': '"', "'": "'", "\\": "\\"}
HEADER = """<?xml version='1.0' encoding='UTF-8'?>
<!--
  Perfect Comms strings. One <string> per Loc.T("key", "English") call site.
  Plain text only: {0} placeholders, \\n for a newline. TextMeshPro markup stays in C#.
  en_US.xml is generated from the code (scripts/localization.py) and checked by LocalizationTests.
-->
<resources>
"""

def unescape(s):
    out, i = [], 0
    while i < len(s):
        if s[i] == "\\":
            i += 1
            if i >= len(s) or s[i] not in ESC:
                raise ValueError("unsupported escape in " + s)
            out.append(ESC[s[i]])
        else:
            out.append(s[i])
        i += 1
    return "".join(out)

def scan():
    strings, problems = {}, []
    for f in sorted(ROOT.rglob("*.cs")):
        rel = f.relative_to(ROOT)
        if rel.parts[0] in SKIP or any(p in ("bin", "obj") for p in rel.parts) or rel.as_posix() == "Comms/Loc.cs":
            continue
        for m in CALL.finditer(f.read_text(encoding="utf-8")):
            key, raw = m["key"], m["english"]
            if not raw.startswith('"'):
                problems.append(f"{rel}: Loc.T(\"{key}\") needs a plain string literal, got {raw}")
                continue
            english = unescape(raw[1:-1])
            if key in strings and strings[key] != english:
                problems.append(f"{rel}: key {key} has different English texts")
            strings[key] = english
    return strings, problems

def xml_keys(path):
    return set(re.findall(r'<string name="([^"]+)"', path.read_text(encoding="utf-8"))) if path.exists() else set()

def parse_xml(path):
    from xml.etree import ElementTree as ET
    out = {}
    for node in ET.parse(path).getroot().iter("string"):
        out[node.get("name")] = (node.text or "").replace("\\n", "\n")
    return out

def audit():
    """Heuristics to review a translation by hand: texts identical to English and placeholder drift."""
    en = parse_xml(LOCALE_DIR / "en_US.xml")
    for loc in ("es_419", "es_ES"):
        tr = parse_xml(LOCALE_DIR / f"{loc}.xml")
        same = sorted(k for k, v in tr.items() if v == en.get(k) and any(c.isalpha() for c in v) and len(v) > 4)
        print(f"== {loc}: {len(tr)} keys")
        print(f"-- identical to English (check they are not forgotten): {len(same)}")
        for k in same: print(f"   {k}: {en[k]!r}")
        # Placeholders and markup must survive translation.
        import re as _re
        bad = [k for k in tr if k in en and (sorted(_re.findall(r"\{\d+", tr[k])) != sorted(_re.findall(r"\{\d+", en[k])) or ("<" in tr[k]) != ("<" in en[k]))]
        print(f"-- placeholder/markup mismatches: {len(bad)}")
        for k in bad: print(f"   {k}")
        if loc == "es_419": break
    return 0

def main():
    if "--audit" in sys.argv:
        return audit()
    strings, problems = scan()
    for p in problems:
        print("ERROR:", p)
    body = "".join(
        f'  <string name="{k}">{escape(v.replace(chr(92), chr(92) * 2).replace(chr(10), chr(92) + "n"))}</string>\n' for k, v in sorted(strings.items()))
    text = HEADER + body + "</resources>\n"
    target = LOCALE_DIR / "en_US.xml"
    if "--check" in sys.argv:
        stale = not target.exists() or target.read_text(encoding="utf-8") != text
        print("en_US.xml is", "STALE" if stale else "up to date")
        return 1 if stale or problems else 0
    target.write_text(text, encoding="utf-8")
    print(f"en_US.xml: {len(strings)} keys")
    for loc in sorted(LOCALE_DIR.glob("*.xml")):
        if loc.name == "en_US.xml":
            continue
        missing = sorted(set(strings) - xml_keys(loc))
        orphan = sorted(xml_keys(loc) - set(strings))
        print(f"{loc.name}: {len(missing)} untranslated, {len(orphan)} orphan")
        for k in missing:
            print("   missing:", k)
        for k in orphan:
            print("   orphan: ", k)
    return 1 if problems else 0

if __name__ == "__main__":
    sys.exit(main())
