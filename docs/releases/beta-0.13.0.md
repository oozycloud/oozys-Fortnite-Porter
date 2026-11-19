# beta 0.13.0

- Export Sabrina outfits’ authored sequin main/trim/secondary colour gradients, effect masks and detail textures. Bake portable colour/roughness maps while retaining source images.
- Verify all six available combinations across Sabrina Carpenter/Babydoll and Tour-Ready pink/Baby Blue/reactivity on/off. View-dependent Unreal thin-film and reactive effects are approximated.
- Export emote music from authored sound notifies, cues and MetaSound references, with WAV files and embedded Blender emote audio data.
- Add Studio emote audio playback, mute and volume, synchronized pause, seeking and speed. Songs continue across shorter dance loops. Portable projects include the audio.
- Keep simulation paused during Studio preparation, and restore cloth/soft-body switches in Blender exports.

Midnight Sun verified: stereo 48kHz intro (0.3667s) and loop (50.8s), loaded and playing in native WebView2, preserved after project save/reopen.

Newer RADA audio uses a separately installed local RADADecoder v1.0 (Settings → Choose RAD audio decoder). It is configured on the test computer; see audio-tools/DEPENDENCIES.md for the official release and integrity hash. The decoder is not bundled or downloaded automatically. Other supported codecs use bundled vgmstream with its notices.
