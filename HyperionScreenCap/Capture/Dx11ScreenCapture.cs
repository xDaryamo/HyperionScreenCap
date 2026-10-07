using HyperionScreenCap.Capture;
using HyperionScreenCap.Config;
using HyperionScreenCap.Model;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using log4net;

namespace HyperionScreenCap
{
    class DX11ScreenCapture : IScreenCapture
    {
        private static readonly ILog LOG = LogManager.GetLogger(typeof(DX11ScreenCapture));

        // ---- COM vtable delegate for IDXGIOutput5::DuplicateOutput1 (slot 27) ----
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DuplicateOutput1Delegate(
            IntPtr thisPtr,
            IntPtr pDevice,
            uint   flags,
            uint   formatsCount,
            [In]   int[]   pFormats,
            out    IntPtr  ppOutputDuplication);

        private int _adapterIndex;
        private int _monitorIndex;
        private int _scalingFactor;
        private int _maxFps;
        private int _frameCaptureTimeout;

        // HDR tone-mapping settings
        private ToneMappingMethod _hdrToneMappingMethod;
        private float _hdrPeakLuminanceNits;
        private float _hdrSaturation;
        private int _hdrSdrWhiteNits;

        private Factory1 _factory;
        private Adapter _adapter;
        private Output _output;
        private Output1 _output1;
        private SharpDX.Direct3D11.Device _device;
        private Texture2D _stagingTexture;
        private Texture2D _smallerTexture;
        private ShaderResourceView _smallerTextureView;
        private OutputDuplication _duplicatedOutput;
        private int _scalingFactorLog2;
        private int _width;
        private int _height;
        private byte[] _lastCapturedFrame;
        private int _minCaptureTime;
        private Stopwatch _captureTimer;
        private bool _desktopDuplicatorInvalid;
        private bool _deviceInvalid;
        private bool _disposed;
        private float _sdrWhiteNits = 80.0f;
        private long  _adapterLuid;

        // HDR state tracking
        private SharpDX.DXGI.Format _activeFormat;
        private bool _isHdrFrame;
        private bool _wasHdrFrame;

        private byte[] _captureBuffer;

        private bool _debugCaptureEnabled;
        private int _debugFrameCount;
        private const int DEBUG_FRAME_INTERVAL = 300;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
        private struct FloatUIntUnion
        {
            [System.Runtime.InteropServices.FieldOffset(0)] public float Float;
            [System.Runtime.InteropServices.FieldOffset(0)] public uint UInt;
        }

        private static readonly byte[] SrgbLut = BuildSrgbLut();

        public int CaptureWidth { get; private set; }
        public int CaptureHeight { get; private set; }

        /// <summary>
        /// True when the most recently captured frame was in R16G16B16A16_Float (HDR) format.
        /// </summary>
        public bool IsHdrActive => _isHdrFrame;

        public static String GetAvailableMonitors()
        {
            StringBuilder response = new StringBuilder();
            using ( Factory1 factory = new Factory1() )
            {
                int adapterIndex = 0;
                foreach(Adapter adapter in factory.Adapters)
                {
                    response.Append($"Adapter Index {adapterIndex++}: {adapter.Description.Description}\n");
                    int outputIndex = 0;
                    foreach(Output output in adapter.Outputs)
                    {
                        response.Append($"\tMonitor Index {outputIndex++}: {output.Description.DeviceName}");
                        var desktopBounds = output.Description.DesktopBounds;
                        response.Append($" {desktopBounds.Right - desktopBounds.Left}×{desktopBounds.Bottom - desktopBounds.Top}\n");
                    }
                    response.Append("\n");
                }
            }
            return response.ToString();
        }

        public DX11ScreenCapture(int adapterIndex, int monitorIndex, int scalingFactor, int maxFps, int frameCaptureTimeout,
            ToneMappingMethod hdrToneMappingMethod, int hdrPeakLuminanceNits, float hdrSaturation,
            int hdrSdrWhiteNits = 200, bool debugCapture = false)
        {
            _adapterIndex = adapterIndex;
            _monitorIndex = monitorIndex;
            _scalingFactor = scalingFactor;
            _maxFps = maxFps;
            _frameCaptureTimeout = frameCaptureTimeout;
            _hdrToneMappingMethod = hdrToneMappingMethod;
            _hdrPeakLuminanceNits = hdrPeakLuminanceNits > 0 ? hdrPeakLuminanceNits : 1000;
            _hdrSaturation = hdrSaturation;
            _hdrSdrWhiteNits = hdrSdrWhiteNits > 0 ? hdrSdrWhiteNits : 200;
            _debugCaptureEnabled = debugCapture;
            _activeFormat = SharpDX.DXGI.Format.B8G8R8A8_UNorm;
            _disposed = true;
            LOG.Info($"DX11: HDR tone mapping method={hdrToneMappingMethod} peakNits={_hdrPeakLuminanceNits} saturation={hdrSaturation} sdrWhiteNits={_hdrSdrWhiteNits} debugCapture={debugCapture}");
        }

        public void Initialize()
        {
            int mipLevels;
            if ( _scalingFactor == 1 )
                mipLevels = 1;
            else if ( _scalingFactor > 0 && _scalingFactor % 2 == 0 )
            {
                /// Mip level for a scaling factor other than one is computed as follows:
                /// 2^n = 2 + n - 1 where LHS is the scaling factor and RHS is the MipLevels value.
                _scalingFactorLog2 = Convert.ToInt32(Math.Log(_scalingFactor, 2));
                mipLevels = 2 + _scalingFactorLog2 - 1;
            }
            else
                throw new Exception("Invalid scaling factor. Allowed valued are 1, 2, 4, etc.");

            // Create DXGI Factory1
            _factory = new Factory1();
            _adapter = _factory.GetAdapter1(_adapterIndex);

            // Create device from Adapter
            _device = new SharpDX.Direct3D11.Device(_adapter);

            // Get DXGI.Output
            _output = _adapter.GetOutput(_monitorIndex);
            _output1 = _output.QueryInterface<Output1>();

            // Width/Height of desktop to capture
            var desktopBounds = _output.Description.DesktopBounds;
            _width = desktopBounds.Right - desktopBounds.Left;
            _height = desktopBounds.Bottom - desktopBounds.Top;

            CaptureWidth = _width / _scalingFactor;
            CaptureHeight = _height / _scalingFactor;
            _captureBuffer = new byte[CaptureWidth * 3 * CaptureHeight];

            // Start with SDR format; format may switch in ManagedCapture once we see the first frame
            _activeFormat = SharpDX.DXGI.Format.B8G8R8A8_UNorm;
            RecreateIntermediateTextures(_activeFormat, mipLevels);

            _minCaptureTime = 1000 / _maxFps;
            _captureTimer = new Stopwatch();
            _disposed = false;

            // Store adapter LUID (kept for potential future use)
            _adapterLuid = _adapter.Description.Luid;

            InitDesktopDuplicator();
        }

        /// <summary>
        /// Disposes and recreates _stagingTexture, _smallerTexture, and _smallerTextureView for the
        /// given pixel format. Called on Initialize, device recreation, and format change.
        /// </summary>
        private void RecreateIntermediateTextures(SharpDX.DXGI.Format format, int mipLevels = -1)
        {
            _stagingTexture?.Dispose();
            _stagingTexture = null;
            _smallerTextureView?.Dispose();
            _smallerTextureView = null;
            _smallerTexture?.Dispose();
            _smallerTexture = null;

            if ( mipLevels < 0 )
            {
                // Recompute mip levels from current scaling factor
                if ( _scalingFactor == 1 )
                    mipLevels = 1;
                else if ( _scalingFactor > 0 && _scalingFactor % 2 == 0 )
                {
                    _scalingFactorLog2 = Convert.ToInt32(Math.Log(_scalingFactor, 2));
                    mipLevels = 2 + _scalingFactorLog2 - 1;
                }
                else
                    throw new Exception("Invalid scaling factor");
            }

            // Staging texture — CPU-readable, no mips, CaptureWidth×CaptureHeight
            var stagingTextureDesc = new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.Read,
                BindFlags = BindFlags.None,
                Format = format,
                Width = CaptureWidth,
                Height = CaptureHeight,
                OptionFlags = ResourceOptionFlags.None,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Staging
            };
            _stagingTexture = new Texture2D(_device, stagingTextureDesc);

            // For FP16 with scaling, GenerateMips requires FL11.0. If we can't use mips, we fall
            // back to CopyResource at full resolution and let the CPU downsample in ToRGBArrayHdr.
            // We always allocate the smaller texture in the same format.
            bool useMips = (mipLevels > 1);
            var smallerTextureDesc = new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.None,
                BindFlags = useMips
                    ? (BindFlags.RenderTarget | BindFlags.ShaderResource)
                    : BindFlags.ShaderResource,
                Format = format,
                Width = _width,
                Height = _height,
                OptionFlags = useMips ? ResourceOptionFlags.GenerateMipMaps : ResourceOptionFlags.None,
                MipLevels = useMips ? mipLevels : 1,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Default
            };
            _smallerTexture = new Texture2D(_device, smallerTextureDesc);
            _smallerTextureView = new ShaderResourceView(_device, _smallerTexture);

            LOG.Info($"DX11: RecreateIntermediateTextures format={format} mipLevels={mipLevels}");
        }

        private void InitDesktopDuplicator()
        {
            // Try IDXGIOutput5::DuplicateOutput1 (vtable slot 27) to get FP16 support
            try
            {
                IntPtr vtable = Marshal.ReadIntPtr(_output1.NativePointer);
                IntPtr slot26  = Marshal.ReadIntPtr(vtable, 26 * IntPtr.Size);
                var fn = Marshal.GetDelegateForFunctionPointer<DuplicateOutput1Delegate>(slot26);
                int[] formats = { (int)SharpDX.DXGI.Format.R16G16B16A16_Float, (int)SharpDX.DXGI.Format.B8G8R8A8_UNorm };
                int hr = fn(_output1.NativePointer, _device.NativePointer, 0, (uint)formats.Length, formats, out IntPtr dupPtr);
                Marshal.ThrowExceptionForHR(hr);
                _duplicatedOutput = new OutputDuplication(dupPtr);
                LOG.Info("DX11: Using DuplicateOutput1 (HDR-capable)");
            }
            catch ( Exception ex )
            {
                LOG.Warn($"DX11: DuplicateOutput1 failed ({ex.Message}), falling back to DuplicateOutput (SDR only)");
                _duplicatedOutput = _output1.DuplicateOutput(_device);
            }

            _desktopDuplicatorInvalid = false;
            _deviceInvalid = false;
        }

        public byte[] Capture()
        {
            if ( _deviceInvalid )
            {
                RecreateD3DDevice();
            }
            else if ( _desktopDuplicatorInvalid )
            {
                _duplicatedOutput?.Dispose();
                InitDesktopDuplicator();
            }

            _captureTimer.Restart();
            byte[] response = ManagedCapture();
            _captureTimer.Stop();

            return response;
        }

        private byte[] ManagedCapture()
        {
            SharpDX.DXGI.Resource screenResource = null;
            OutputDuplicateFrameInformation duplicateFrameInformation;

            try
            {
                try
                {
                    // Try to get duplicated frame within given time
                    _duplicatedOutput.AcquireNextFrame(_frameCaptureTimeout, out duplicateFrameInformation, out screenResource);

                    if ( duplicateFrameInformation.LastPresentTime == 0 && _lastCapturedFrame != null )
                        return _lastCapturedFrame;
                }
                catch ( SharpDXException ex )
                {
                    if ( ex.ResultCode.Code == SharpDX.DXGI.ResultCode.WaitTimeout.Code && _lastCapturedFrame != null )
                        return _lastCapturedFrame;

                    if ( ex.ResultCode == SharpDX.DXGI.ResultCode.AccessLost
                        || ex.ResultCode == SharpDX.DXGI.ResultCode.AccessDenied
                        || ex.ResultCode == SharpDX.DXGI.ResultCode.SessionDisconnected
                        || ex.ResultCode == SharpDX.DXGI.ResultCode.DeviceRemoved
                        || ex.ResultCode == SharpDX.DXGI.ResultCode.DeviceReset
                        || ex.ResultCode == SharpDX.DXGI.ResultCode.InvalidCall )
                    {
                        _desktopDuplicatorInvalid = true;
                        _deviceInvalid = true;
                    }

                    throw ex;
                }

                // Detect format change (e.g. HDR <-> SDR transition)
                using ( var capturedTexture = screenResource.QueryInterface<Texture2D>() )
                {
                    SharpDX.DXGI.Format capturedFormat = capturedTexture.Description.Format;
                    if ( capturedFormat != _activeFormat )
                    {
                        LOG.Info($"DX11: Display format changed from {_activeFormat} to {capturedFormat}, recreating intermediate textures");
                        RecreateIntermediateTextures(capturedFormat);
                        _activeFormat = capturedFormat;
                    }
                    _isHdrFrame = (capturedFormat == SharpDX.DXGI.Format.R16G16B16A16_Float);
                    if ( _isHdrFrame != _wasHdrFrame )
                    {
                        LOG.Info($"DX11: Display mode transition → {((_isHdrFrame) ? "HDR" : "SDR")}");
                        if ( _isHdrFrame )
                            LOG.Info($"DX11: HDR pixel path active — tonemap={_hdrToneMappingMethod} peakNits={_hdrPeakLuminanceNits} saturation={_hdrSaturation}");
                        _wasHdrFrame = _isHdrFrame;
                    }

                    // Check if scaling is used
                    if ( CaptureWidth != _width )
                    {
                        _device.ImmediateContext.CopySubresourceRegion(capturedTexture, 0, null, _smallerTexture, 0);
                    }
                    else
                    {
                        _device.ImmediateContext.CopyResource(capturedTexture, _stagingTexture);
                    }
                }

                if ( CaptureWidth != _width )
                {
                    // Attempt mip generation for downscaling; fall back to CopyResource if it fails
                    if ( _isHdrFrame )
                    {
                        try
                        {
                            _device.ImmediateContext.GenerateMips(_smallerTextureView);
                            _device.ImmediateContext.CopySubresourceRegion(_smallerTexture, _scalingFactorLog2, null, _stagingTexture, 0);
                        }
                        catch ( Exception ex )
                        {
                            LOG.Warn($"DX11: GenerateMips failed for FP16 ({ex.Message}), returning cached frame");
                            if ( _lastCapturedFrame != null )
                                return _lastCapturedFrame;
                            throw;
                        }
                    }
                    else
                    {
                        // SDR path — same as original
                        _device.ImmediateContext.GenerateMips(_smallerTextureView);
                        _device.ImmediateContext.CopySubresourceRegion(_smallerTexture, _scalingFactorLog2, null, _stagingTexture, 0);
                    }
                }
                // (else: already did CopyResource to stagingTexture above)

                // Get the desktop capture texture
                var mapSource = _device.ImmediateContext.MapSubresource(_stagingTexture, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);

                if ( _isHdrFrame )
                    _lastCapturedFrame = ToRGBArrayHdr(mapSource);
                else
                    _lastCapturedFrame = ToRGBArray(mapSource);

                if ( _debugCaptureEnabled && ++_debugFrameCount >= DEBUG_FRAME_INTERVAL )
                {
                    _debugFrameCount = 0;
                    SaveDebugFrame(_lastCapturedFrame);
                }

                return _lastCapturedFrame;
            }
            finally
            {
                screenResource?.Dispose();
                // Fixed OUT_OF_MEMORY issue on AMD Radeon cards. Ignoring all exceptions during unmapping.
                try { _device.ImmediateContext.UnmapSubresource(_stagingTexture, 0); } catch { };
                // Ignore DXGI_ERROR_INVALID_CALL, DXGI_ERROR_ACCESS_LOST errors since capture is already complete
                try { _duplicatedOutput.ReleaseFrame(); } catch { }
            }
        }

        private void RecreateD3DDevice()
        {
            LOG.Info($"DX11: Recreating D3D11 resources due to AccessLost");

            // Dispose all D3D11 resources
            _stagingTexture?.Dispose();
            _smallerTexture?.Dispose();
            _smallerTextureView?.Dispose();
            _duplicatedOutput?.Dispose();
            _output1?.Dispose();
            _device?.Dispose();
            _output?.Dispose();
            _adapter?.Dispose();
            _factory?.Dispose();

            _stagingTexture = null;
            _smallerTexture = null;
            _smallerTextureView = null;
            _duplicatedOutput = null;
            _output1 = null;
            _device = null;
            _output = null;
            _adapter = null;
            _factory = null;

            try
            {
                // Recreate the entire D3D11 resource chain
                _factory = new Factory1();
                _adapter = _factory.GetAdapter1(_adapterIndex);
                _adapterLuid = _adapter.Description.Luid;
                _device = new SharpDX.Direct3D11.Device(_adapter);
                _output = _adapter.GetOutput(_monitorIndex);
                _output1 = _output.QueryInterface<Output1>();

                // Reset to SDR — InitDesktopDuplicator will try DuplicateOutput1 again
                _activeFormat = SharpDX.DXGI.Format.B8G8R8A8_UNorm;
                _isHdrFrame = false;
                RecreateIntermediateTextures(_activeFormat);

                // Recreate duplicator (tries DuplicateOutput1, falls back to DuplicateOutput)
                InitDesktopDuplicator();

                _captureBuffer = new byte[CaptureWidth * 3 * CaptureHeight];
                _deviceInvalid = false;
                _desktopDuplicatorInvalid = false;
                LOG.Info("DX11: D3D11 resource recreation successful");
            }
            catch ( Exception ex )
            {
                _deviceInvalid = true;
                LOG.Error($"DX11: D3D11 resource recreation failed: {ex.Message}");
                throw;
            }
        }

        public bool IsDeviceInvalid()
        {
            return _deviceInvalid;
        }

        /// <summary>
        /// Reads from the memory locations pointed to by the DataBox and saves it into a byte array
        /// ignoring the alpha component of each pixel.
        /// SDR path — B8G8R8A8_UNorm. Byte-for-byte identical to original implementation.
        /// </summary>
        private byte[] ToRGBArray(DataBox mapSource)
        {
            var sourcePtr = mapSource.DataPointer;
            var bytes = _captureBuffer;
            int byteIndex = 0;
            for ( int y = 0; y < CaptureHeight; y++ )
            {
                Int32[] rowData = new Int32[CaptureWidth];
                Marshal.Copy(sourcePtr, rowData, 0, CaptureWidth);

                foreach ( Int32 pixelData in rowData )
                {
                    byte[] values = BitConverter.GetBytes(pixelData);
                    if ( BitConverter.IsLittleEndian )
                    {
                        // Byte order : bgra
                        bytes[byteIndex++] = values[2];
                        bytes[byteIndex++] = values[1];
                        bytes[byteIndex++] = values[0];
                    }
                    else
                    {
                        // Byte order : argb
                        bytes[byteIndex++] = values[1];
                        bytes[byteIndex++] = values[2];
                        bytes[byteIndex++] = values[3];
                    }
                }

                sourcePtr = IntPtr.Add(sourcePtr, mapSource.RowPitch);
            }
            return bytes;
        }

        /// <summary>
        /// Reads R16G16B16A16_Float pixels from the DataBox, applies tone-mapping and sRGB encoding,
        /// and returns an RGB byte array. Called only when _isHdrFrame && _hdrToneMappingEnabled.
        /// </summary>
        private byte[] ToRGBArrayHdr(DataBox mapSource)
        {
            var sourcePtr = mapSource.DataPointer;
            var bytes = _captureBuffer;
            int byteIndex = 0;

            // Normalize so the Windows SDR white level maps to 1.0.
            // scRGB 1.0 = 80 nits; OS boosts SDR apps to _hdrSdrWhiteNits (typically 200 nits),
            // so SDR white appears as _hdrSdrWhiteNits/80 in the FP16 buffer.
            float scale = 80.0f / _hdrSdrWhiteNits;
            // W: how many times brighter than SDR white the HDR peak is
            float W = _hdrPeakLuminanceNits / _hdrSdrWhiteNits;

            bool applysat = Math.Abs(_hdrSaturation - 1.0f) > 1e-5f;

            // Opt 5: select tone-mapping delegate once, outside the pixel loops
            Func<float, float> toneMap;
            switch ( _hdrToneMappingMethod )
            {
                case ToneMappingMethod.Reinhard:
                    toneMap = v => v / (1f + v);
                    break;
                case ToneMappingMethod.ReinhardExtended:
                    float W2 = W * W;
                    toneMap = v => { float t = v * (1f + v / W2) / (1f + v); return t < 0f ? 0f : t > 1f ? 1f : t; };
                    break;
                case ToneMappingMethod.Aces:
                    toneMap = AcesNarkowicz;
                    break;
                default: // Clip
                    toneMap = v => v > 1f ? 1f : v;
                    break;
            }

            // Opt 3: unsafe pointer arithmetic replaces Marshal.ReadInt16 per pixel
            unsafe
            {
                for ( int y = 0; y < CaptureHeight; y++ )
                {
                    // 8 bytes per pixel: R16 G16 B16 A16 (all half-floats, little-endian)
                    ushort* row = (ushort*)((byte*)sourcePtr + y * mapSource.RowPitch);
                    for ( int x = 0; x < CaptureWidth; x++ )
                    {
                        ushort rRaw = row[x * 4];
                        ushort gRaw = row[x * 4 + 1];
                        ushort bRaw = row[x * 4 + 2];

                        // Convert half-float to single-precision (IEEE 754 binary16 -> binary32)
                        float r = HalfToFloat(rRaw);
                        float g = HalfToFloat(gRaw);
                        float b = HalfToFloat(bRaw);

                        // Normalise to [0, ~1] for SDR white
                        r *= scale;
                        g *= scale;
                        b *= scale;

                        // Clamp negatives (scRGB can go negative for out-of-gamut colours)
                        r = r < 0f ? 0f : r;
                        g = g < 0f ? 0f : g;
                        b = b < 0f ? 0f : b;

                        // Tone map per channel (delegate selected once above the loops)
                        r = toneMap(r);
                        g = toneMap(g);
                        b = toneMap(b);

                        // Optional saturation adjustment
                        if ( applysat )
                        {
                            float lum = 0.2126f * r + 0.7152f * g + 0.0722f * b;
                            r = lum + _hdrSaturation * (r - lum);
                            g = lum + _hdrSaturation * (g - lum);
                            b = lum + _hdrSaturation * (b - lum);
                            // Clamp to [0,1]
                            r = r < 0f ? 0f : (r > 1f ? 1f : r);
                            g = g < 0f ? 0f : (g > 1f ? 1f : g);
                            b = b < 0f ? 0f : (b > 1f ? 1f : b);
                        }

                        // sRGB gamma encoding (IEC 61966-2-1)
                        bytes[byteIndex++] = FloatToSrgbByte(r);
                        bytes[byteIndex++] = FloatToSrgbByte(g);
                        bytes[byteIndex++] = FloatToSrgbByte(b);
                    }
                }
            }
            return bytes;
        }

        /// <summary>
        /// Converts an IEEE 754 binary16 (half-float) bit pattern to a 32-bit float.
        /// Handles normals, sub-normals, infinities, and NaN. No library dependency required.
        /// </summary>
        private static float HalfToFloat(ushort h)
        {
            int sign     = (h >> 15) & 1;
            int exponent = (h >> 10) & 0x1F;
            int mantissa =  h        & 0x3FF;

            int fBits;
            if ( exponent == 0 )
            {
                if ( mantissa == 0 )
                {
                    // Signed zero
                    fBits = sign << 31;
                }
                else
                {
                    // Sub-normal: normalise
                    exponent = 1;
                    while ( (mantissa & 0x400) == 0 )
                    {
                        mantissa <<= 1;
                        exponent--;
                    }
                    mantissa &= ~0x400;
                    fBits = (sign << 31) | ((exponent + (127 - 15)) << 23) | (mantissa << 13);
                }
            }
            else if ( exponent == 31 )
            {
                // Infinity or NaN
                fBits = (sign << 31) | (0xFF << 23) | (mantissa << 13);
            }
            else
            {
                // Normal number
                fBits = (sign << 31) | ((exponent + (127 - 15)) << 23) | (mantissa << 13);
            }

            var u = new FloatUIntUnion();
            u.UInt = (uint)fBits;
            return u.Float;
        }

        private static float AcesNarkowicz(float x)
        {
            float result = (x * (2.51f * x + 0.03f)) / (x * (2.43f * x + 0.59f) + 0.14f);
            return result < 0f ? 0f : (result > 1f ? 1f : result);
        }

        private static byte FloatToSrgbByte(float x)
        {
            if ( x <= 0f ) return 0;
            if ( x >= 1f ) return 255;
            return SrgbLut[(int)(x * 4096f)];
        }

        private static byte[] BuildSrgbLut()
        {
            const int size = 4096;
            var lut = new byte[size + 1]; // +1 so index size is safe
            for ( int i = 0; i <= size; i++ )
            {
                float x = i / (float)size;
                float encoded = x <= 0.0031308f
                    ? 12.92f * x
                    : 1.055f * (float)Math.Pow(x, 1.0 / 2.4) - 0.055f;
                int q = (int)(encoded * 255f + 0.5f);
                lut[i] = (byte)(q < 0 ? 0 : q > 255 ? 255 : q);
            }
            return lut;
        }

        public void DelayNextCapture()
        {
            int remainingFrameTime = _minCaptureTime - (int)_captureTimer.ElapsedMilliseconds;
            if ( remainingFrameTime > 0 )
            {
                Thread.Sleep(remainingFrameTime);
            }
        }

        public void Dispose()
        {
            _duplicatedOutput?.Dispose();
            _output1?.Dispose();
            _output?.Dispose();
            _stagingTexture?.Dispose();
            _smallerTexture?.Dispose();
            _smallerTextureView?.Dispose();
            _device?.Dispose();
            _adapter?.Dispose();
            _factory?.Dispose();
            _lastCapturedFrame = null;
            _disposed = true;
            _desktopDuplicatorInvalid = false;
            _deviceInvalid = false;
        }

        public bool IsDisposed()
        {
            return _disposed;
        }

        private void SaveDebugFrame(byte[] rgb)
        {
            try
            {
                using ( var bmp = new Bitmap(CaptureWidth, CaptureHeight, PixelFormat.Format24bppRgb) )
                {
                    var rect = new Rectangle(0, 0, CaptureWidth, CaptureHeight);
                    var bd = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
                    // Format24bppRgb stores BGR in memory; our array is RGB so swap R↔B per pixel
                    for ( int y = 0; y < CaptureHeight; y++ )
                    {
                        IntPtr row = bd.Scan0 + y * bd.Stride;
                        for ( int x = 0; x < CaptureWidth; x++ )
                        {
                            int s = (y * CaptureWidth + x) * 3;
                            Marshal.WriteByte(row, x * 3,     rgb[s + 2]); // B
                            Marshal.WriteByte(row, x * 3 + 1, rgb[s + 1]); // G
                            Marshal.WriteByte(row, x * 3 + 2, rgb[s]);     // R
                        }
                    }
                    bmp.UnlockBits(bd);
                    string path = Path.Combine(MiscUtils.GetLogDirectory(), "debug_frame.png");
                    bmp.Save(path, ImageFormat.Png);
                    LOG.Info($"DX11: Debug frame saved → {path} ({CaptureWidth}×{CaptureHeight})");
                }
            }
            catch ( Exception ex )
            {
                LOG.Warn($"DX11: SaveDebugFrame failed: {ex.Message}");
            }
        }
    }
}
