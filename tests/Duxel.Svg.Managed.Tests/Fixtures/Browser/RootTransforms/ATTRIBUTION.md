# Root transform precision references

`triangle.svg` is derived from the critical straight edge in `path11595` of [Red Gallardo](https://commons.wikimedia.org/wiki/File:Red_Gallardo.svg), by Tomeq183, Tacosunday and Svgalbertian, under CC BY-SA 3.0. It retains the original fractional root dimensions and definitions. `matrix.svg` and `baked.svg` express the same critical triangle using an integer root with the current float camera matrix or its mapped device points.

The independently captured Chromium Canvas2D images are immutable diagnostic references. The original source and full-image acceptance criteria remain separate. Source/reference hashes, DPR and capture metadata are in `references.json`.

Run the managed test executable with `--browser-root-transforms <fresh-output-directory>`. It strictly compares the white-initialized output and a separate transparent output. Transparent comparison currently matches the two integer-root controls and fails the fractional-root case. White-initialized comparison fails all three at the same pixel. The independently captured browser controls themselves differ by56 channel values between white-initialized rendering and post-composition of their transparent output at (323,323). Thus root transforms alone are not a sufficient explanation. Keep both phases and do not add a coordinate epsilon or update references to make a candidate pass.
