# Audio decoder dependencies

vgmstream r2168 is bundled from https://github.com/vgmstream/vgmstream/releases/tag/r2168 . Its COPYING, README and usage notices are retained here.

RADA audio uses a separately installed local RADADecoder v1.0, configured through Porter Settings. Official release: https://github.com/GhostScissors/RadA-Decoder/releases/tag/1.0 . The application checks SHA256 4D0022C501D61EA97495EDD58645C82AA78461B08E6BE2AE79FC37A7B7B3A7F2 before running it. RADADecoder is not redistributed in this source or installer.

Decoded emote WAVs are included in the export and embedded in the Porter emote data inside Blender files. Studio can play these decoded files without a game-audio decoder.
