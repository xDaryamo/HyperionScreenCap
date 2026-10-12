using log4net;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Windows.Forms;

namespace HyperionScreenCap.Helper
{
    static class Updater
    {
        private static readonly ILog LOG = LogManager.GetLogger(typeof(Updater));
        private const string LATEST_RELEASE_URL = "https://api.github.com/repos/xDaryamo/HyperionScreenCap/releases/latest";

        public static void Run(NotificationUtils notifications)
        {
            string exe = Application.ExecutablePath;
            string old = exe + ".old";
            string fresh = Path.Combine(Path.GetTempPath(), "HyperionScreenCap.exe.new");
            try
            {
                if ( File.Exists(Path.ChangeExtension(exe, ".pdb")) )
                    return;
                using ( var client = new WebClient() )
                {
                    client.Headers[HttpRequestHeader.UserAgent] = "HyperionScreenCap";
                    JObject release = JObject.Parse(client.DownloadString(LATEST_RELEASE_URL));
                    Version latest = Version.Parse(((string) release["tag_name"]).TrimStart('v'));
                    Version current = Assembly.GetExecutingAssembly().GetName().Version;
                    if ( latest <= new Version(current.Major, current.Minor, current.Build) )
                        return;
                    JToken asset = release["assets"].First(a => ((string) a["name"]).EndsWith(".exe"));
                    long size = (long) asset["size"];
                    client.DownloadFile((string) asset["browser_download_url"], fresh);
                    if ( new FileInfo(fresh).Length != size )
                        throw new IOException("Downloaded file is incomplete");
                    try
                    {
                        File.Delete(old);
                        File.Move(exe, old);
                        try
                        {
                            File.Move(fresh, exe);
                        }
                        catch
                        {
                            File.Move(old, exe);
                            throw;
                        }
                    }
                    catch ( UnauthorizedAccessException )
                    {
                        LOG.Info("The program folder is not writable. Asking for administrator rights.");
                        var elevated = new ProcessStartInfo("cmd.exe", SwapArguments(exe, old, fresh))
                        {
                            Verb = "runas",
                            WindowStyle = ProcessWindowStyle.Hidden
                        };
                        using ( Process process = Process.Start(elevated) )
                            process.WaitForExit();
                        if ( new FileInfo(exe).Length != size )
                            throw new IOException("The elevated update did not replace the program");
                    }
                    notifications.Info($"Version {latest} installed. It starts with the next launch.");
                }
            }
            catch ( Exception ex )
            {
                LOG.Warn($"Update check failed: {ex.Message}");
            }
        }

        public static string SwapArguments(string exe, string old, string fresh)
        {
            return $"/s /c \"del /q \"{old}\" 2>nul & move /y \"{exe}\" \"{old}\" && (move /y \"{fresh}\" \"{exe}\" || move /y \"{old}\" \"{exe}\")\"";
        }
    }
}
