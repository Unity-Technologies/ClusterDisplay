@echo off

REM Build the Quadro Sync Plugin in Windows
echo ************************************
echo Building Quadro Sync Plugin
echo ************************************

REM Log OS x86 or x64
wmic os get osarchitecture
SET INSTALLDIR=%cd%\\source\\com.unity.cluster-display\\Runtime\\Plugins\\x86_64
echo Install dir is:
echo %INSTALLDIR%

REM Log Build configuration Release or Debug
SET BUILD_CONFIG="Release"
if /I "%1" == "Debug" (
	SET BUILD_CONFIG="Debug"
)
echo Build configuration: %BUILD_CONFIG%

REM Erase any previous pdb in the install directory (so that there is no old .pdb when building in release)
del "%INSTALLDIR%\\GfxPluginQuadroSync*.pdb"

REM build
pushd GfxPluginQuadroSync
if exist build_win (
    rmdir /s /q build_win
)

REM Build the Release library
mkdir build_win
pushd build_win
echo.
echo ************************************
echo Prepare CMake project
echo ************************************
cmake .. ^
	-A x64 ^
    -DCMAKE_INSTALL_PREFIX=%INSTALLDIR%
IF %ERRORLEVEL% NEQ 0 (
	echo Failed to prepare CMake project
	exit 1
)
echo ************************************
echo Build and install library
echo ************************************
cmake --build . ^
	--target INSTALL ^
	--config %BUILD_CONFIG%
IF %ERRORLEVEL% NEQ 0 (
	echo Failed to build and install ProRes wrapper library
	exit 1
)

popd REM build_win

REM To debug Yamato failures of the native wrapper build, comment out these files and look at Yamato artifacts
rmdir /s /q build_win

REM Remove the .lib files
del %INSTALLDIR%\\GfxPluginQuadroSync.lib

popd REM Native\\Windows
