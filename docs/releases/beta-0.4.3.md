# beta 0.4.3

Fixed Fortnite scans failing with **Usmap has invalid version (5)** after the latest public mapping update.

The mapping reader now accepts format 5 and reads its optional engine-version header before passing the unchanged mapping payload to the existing parser. Formats 0–4 retain their existing reader. Downloaded mapping files are not modified, and unknown future formats are rejected with an update message.

Compatibility refresh now parses mappings before accepting them, retries damaged cached downloads, and validates saved mappings before using them offline.

Validation covered version 4 and 5 headers with and without version metadata, malformed/unknown formats, four real mapping files through Fortnite 42.40, cache failures and the live mapping service. A scan of the installed game completed with 19,710 assets and zero unreadable cosmetic definitions.

Format reference: [CUE4Parse's mapping parser](https://github.com/FabianFG/CUE4Parse/blob/master/CUE4Parse/MappingsProvider/Usmap/UsmapParser.cs).
