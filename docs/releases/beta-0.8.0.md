# beta 0.8.0 — studio theme and emote loops

- Port the website's charcoal, soft-grey and muted-gold appearance to the app and installer. Bundle Manrope; retain icon fonts, navigation, export controls and existing animated-background/reduced-motion options.
- Read authored montage section cycles to select one playable loop and exclude introductory/ending sections when the source provides that boundary. Leave one-shot sequences unforced.
- Keep an interpolation closure key outside the Blender playback range, preventing the repeated end-frame pause. Adjust sampling intervals/FPS-base so the source duration is retained at the selected animation frame rate.
- Correct the final approximately 150ms of identified loops to match both boundary pose and movement. Retain traversal root distance with offset repetition.
- Export glTF animation timestamps starting at zero and retain the closure key. Preserve loop metadata when attaching separately exported emotes.
- Website: replace the model picker with Black & Gold Renegade Raider dancing Scenario. Face forward at start; drag or swipe to turn, wheel/pinch/buttons to zoom, arrows/+/- and Home to control/reset. Pause hidden-page animation and respect reduced motion.

Scenario checks at requested 24, 30 and 60 FPS preserve its 8.133333-second duration, matching boundary pose and movement without a duplicated playback frame. glTF input timestamps start at zero and include the exact closing boundary. Dancin’ Domino also passes closure/movement checks at 189 playback frames over its authored 6.30-second loop, excluding its introduction. All 11 installer fresh-install, repair, update and UI/runtime checks passed. Bundled Manrope resolves as the actual font.

Authored looping metadata is required; this does not fabricate loops from unrelated one-shot animations or combine multi-segment montages into a new animation.

The desktop build is a local test installer; the website is deployed separately. Existing material/export limitations remain in their reports.
