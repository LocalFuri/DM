@echo off
set "MARKER=C:\Unity\DM\Assets\Editor\DOSBox_VGA_Stage3_ARM.txt"
> "%MARKER%" echo ARM
if errorlevel 1 (
  echo ERROR: Cannot write ARM marker at %MARKER%
) else (
  echo DOSBox-X Stage 3 armed. Return to Dungeon Master and turn once.
)
pause
