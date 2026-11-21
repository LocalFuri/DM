@echo off
cd /d "%~dp0"
python DOS_Extract_Indexed_Manacles_332_20261121.py GRAPHICS_DOS.ZIP
if errorlevel 1 (echo EXTRACTION FAILED. Read the error above.) else (echo Extraction completed successfully.)
pause
