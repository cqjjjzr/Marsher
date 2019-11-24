using System;
using System.IO;
using System.Reflection;
using Squirrel;

namespace Marsher
{
    public class MarsherUpdateManager : IDisposable
    {
        private const string UpdateUrlPath = "update_url.txt";
        private const string DefaultUpdateUrl = "https://soft.danmuji.org/Marsher";

        private readonly string _updateUrl = DefaultUpdateUrl;
        private readonly UpdateManager _mgr;
        public MarsherUpdateManager()
        {
            var updateUrlPath = MarsherFilesystem.GetPath(UpdateUrlPath);
            if (!File.Exists(updateUrlPath))
                File.WriteAllText(updateUrlPath, DefaultUpdateUrl);
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
