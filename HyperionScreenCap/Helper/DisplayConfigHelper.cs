using System;
using System.Runtime.InteropServices;
using log4net;

namespace HyperionScreenCap.Helper
{
    static class DisplayConfigHelper
    {
        private static readonly ILog LOG = LogManager.GetLogger(typeof(DisplayConfigHelper));

        [DllImport("user32.dll")]
        private static extern int GetDisplayConfigBufferSizes(uint flags, out uint pathCount, out uint modeCount);

        [DllImport("user32.dll")]
        private static extern int QueryDisplayConfig(
            uint flags,
            ref uint pathCount,
            [Out] DisplayConfigPathInfo[] paths,
            ref uint modeCount,
            [Out] DisplayConfigModeInfo[] modes,
            IntPtr currentTopologyId);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(ref SdrWhiteLevel request);

        private const uint QDC_ONLY_ACTIVE_PATHS = 2;
        private const int ERROR_SUCCESS = 0;
        private const int DISPLAYCONFIG_DEVICE_INFO_GET_SDR_WHITE_LEVEL = 12;

        [StructLayout(LayoutKind.Sequential)]
        private struct Luid { public uint LowPart; public int HighPart; }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigDeviceInfoHeader
        {
            public int  type;
            public uint size;
            public Luid adapterId;
            public uint id;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SdrWhiteLevel
        {
            public DisplayConfigDeviceInfoHeader header;
            public uint SDRWhiteLevel;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigPathSourceInfo
        {
            public Luid adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigPathTargetInfo
        {
            public Luid adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint outputTechnology;
            public uint rotation;
            public uint scaling;
            public uint refreshRateNumerator;
            public uint refreshRateDenominator;
            public uint scanLineOrdering;
            public int  targetAvailable;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigPathInfo
        {
            public DisplayConfigPathSourceInfo sourceInfo;
            public DisplayConfigPathTargetInfo targetInfo;
            public uint flags;
        }

        [StructLayout(LayoutKind.Sequential, Size = 64)]
        private struct DisplayConfigModeInfo { }

        /// <summary>
        /// Returns the Windows SDR white level in nits for the display on the given adapter LUID.
        /// Returns 80.0f (no boost / fallback) when HDR is off or the query fails.
        /// </summary>
        public static float GetSdrWhiteLevelNits(long adapterLuidValue)
        {
            try
            {
                uint pathCount, modeCount;
                if ( GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out pathCount, out modeCount) != ERROR_SUCCESS )
                    return 80.0f;

                var paths = new DisplayConfigPathInfo[pathCount];
                var modes = new DisplayConfigModeInfo[modeCount];
                if ( QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != ERROR_SUCCESS )
                    return 80.0f;

                for ( int i = 0; i < pathCount; i++ )
                {
                    var t = paths[i].targetInfo;
                    long pathLuid = ((long)t.adapterId.HighPart << 32) | t.adapterId.LowPart;
                    if ( pathLuid != adapterLuidValue ) continue;

                    var query = new SdrWhiteLevel();
                    query.header.type       = DISPLAYCONFIG_DEVICE_INFO_GET_SDR_WHITE_LEVEL;
                    query.header.size       = (uint)Marshal.SizeOf(typeof(SdrWhiteLevel));
                    query.header.adapterId  = t.adapterId;
                    query.header.id         = t.id;

                    if ( DisplayConfigGetDeviceInfo(ref query) == ERROR_SUCCESS && query.SDRWhiteLevel > 0 )
                    {
                        float nits = query.SDRWhiteLevel / 1000.0f * 80.0f;
                        LOG.Info($"DisplayConfig: adapter 0x{adapterLuidValue:X} SDR white level = {query.SDRWhiteLevel} ({nits} nits)");
                        return nits;
                    }
                }
            }
            catch ( Exception ex )
            {
                LOG.Warn($"DisplayConfig: GetSdrWhiteLevelNits failed: {ex.Message}");
            }
            return 80.0f;
        }
    }
}
