using System;

namespace HyperionScreenCap.Capture
{
    interface IScreenCapture : IDisposable
    {

        int CaptureWidth { get; }
        int CaptureHeight { get; }

        void Initialize();

        byte[] Capture();

        void DelayNextCapture();

        bool IsDisposed();

        /// <summary>
        /// Returns true when the most recently captured frame was in HDR (R16G16B16A16_Float) format.
        /// Always false for non-DX11 capture methods.
        /// </summary>
        bool IsHdrActive { get; }

    }
}
