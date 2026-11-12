@echo off
setlocal
rem DOSBox-X native AUTOTYPE: avoids sending Windows keyboard input.
rem Does not change any configurations, existing launchers, or saved games.
set "DOSBOX=C:\Program Files\DOSBox-X\dosbox-x.exe"
set "DMROOT=C:\!!!Stick\Coder\DM"
if not exist "%DOSBOX%" (
  echo DOSBox-X not found: "%DOSBOX%"
  pause
  exit /b 1
)
if not exist "%DMROOT%\DM1\DM.EXE" (
  echo DM.EXE not found: "%DMROOT%\DM1\DM.EXE"
  pause
  exit /b 1
)
rem Faster timing: 1.5-second initial wait; 0.25-second spacing between inputs.
rem Graphics 1, Enter; sound 6, Enter; controls 1, Enter.
start "Dungeon Master - DOSBox-X" "%DOSBOX%" -fastlaunch -c "mount c %DMROOT%" -c "c:" -c "cd DM1" -c "Z:\BIN\AUTOTYPE -w 1.5 -p 0.25 1 enter 6 enter 1 enter" -c "DM"
endlocal
