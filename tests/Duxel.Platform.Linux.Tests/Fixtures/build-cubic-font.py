"""Rebuild the original cubic-ring OpenType test fixture with fontTools 4.66.1.

The geometry and names are authored for this repository; no system font is copied.
This is an explicit fixture-generation command, never part of test execution.
"""
from pathlib import Path
import fontTools
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.t2CharStringPen import T2CharStringPen

assert fontTools.__version__ == "4.66.1"
builder = FontBuilder(1000, isTTF=False)
builder.setupGlyphOrder([".notdef", "space", "O"])
builder.setupCharacterMap({32: "space", 79: "O"})
strings = {}
for name in [".notdef", "space", "O"]:
    pen = T2CharStringPen(640, None)
    if name == "O":
        # Two opposite-winding cubic circles form a genuine CFF counter.
        for radius, direction in [(250, 1), (150, -1)]:
            k = radius * 0.5522847498307936
            x, y = 320, 400
            pen.moveTo((x + radius, y))
            pen.curveTo((x + radius, y + direction * k), (x + k, y + direction * radius), (x, y + direction * radius))
            pen.curveTo((x - k, y + direction * radius), (x - radius, y + direction * k), (x - radius, y))
            pen.curveTo((x - radius, y - direction * k), (x - k, y - direction * radius), (x, y - direction * radius))
            pen.curveTo((x + k, y - direction * radius), (x + radius, y - direction * k), (x + radius, y))
            pen.closePath()
    strings[name] = pen.getCharString()
builder.setupCFF("DuxelCubicFixture", {"FullName": "Duxel cubic outline fixture", "FamilyName": "DuxelFixture", "Weight": "Regular"}, strings, {})
builder.setupHorizontalMetrics({name: (640, 70 if name == "O" else 0) for name in strings})
builder.setupHorizontalHeader(ascent=800, descent=-200)
builder.setupNameTable({"familyName": "DuxelFixture", "styleName": "Regular", "uniqueFontIdentifier": "DuxelCubicFixture1", "fullName": "Duxel cubic outline fixture", "psName": "DuxelCubicFixture", "version": "Version 1.0"})
builder.setupOS2(sTypoAscender=800, sTypoDescender=-200, usWinAscent=800, usWinDescent=200)
builder.setupPost()
builder.font.recalcTimestamp = False
builder.font["head"].created = builder.font["head"].modified = 2082844800
builder.save(Path(__file__).with_name("CubicOutline.otf"))
