# beta 0.12.0 validation

A native Studio check imported an older Fennec/Dignitas/Zomba file. The resulting GLB had separate tyre and rim primitives, a tyre base-colour image, metallic 0 and roughness 0.82. Native WebView2 screenshots confirmed black rubber with the eight material-animated wheel faces still playing. Timeline seek/step and pose save/reopen checks passed.

The vehicle template worker generated sixteen pairs of 2048px PNGs from the Fennec, including body, chassis, tyre/rim and animated face surfaces with their original UV tiles. The labelled body sheet was inspected; the transparent guide has the same UV pixel placement and no reference labels. Every pair is indexed by material, UV map, tile and mesh/island identifiers.

A fresh Fortnite ATK export produced one mesh, ten textures and no export warnings; its packed scene produced two UV sheets (four PNGs) with zero template warnings in the packaged worker. Repeated or mirrored UVs remain shared, and material/mesh names do not establish semantic panel names.
