#pragma once

#include <fstream>
#include <sstream>

#define DEBUG_LOG

namespace GfxQuadroSync
{
    void WriteFileDebug(const char* const message, const bool append = true);

    void WriteFileDebug(const char* const message, int value, const bool append = true);

    void WriteFileDebug(const char* const message, unsigned long long value, const bool append = true);
}
