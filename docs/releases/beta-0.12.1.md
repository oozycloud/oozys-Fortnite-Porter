# beta 0.12.1

- Unpack and apply Dune Racer’s authored alpha paint mask for independently painted decal accents.
- Add Painted Decal Accents, separate from car trim and secondary colour.
- Invalidate obsolete Rocket League preview renders after material changes.
- Skip texture-template generation for temporary appearance previews.

Verified Dune Racer default and Titanium White accents, plus a changed secondary colour. Pixel comparisons prove secondary changes leave all zero-green-mask pixels unchanged, and decal-paint changes leave all zero-alpha-mask pixels unchanged.
