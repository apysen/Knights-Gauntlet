# Assets cleanup — October 17, 2026

Removed 2,102 unused assets (377.54 MiB of asset content), their importer metadata, and 112 empty folders from `Assets`. Source work under `ArtSource` remains available.

The audit combined Unity's recursive dependency graph with enabled build scenes, ProjectSettings references, scripts and editor utilities, native/mobile SDK folders, and runtime Resources loads. Alternate UI styles, cosmetic VFX, the original chess model sets, dynamically loaded fonts, and active sound effects were preserved. The rejected 3D pixel knight experiment, unused pixel-army imports, source art/audio packs, screenshots, unused TMP samples, scene templates, and superseded Resources artwork were removed.

`candidates.json` and `additional.json` list the deleted asset paths and original byte sizes. `roots.txt` and `dependencies.txt` record the initial dependency audit; the additional Resources pass excluded obsolete paths from those roots and checked the dependency closure again. `incoming-references.json` records the initial serialized-reference check (no retained asset referenced the initial deletion set). These files are audit snapshots, not a general-purpose automatic unused-asset detector.

Recovery archives are outside the repository in the Windows temporary directory. Their exact paths, file counts, and sizes are recorded in `result.json` and `additional-result.json`. Each archive includes deleted assets, their `.meta` files, and folder metadata. Archive entry counts and uncompressed sizes were verified before deletion. Notices from removed source packs are also retained under `Notices` here.

No commit or push was performed for this cleanup.

## Validation

Unity compiled without C# errors, and the post-cleanup gameplay regression run reported 289 passed checks and nine failures, with no missing-asset warnings or game runtime exceptions. Every deleted asset was then temporarily restored from the recovery archives and the suite was rerun: the same 289 checks passed and the exact same nine checks failed. The failures concern terrain-transition expectations and projected cell round-trips, and therefore predate this cleanup. The cleanup was reapplied after comparison. Both reports and the exact failure comparison are saved alongside this record.
