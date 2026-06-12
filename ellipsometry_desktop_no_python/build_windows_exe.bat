@echo off
chcp 65001 > nul
echo Building EllipsometrySolver.exe without Python...

dotnet --version > nul 2>&1
if errorlevel 1 (
  echo .NET SDK is not installed.
  echo Install .NET SDK 8 from:
  echo https://dotnet.microsoft.com/download
  pause
  exit /b 1
)

dotnet publish EllipsometrySolver.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  /p:PublishSingleFile=true ^
  /p:EnableCompressionInSingleFile=true ^
  /p:IncludeNativeLibrariesForSelfExtract=true

if not exist "bin\Release\net8.0-windows\win-x64\publish\EllipsometrySolver.exe" (
  echo Build failed.
  pause
  exit /b 1
)

if not exist "bin\Release\net8.0-windows\win-x64\publish\outputs" mkdir "bin\Release\net8.0-windows\win-x64\publish\outputs"

echo.
echo Done.
echo EXE:
echo bin\Release\net8.0-windows\win-x64\publish\EllipsometrySolver.exe
echo.
echo CSV files will be saved to:
echo bin\Release\net8.0-windows\win-x64\publish\outputs
pause
