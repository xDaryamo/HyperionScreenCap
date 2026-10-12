using log4net;
using Newtonsoft.Json.Linq;
using System;
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
            try
            {
                File.Delete(exe + ".old");
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
                    client.DownloadFile((string) asset["browser_download_url"], exe + ".new");
                    if ( new FileInfo(exe + ".new").Length != (long) asset["size"] )
                        throw new IOException("Downloaded file is incomplete");
                    File.Move(exe, exe + ".old");
                    try
                    {
                        File.Move(exe + ".new", exe);
                    }
                    catch
                    {
                        File.Move(exe + ".old", exe);
                        throw;
                    }
                    notifications.Info($"Version {latest} installed. It starts with the next launch.");
                }
            }
            catch ( Exception ex )
            {
                LOG.Warn($"Update check failed: {ex.Message}");
            }
        }
    }
}
