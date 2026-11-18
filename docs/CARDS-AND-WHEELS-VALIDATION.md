# Card gallery and Rocket League checks — beta 0.10.0

- Build and Python syntax checks pass.
- Native card UI: 9,484 Fortnite entries and 1,537 registered PC parts; 18–24 initial realized card containers, scrolling stays bounded. Corrected direct-jump container placement and added visible-card assertions.
- Native tests cover card search, game switching, in-app overlay, isolated background input, closing/restoring focus, two-item batch review, shared format binding, removed combined mode and minimum-size captures.
- Real WebP artwork decodes through SkiaSharp; Zomba source thumbnail extraction passes.
- Existing app regression checks cover update settings, native caption controls, independent style channels, source/exchange formats, asynchronous scans, exact IDs, cancellation and genuine progress.
- Independent visual evaluation PASS after improving card name wrapping, sidebar headings and primary button hover contrast.
- Rocket League real native exports: Octane/Dignitas, Fennec/Dignitas2024, Octane/NRG and default blue paint. Packed body colour and surface textures are referenced by GLB materials.
- Wheel controls: original mount parent hierarchy retained under new controls; moving front-left0.25m does not move front-right; steering0.3 radians and rolling0.5 radians verified. Source Fennec file retains28 packed image resources.

Fixtures and game models are local and excluded from source/release archives. These checks establish the tested cases, not universal shader fidelity.
