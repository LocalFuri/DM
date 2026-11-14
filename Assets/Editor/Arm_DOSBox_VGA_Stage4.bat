@echo off
set "MARKER=C:\Unity\DM\Assets\Editor\DOSBox_VGA_Stage4_ARM.txt"
> "%MARKER%" echo ARM
if errorlevel 1 (
  echo ERROR: Could not arm Stage 4.
) else (
  echo Stage 4 armed. Return to Dungeon Master and turn once.
)
pause
