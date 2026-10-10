LEVEL 0 ORIGINAL MAP MIGRATION — 2026-10-10

1. COMMIT your current Unity working version to Git first.
2. Replace your existing DungeonMap.cs with DungeonMap.cs in this ZIP.
3. Replace your existing ViewportLayoutEditor.cs with ViewportLayoutEditor.cs in this ZIP, wherever it currently resides (do not create a second script).
4. Confirm Assets/Data/Maps/DungeonMaster_Level00.json is present (already uploaded by user).
5. In GameBootstrap Inspector assign levelMaps[0] to DungeonMaster_Level00 (and mapJson if it is used for startup). Keep 1..13 as currently assigned.
6. Let Unity compile, check Console, then test ViewEdit Level 0 door markers and staircase 0->1.

This patch does not delete HallOfChampions.json and does not alter other level maps or graphic calibration assets.
The revised ViewEdit source comes from StairTransitions_0-13_20261010.zip.
The DungeonMap source comes from DungeonMap.zip.
The original JSON Level 0 has no explicit playerStart, so this patch preserves verified (1,3) South for Level 0 only.
No code has been compiled in Unity.
