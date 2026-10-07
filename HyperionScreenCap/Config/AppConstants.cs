using System;
using System.IO;

namespace HyperionScreenCap.Config
{
    class AppConstants
    {
        /// <summary>
        /// Number of screen capture failure attemps after which screen capture should be re-initialized.
        /// </summary>
        public const int REINIT_CAPTURE_AFTER_ATTEMPTS = 15;

        /// <summary>
        /// Amount of time to wait before attempting screen capture after a failure.
        /// </summary>
        public const int CAPTURE_FAILED_COOLDOWN_MILLIS = 1000;

        /// <summary>
        /// Maximum backoff delay (in milliseconds) between capture retry attempts.
        /// </summary>
        public const int MAX_BACKOFF_MILLIS = 30000;

        public const int DEVICE_LOST_RETRY_MILLIS = 3000;

        /// <summary>
        /// Amount of time to wait before resuming screen capture.
        /// </summary>
        public const int CAPTURE_RESUME_GRACE_MILLIS = 5000;

        /// <summary>
        /// The send and receive timeout for the socket used by the Proto client.
        /// </summary>
        public const int PROTO_CLIENT_SOCKET_TIMEOUT = 2500;

        /// <summary>
        /// SDR reference white level in nits used for HDR tone-mapping normalisation.
        /// </summary>
        public const float HDR_SDR_REFERENCE_WHITE_NITS = 80f;

        /// <summary>
        /// File name for debugging screen captured by this application.
        /// </summary>
        public static string DEBUG_IMAGE_FILE_NAME = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            + Path.DirectorySeparatorChar + "hyperion-capture-debug.png";

        /// <summary>
        /// Name of the application's log file.
        /// </summary>
        public static string LOG_FILE_NAME = "hyperion-screen-capture.log";

        /// <summary>
        /// Defines the constants related to the taskbar icon.
        /// </summary>
        public static class TrayIcon
        {
            /// <summary>
            /// Tooltip message displayed when screen capture is disabled.
            /// </summary>
            public const string TOOLTIP_CAPTURE_DISABLED = "Hyperion Screen Capture (Disabled)";

            /// <summary>
            /// Tooltip message displayed when screen capture is enabled.
            /// </summary>
            public const string TOOLTIP_CAPTURE_ENABLED = "Hyperion Screen Capture (Enabled)";

            /// <summary>
            /// Start capture menu option text.
            /// </summary>
            public const string MENU_TXT_START_CAPTURE = "Start Capture";

            /// <summary>
            /// Stop capture menu option text.
            /// </summary>
            public const string MENU_TXT_STOP_CAPTURE = "Stop Capture";

            /// <summary>
            /// Setup menu option text.
            /// </summary>
            public const string MENU_TXT_SETUP = "Setup";

            /// <summary>
            /// Setup menu option text.
            /// </summary>
            public const string MENU_TXT_DONATE = "Buy Me a Coffee";

            /// <summary>
            /// Exit menu option text.
            /// </summary>
            public const string MENU_TXT_EXIT = "Exit";
        }
    }
}
