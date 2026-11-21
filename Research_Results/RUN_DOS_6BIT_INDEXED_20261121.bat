@echo off
cd /d "%~dp0"
python DOS_Generic_6bit_Indexed_Renderer_20261121.py GRAPHICS_DOS.ZIP DOS_All_Ornament_Geometry_F0635_Corrected_20261121.csv
if errorlevel 1 (
  echo ERROR: indexed renderer failed. No fallback results were generated.
) else (
  echo SUCCESS: See DOS_6bit_Indexed_Output_20261121 folder.
)
pause
