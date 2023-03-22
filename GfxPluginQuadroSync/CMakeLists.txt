cmake_minimum_required(VERSION 3.14.0 FATAL_ERROR)

set(PROJECT_NAME "GfxPluginQuadroSync")
PROJECT(${PROJECT_NAME})
set(CMAKE_CXX_STANDARD 14)
set(CMAKE_CXX_FLAGS "/DWIN32 /D_WINDOWS /GR /EHsc")
message("CXX flags: ${CMAKE_CXX_FLAGS}")

# base files
set( QUADROSYNC_WRAPPER_PUBLIC_HEADERS
	../External/NvAPI/nvapi.h
	../External/NvAPI/nvapi_lite_common.h
	../External/NvAPI/nvapi_lite_d3dext.h
	../External/NvAPI/nvapi_lite_salend.h
	../External/NvAPI/nvapi_lite_salstart.h
	../External/NvAPI/nvapi_lite_sli.h
	../External/NvAPI/nvapi_lite_stereo.h
	../External/NvAPI/nvapi_lite_surround.h
	../External/NvAPI/NvApiDriverSettings.h
	../External/NvAPI/nvHLSLExtns.h
	../External/NvAPI/nvHLSLExtnsInternal.h
	../External/NvAPI/nvShaderExtnEnums.h
)

set( QUADROSYNC_WRAPPER_PROJECT_HEADERS
	Includes/QuadroSync.h
	Includes/GfxQuadroSync.h
	Includes/IGraphicsDevice.h
	Includes/Logger.h
	Includes/D3D11GraphicsDevice.h
	Includes/D3D12GraphicsDevice.h
)

set( QUADROSYNC_WRAPPER_PRIVATE_HEADERS
)

set( QUADROSYNC_WRAPPER_SOURCES
	Sources/QuadroSync.cpp
	Sources/GfxQuadroSync.cpp
	Sources/Logger.cpp
	Sources/D3D11GraphicsDevice.cpp
	Sources/D3D12GraphicsDevice.cpp
	Sources/ComHelpers.cpp
)

INCLUDE_DIRECTORIES(
   "Unity"
   "../External/NvAPI"
   "Includes"
	include
)

add_library( ${PROJECT_NAME} SHARED
${QUADROSYNC_WRAPPER_SOURCES}
${QUADROSYNC_WRAPPER_PUBLIC_HEADERS}
${QUADROSYNC_WRAPPER_PROJECT_HEADERS}
${QUADROSYNC_WRAPPER_PRIVATE_HEADERS}
${QUADROSYNC_WRAPPER_RESOURCES}
)

# Remove 'lib' prefix
SET_TARGET_PROPERTIES( ${PROJECT_NAME} PROPERTIES
   PREFIX ""
)

# Link libraries
set( QUADROSYNC_WRAPPER_DEPENDENCIES
	"nvapi64"
)

target_link_directories(${PROJECT_NAME} PUBLIC
	"../External/NvAPI/amd64"
)

target_link_libraries( ${PROJECT_NAME}
${QUADROSYNC_WRAPPER_STATIC_DEPENDENCIES}
${QUADROSYNC_WRAPPER_DEPENDENCIES}
)

# Install
install( TARGETS ${PROJECT_NAME} DESTINATION .)
install( FILES $<TARGET_PDB_FILE:${PROJECT_NAME}> DESTINATION . OPTIONAL )
