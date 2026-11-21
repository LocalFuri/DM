@echo off
setlocal
cd /d "%~dp0"
python DOS_Ornament_Pixel_Sampling_Candidates_20261121.py GRAPHICS.DAT DOS_All_Ornament_Geometry_F0635_Corrected_20261121.csv
if errorlevel 1 (
 echo.
 echo ERROR: The decoder or required input was unavailable. Nothing was changed in Unity.
) else (
 echo.
 echo Open DOS_Ornament_Pixel_Candidates_20261121_Comparison.png
)
pause
