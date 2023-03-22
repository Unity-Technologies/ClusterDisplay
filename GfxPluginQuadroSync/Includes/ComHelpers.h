#pragma once

#include <Windows.h>

#include <functional>
#include <memory>

struct IUnknown;

namespace GfxQuadroSync
{
    /**
     * \brief Helper to make management of COM objects (IUnknown) easier.
     *
     * \remark Constructor from raw T* and reset method will "adopt" the pointer, in other words it will not call
     *         AddRef on it.  Caller must manually call AddRef on the raw T* if it keep on using it (and eventually 
     *         call Release on it).
     */
    template <class T>
    class ComSharedPtr : public std::shared_ptr<T>
    {
    public:
        ComSharedPtr() {}
        explicit ComSharedPtr(T* ptr)
            : std::shared_ptr<T>(ptr, [](T* toRelease) { if (toRelease) toRelease->Release(); }) {}
        ComSharedPtr(const ComSharedPtr& toCopy) : std::shared_ptr<T>(toCopy) {}
        ComSharedPtr(ComSharedPtr&& toMove) noexcept : std::shared_ptr<T>(toMove) {}

        void reset()
        {
            std::shared_ptr<T>::reset();
        }
        void reset(T* ptr)
        {
            ComSharedPtr(ptr).swap(*this);
        }
    };

    /**
     * \brief Helper class that automatically release a Win32 HANDLE.
     *
     * \remark Designed to behave sort of like std::unique_ptr (cannot be copied).  If parts of the code need it then
     *         consider putting it in a shared_ptr.
     * \remark Default / Empty handle has the value of 0 / null (some parts of Windows API is using INVALID_HANDLE_VALUE,
     *         those handles are not to be used with this class).
     * \remark Constructor or reset method from a raw HANDLE will "adopt" the pointer so that the caller does not need / 
     *         shall not call CloseHandle.
     */
    class HandleWrapper final
    {
    public:
        HandleWrapper() {}
        explicit HandleWrapper(HANDLE handle) : m_Handle(handle) {}
        HandleWrapper(HandleWrapper&& toMove) noexcept { std::swap(m_Handle, toMove.m_Handle); }

        ~HandleWrapper()
        {
            reset();
        }

        HandleWrapper& operator=(HandleWrapper&& toMove) noexcept
        {
            std::swap(m_Handle, toMove.m_Handle);
            return *this;
        }

        explicit operator bool() const noexcept { return m_Handle != NULL; }
        HANDLE get() const noexcept { return m_Handle; }

        void reset()
        {
            if (m_Handle != NULL)
            {
                CloseHandle(m_Handle);
                m_Handle = NULL;
            }
        }
        void reset(HANDLE handle)
        {
            reset();
            m_Handle = handle;
        }

        HandleWrapper(const HandleWrapper&) = delete;
        HandleWrapper& operator=(const HandleWrapper&) = delete;

    private:
        HANDLE m_Handle = NULL;
    };
}
