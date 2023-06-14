using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using DiskStationManager.SecureShell;

namespace VPNCenter.OpenVPN.PackageConfig
{
    public class OpenVPNConfiguration : ConfigurationSection
    {
        private static OpenVPNConfiguration _default_instance = null;
        private static Func<OpenVPNConfiguration> _load_method = null;
        private readonly static object _thread = new object();
        private static int _save_count = 0;
        public static void Initialize(Func<OpenVPNConfiguration> method)
        {
            lock (_thread)
            {
                _load_method = method;
                _default_instance = _load_method();
            }
        }
        public static OpenVPNConfiguration Profile
        {
            get { return _default_instance; }
        }
        public void Save()
        {
            lock (_thread)
            {
                ++_save_count;
                _default_instance.CurrentConfiguration.Save();
                Reload();
                Settings.Default.Reload();
            }
        }
        public void Reload()
        {
            lock (_thread)
            {
                ConfigurationManager.RefreshSection(this.SectionInformation.Name);

                _default_instance = null;
                _default_instance = _load_method();
            }
        }
        public OpenVPNConfiguration() : base()
        {

        }
        [ConfigurationProperty("DSMHost")]
        public DSMHost OpenVPNServer
        {
            get
            {
                return this["DSMHost"] as DSMHost;
            }
            set
            {
                this["DSMHost"] = value;
            }
        }
    }
}
