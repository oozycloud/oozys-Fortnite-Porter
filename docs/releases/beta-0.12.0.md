# beta 0.12.0

- Fix white Zomba tyres in Studio and standard model exports by assigning tyre and rim faces separate portable materials, with rubber roughness and rim normals. Existing Porter wheel exports are repaired during preview conversion.
- Retain painted rim colours when separating the surfaces.
- Add labelled 2048px vehicle UV template PNG export for Fortnite vehicles and Rocket League cars. The export overlay has a template checkbox, enabled by default.
- Include a transparent UV wire guide, labelled reference PNG, material/UV-tile information, island numbers, mesh names, machine-readable index and painting instructions. Original UVs remain unchanged.

Overlapping UV islands intentionally share texture pixels. Material and mesh names identify regions; panel names are not guessed. Vehicles without usable UVs report a warning. Custom game shaders and decal application inside games are not recreated by a template.
