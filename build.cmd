@echo off

REM ////////////// DIRECT3D12/////////////////


REM Build the D3D12 Plugin in Windows
echo ************************************
echo Building D3D12 Plugin
echo ************************************

REM Log OS x86 or x64
wmic os get osarchitecture
SET INSTALLDIR=%cd%\\source\\com.unity.cluster-display\\Runtime\\Plugins\\x86_64
echo Install dir is:
echo %INSTALLDIR%

pushd GfxPluginQuadroSyncD3D12

REM build
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
	--config Release
IF %ERRORLEVEL% NEQ 0 ( 
	echo Failed to build and install ProRes wrapper library
	exit 1 
)

popd REM build_win

REM To debug Yamato failures of the native wrapper build, comment out these files and look at Yamato artifacts
rmdir /s /q build_win

REM Remove the .lib files
del %INSTALLDIR%\\GfxPluginQuadroSyncD3D12.lib

popd REM Native\\Windows



REM ////////////// DIRECT3D11/////////////////



REM Build the D3D11 Plugin in Windows
echo ************************************
echo Building D3D11 Plugin
echo ************************************

REM Log OS x86 or x64
wmic os get osarchitecture
SET INSTALLDIR=%cd%\\source\\com.unity.cluster-display\\Runtime\\Plugins\\x86_64
echo Install dir is:
echo %INSTALLDIR%

pushd GfxPluginQuadroSyncD3D11

REM build
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
	--config Release
IF %ERRORLEVEL% NEQ 0 ( 
	echo Failed to build and install ProRes wrapper library
	exit 1 
)

popd REM build_win

REM To debug Yamato failures of the native wrapper build, comment out these files and look at Yamato artifacts
rmdir /s /q build_win

REM Remove the .lib files
del %INSTALLDIR%\\GfxPluginQuadroSyncD3D11.lib

popd REM Native\\Windows