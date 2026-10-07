using HyperionScreenCap.Properties;
using System;
using System.Collections.Generic;

namespace HyperionScreenCap.Model
{
    [Serializable]
    public class HyperionTaskConfiguration
    {
        public String Id { get; set; }
        public bool Enabled { get; set; }
        public CaptureMethod CaptureMethod { get; set; }
        public int Dx11MaxFps { get; set; }
        public int Dx11FrameCaptureTimeout { get; set; }
        public int Dx11ImageScalingFactor { get; set; }
        public int Dx11AdapterIndex { get; set; }
        public int Dx11MonitorIndex { get; set; }
        public string Dx11MonitorDeviceName { get; set; }
        public ToneMappingMethod Dx11HdrToneMappingMethod { get; set; } = ToneMappingMethod.Reinhard;
        public int Dx11HdrPeakLuminanceNits { get; set; } = 1000;
        public float Dx11HdrSaturation { get; set; } = 1.0f;
        public int Dx11HdrSdrWhiteNits { get; set; } = 200;
        public bool Dx11DebugCapture { get; set; } = false;
        public bool PauseWhenMonitorOff { get; set; } = true;
        public List<HyperionServer> HyperionServers { get; set; }

        public static HyperionTaskConfiguration BuildUsingLegacySettings()
        {
            List<HyperionServer> hyperionServers = new List<HyperionServer>();
            hyperionServers.Add(HyperionServer.BuildUsingDefaultFbsSettings());

            return new HyperionTaskConfiguration()
            {
                Id = GetNewId(),
                Enabled = true,
                CaptureMethod = Settings.Default.captureMethod,
                Dx11MaxFps = Settings.Default.dx11MaxFps,
                Dx11FrameCaptureTimeout = Settings.Default.dx11FrameCaptureTimeout,
                Dx11ImageScalingFactor = Settings.Default.dx11ImageScalingFactor,
                Dx11AdapterIndex = Settings.Default.dx11AdapterIndex,
                Dx11MonitorIndex = Settings.Default.dx11MonitorIndex,
                HyperionServers = hyperionServers
            };
        }

        public static HyperionTaskConfiguration BuildUsingDefaultSettings()
        {
            List<HyperionServer> hyperionServers = new List<HyperionServer>();
            hyperionServers.Add(HyperionServer.BuildUsingDefaultFbsSettings());

            return new HyperionTaskConfiguration()
            {
                Id = GetNewId(),
                Enabled = true,
                CaptureMethod = CaptureMethod.DX11,
                Dx11MaxFps = 60,
                Dx11FrameCaptureTimeout = 1250,
                Dx11ImageScalingFactor = 32,
                Dx11AdapterIndex = 0,
                Dx11MonitorIndex = 0,
                Dx11HdrToneMappingMethod = ToneMappingMethod.Reinhard,
                Dx11HdrPeakLuminanceNits = 1000,
                Dx11HdrSaturation = 1.0f,
                Dx11HdrSdrWhiteNits = 200,
                Dx11DebugCapture = false,
                HyperionServers = hyperionServers
            };
        }

        public static String GetNewId()
        {
            return Guid.NewGuid().ToString().Substring(0, 6);
        }

        public HyperionTaskConfiguration DeepCopy()
        {
            var copy = (HyperionTaskConfiguration) MemberwiseClone();
            copy.Id = String.Copy(Id);
            copy.HyperionServers = new List<HyperionServer>();
            HyperionServers.ForEach(server => copy.HyperionServers.Add(server.DeepCopy()));
            return copy;
        }
    }
}
