using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Squirrel;

namespace Marsher
{
    public class MarsherUpdateManager : IDisposable
    {
        private const string UpdateUrlPath = "update_url.txt";
        private const string NewVersionIdentifierPath = "CURRENT_VERSION";
        private const string DefaultUpdateUrl = "https://soft.danmuji.org/Marsher";

        public readonly bool Updated = false;
        private readonly string _updateUrl = DefaultUpdateUrl;
        private readonly UpdateManager _mgr;
        public MarsherUpdateManager()
        {
            var updateUrlPath = MarsherFilesystem.GetPath(UpdateUrlPath);
            var nf = MarsherFilesystem.GetPath(NewVersionIdentifierPath);
            if (!File.Exists(nf))
            {
                File.WriteAllText(nf, Assembly.GetCallingAssembly().GetName().Version.ToString());
                Updated = true;
            }
            else
            {
                var versionStr = File.ReadAllText(nf).Trim();
                Version.TryParse(versionStr, out var version);
                if (version != null && Assembly.GetCallingAssembly().GetName().Version > version)
                {
                    File.WriteAllText(nf, Assembly.GetCallingAssembly().GetName().Version.ToString());
                    Updated = true;
                }
            }
            if (!File.Exists(updateUrlPath))
            {
                Updated = false;
                File.WriteAllText(updateUrlPath, DefaultUpdateUrl);
            }
            else
                _updateUrl = File.ReadAllText(updateUrlPath);
            try
            {
                _mgr = new UpdateManager(_updateUrl);
            }
            catch (Exception)
            {
                _mgr = null;
            }
        }

        public void CheckUpdate()
        {
            _mgr?.UpdateApp();
        }

        public string GetCurrentVersion()
        {
            return _mgr?.CurrentlyInstalledVersion()?.ToString() ?? Assembly.GetCallingAssembly().GetName().Version.ToString();
        }

        public void Dispose()
        {
            _mgr?.Dispose();
        }
    }
}
