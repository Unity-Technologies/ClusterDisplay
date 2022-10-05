using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

// ReSharper disable InconsistentNaming
// ReSharper disable MemberCanBePrivate.Local
// ReSharper disable NotAccessedField.Local
// ReSharper disable UnusedAutoPropertyAccessor.Local
// ReSharper disable UnusedMember.Local
#pragma warning disable CS0649

namespace Unity.ClusterDisplay.MissionControl.LaunchPad.Services
{
    class HealthServiceWindows: IHealthService
    {
        public HealthServiceWindows(ILogger<HealthServiceWindows> logger, IHostApplicationLifetime applicationLifetime)
        {
            m_Logger = logger;
            m_ApplicationLifetime = applicationLifetime;

            // Create the query we will use to periodically fetch CPU usage (we either have to do it periodically all
            // the time or have the call asking for it "wait for some time" (minimum viable is +/- 1/4 second).  So
            // instead we will periodically ask for it and return the last one when asked for it.
            var result = PdhApi.PdhOpenQuery(null, IntPtr.Zero, out m_CpuQuery);
            if (result == PdhApi.ERROR_SUCCESS)
            {
                result = PdhApi.PdhAddCounter(m_CpuQuery, k_CpuCounterName, IntPtr.Zero, out m_CpuCounter);
                if (result == PdhApi.ERROR_SUCCESS)
                {
                    Task.Run(UpdateCpuUsage);
                }
                else
                {
                    m_Logger.LogError("Unable to add counter ({Result})", result);
                    m_CpuQuery.Dispose();
                }
            }
            else
            {
                m_Logger.LogError("Unable to open query ({Result})", result);
            }
        }

        public Health Fetch()
        {
            Health ret = new();

            ret.CpuUtilization = m_LastCpuUsageValue;

            MemoryStatusApi.MEMORYSTATUSEX statEx = new();
            if (MemoryStatusApi.GlobalMemoryStatusEx(statEx))
            {
                ret.MemoryUsage = (long)(statEx.ullTotalPhys - statEx.ullAvailPhys);
                ret.MemoryAvailable = (long)statEx.ullTotalPhys;
            }

            return ret;
        }

        /// <summary>
        /// Static class containing GlobalMemoryStatusEx interop related definitions.
        /// </summary>
        static class MemoryStatusApi
        {
            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
            public class MEMORYSTATUSEX
            {
                public uint dwLength;
                public uint dwMemoryLoad;
                public ulong ullTotalPhys;
                public ulong ullAvailPhys;
                public ulong ullTotalPageFile;
                public ulong ullAvailPageFile;
                public ulong ullTotalVirtual;
                public ulong ullAvailVirtual;
                public ulong ullAvailExtendedVirtual;

                public MEMORYSTATUSEX()
                {
                    this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                }
            }

            [return: MarshalAs(UnmanagedType.Bool)]
            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);
        }

        /// <summary>
        /// Static class containing some PDH (Performance Data Helper -> performance counter) API interop related
        /// definitions.
        /// </summary>
        static class PdhApi
        {
            /// <summary>
            /// A safe wrapper around a PDH query handle
            /// </summary>
            public class QueryHandle : SafeHandleZeroOrMinusOneIsInvalid
            {
                public QueryHandle() : base(true) {}

                protected override bool ReleaseHandle()
                {
                    return PdhApi.PdhCloseQuery(handle) == 0;
                }
            }

            /// <summary>
            /// A safe wrapper around a PDH counter handle
            /// </summary>
            public class CounterHandle : SafeHandleZeroOrMinusOneIsInvalid
            {
                public CounterHandle() : base(true) {}

                protected override bool ReleaseHandle()
                {
                    return PdhApi.PdhRemoveCounter(handle) == 0;
                }
            }

            /// <summary>
            /// Stores value formatted by PdhGetFormattedCounterValue
            /// </summary>
            public class FmtCounterValue
            {
                public UInt32 CStatus { get; set; }
                public object? Value { get; set; }
            };

            // A few common flags, enum constants and status codes
            public const UInt32 ERROR_SUCCESS = 0;
            public const UInt32 PDH_FLAGS_CLOSE_QUERY = 1;
            public const UInt32 PDH_NO_MORE_DATA = 0xC0000BCC;
            public const UInt32 PDH_INVALID_DATA = 0xC0000BC6;
            public const UInt32 PDH_ENTRY_NOT_IN_LOG_FILE = 0xC0000BCD;
            public const UInt32 PDH_FMT_RAW = 0x00000010;
            public const UInt32 PDH_FMT_ANSI = 0x00000020;
            public const UInt32 PDH_FMT_UNICODE = 0x00000040;
            public const UInt32 PDH_FMT_LONG = 0x00000100;
            public const UInt32 PDH_FMT_DOUBLE = 0x00000200;
            public const UInt32 PDH_FMT_LARGE = 0x00000400;
            public const UInt32 PDH_FMT_NOSCALE = 0x00001000;
            public const UInt32 PDH_FMT_1000 = 0x00002000;
            public const UInt32 PDH_FMT_NODATA = 0x00004000;
            public const UInt32 PDH_FMT_NOCAP100 = 0x00008000;
            public const UInt32 PDH_FMT_DATATYPE_MASK = 0x00000FFF;

            /// <summary>
            /// Opens a query handle
            /// </summary>
            [DllImport("pdh.dll", SetLastError = true, CharSet = CharSet.Auto)]
            public static extern UInt32 PdhOpenQuery(string? szDataSource, IntPtr dwUserData, out QueryHandle phQuery);

            /// <summary>
            /// Closes a handle to a query
            /// </summary>
            [DllImport("pdh.dll", SetLastError = true)]
            public static extern UInt32 PdhCloseQuery(IntPtr hQuery);

            /// <summary>
            /// Removes a counter from the given query.
            /// </summary>
            [DllImport("pdh.dll", SetLastError = true)]
            public static extern UInt32 PdhRemoveCounter(IntPtr hQuery);

            /// <summary>
            /// Adds a counter the query and passes out a handle to the counter.
            /// </summary>
            [DllImport("pdh.dll", SetLastError = true, CharSet = CharSet.Auto)]
            public static extern UInt32 PdhAddCounter(QueryHandle hQuery, string szFullCounterPath, IntPtr dwUserData,
                out CounterHandle phCounter);

            /// <summary>
            /// Retrieves a sample from the source.
            /// </summary>
            [DllImport("pdh.dll", SetLastError = true)]
            public static extern UInt32 PdhCollectQueryData(QueryHandle phQuery);

            /// <summary>
            /// The value of a counter as returned by PDH API.
            /// </summary>
            /// <remarks>Sorry for having a 32 or 64 version, this is because the native API is using a union inside a
            /// struct (where padding rules varies depending on 32 vs 64 bits).</remarks>
            [StructLayout(LayoutKind.Explicit)]
            struct PDH_FMT_COUNTERVALUE_32
            {
                [FieldOffset(0)]
                public UInt32 CStatus;
                [FieldOffset(4)]
                public int longValue;
                [FieldOffset(4)]
                public double doubleValue;
                [FieldOffset(4)]
                public long longLongValue;
                [FieldOffset(4)]
                public IntPtr AnsiStringValue;
                [FieldOffset(4)]
                public IntPtr WideStringValue;
            }
            [StructLayout(LayoutKind.Explicit)]
            struct PDH_FMT_COUNTERVALUE_64
            {
                [FieldOffset(0)]
                public UInt32 CStatus;
                [FieldOffset(8)]
                public int longValue;
                [FieldOffset(8)]
                public double doubleValue;
                [FieldOffset(8)]
                public long longLongValue;
                [FieldOffset(8)]
                public IntPtr AnsiStringValue;
                [FieldOffset(8)]
                public IntPtr WideStringValue;
            }

            [DllImport("pdh.dll", EntryPoint = "PdhGetFormattedCounterValue", SetLastError = true)]
            static extern UInt32 PdhGetFormattedCounterValue32(CounterHandle phCounter, UInt32 dwFormat,
                IntPtr lpdwType, out PDH_FMT_COUNTERVALUE_32 pValue);
            [DllImport("pdh.dll", EntryPoint = "PdhGetFormattedCounterValue", SetLastError = true)]
            static extern UInt32 PdhGetFormattedCounterValue64(CounterHandle phCounter, UInt32 dwFormat,
                IntPtr lpdwType, out PDH_FMT_COUNTERVALUE_64 pValue);

            /// <summary>
            /// Retrieves a specific counter value in the specified format.
            /// </summary>
            public static UInt32 PdhGetFormattedCounterValue(CounterHandle phCounter, UInt32 dwFormat,
                IntPtr lpdwType, out FmtCounterValue value)
            {
                value = new();

                if (IntPtr.Size == 8)
                {
                    var ret = PdhGetFormattedCounterValue64(phCounter, dwFormat, lpdwType, out var fmtCounterValue);
                    if (ret == ERROR_SUCCESS)
                    {
                        value.CStatus = fmtCounterValue.CStatus;
                        switch (dwFormat & PDH_FMT_DATATYPE_MASK)
                        {
                            case PDH_FMT_LONG:
                                value.Value = fmtCounterValue.longLongValue;
                                break;
                            case PDH_FMT_DOUBLE:
                                value.Value = fmtCounterValue.doubleValue;
                                break;
                            default:
                                throw new ArgumentException("Unsupported enum value", nameof(dwFormat));
                        }
                    }
                    return ret;
                }
                else
                {
                    Debug.Assert(IntPtr.Size == 4);
                    var ret = PdhGetFormattedCounterValue32(phCounter, dwFormat, lpdwType, out var fmtCounterValue);
                    if (ret == ERROR_SUCCESS)
                    {
                        value.CStatus = fmtCounterValue.CStatus;
                        switch (dwFormat & PDH_FMT_DATATYPE_MASK)
                        {
                            case PDH_FMT_LONG:
                                value.Value = fmtCounterValue.longLongValue;
                                break;
                            case PDH_FMT_DOUBLE:
                                value.Value = fmtCounterValue.doubleValue;
                                break;
                            default:
                                throw new ArgumentException("Unsupported enum value", nameof(dwFormat));
                        }
                    }
                    return ret;
                }
            }
        }

        async Task UpdateCpuUsage()
        {
            while (!m_ApplicationLifetime.ApplicationStopping.IsCancellationRequested)
            {
                // Get the latest value of the counter
                var result = PdhApi.PdhCollectQueryData(m_CpuQuery);
                if (result == PdhApi.ERROR_SUCCESS)
                {
                    result = PdhApi.PdhGetFormattedCounterValue(m_CpuCounter, PdhApi.PDH_FMT_DOUBLE, IntPtr.Zero,
                        out var fmtValue);
                    if (result == PdhApi.ERROR_SUCCESS)
                    {
                        m_LastCpuUsageValue = (float)((double)fmtValue.Value! / 100.0);
                    }
                    else if (result == PdhApi.PDH_INVALID_DATA)
                    {
                        // This is normal for the first PdhGetFormattedCounterValue, just skip
                    }
                    else
                    {
                        m_Logger.LogWarning("Failed to format Cpu Usage counter data ({Result})", result);
                    }
                }
                else
                {
                    m_Logger.LogWarning("Failed to collect Cpu Usage counter data ({Result})", result);
                }

                // Wait to do it again in a second
                await Task.Delay(1000, m_ApplicationLifetime.ApplicationStopping);
            }
        }

        const string k_CpuCounterName = @"\Processor(_Total)\% Processor Time";

        readonly ILogger m_Logger;
        readonly IHostApplicationLifetime m_ApplicationLifetime;

        /// <summary>
        /// Query used to get CPU usage from the performance counters.
        /// </summary>
        PdhApi.QueryHandle m_CpuQuery;
        /// <summary>
        /// CPU usage counter.
        /// </summary>
        PdhApi.CounterHandle m_CpuCounter = new();
        /// <summary>
        /// Last fetched value for the CPU usage of the system.
        /// </summary>
        float m_LastCpuUsageValue = -1.0f;
    }
}
