using DiskStationManager.SecureShell;
using System.Security.Cryptography;

using static System.Configuration.UserSectionHandler;
using static VPNCenter.OpenVPN.PackageConfig.Settings;

namespace VPNCenter.OpenVPN.PackageConfig
{

    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            bool oneTimeVector = false;
            string vector = string.Empty;
            DirectoryInfo workFolder = new DirectoryInfo(Environment.CurrentDirectory);

            try
            {
                var cmdline = new Queue<string>(args);
                while (cmdline.Count > 0)
                {
                    if (cmdline.TryPeek(out string? option))
                    {
                        switch (option)
                        {
                            case "e":
                                _ = cmdline.Dequeue();
                                vector = cmdline.Dequeue();
                                oneTimeVector = true;
                                break;
                            case "profile":
                                _ = cmdline.Dequeue();
                                break;
                            default:
                                workFolder = new DirectoryInfo(cmdline.Dequeue());
                                break;
                        }
                    }
                }

                string entropy = string.IsNullOrWhiteSpace(Default.DPAPIVector)
                    ? "synologyforums"
                    : GetEntropy(oneTimeVector ? vector : Default.DPAPIVector);

                _ = ReadConfiguration(entropy);


                PushConfiguration.PushFromProfile(new DirectoryInfo(@"C:\Users\Sander\Desktop\openvpn\"));
            }
            catch (Exception ex)
            {
                System.Console.WriteLine(ex);
            }

            //        var serverCertificatesLocation = new DirectoryInfo(Path.Combine(workLocation.FullName, "Certificates", "Server"));
            //        var templatesLocation = new DirectoryInfo(Path.Combine(workLocation.FullName, "Templates"));

            //        var serverTemplate = new FileInfo(Path.Combine(templatesLocation.FullName, "openvpn.conf"));
            //        var clientTemplate = new FileInfo(Path.Combine(templatesLocation.FullName, "openvpn.ovpn"));
            //        const string appArmour = "/usr/syno/etc.defaults/rc.sysv/apparmor.sh";

            //        var test = new ConfigParser(serverTemplate);



            //        //config.OpenVPNServer.FingerPrint = new byte[] { };
            //        //config.OpenVPNServer.Host = "diskstation";
            //        //config.OpenVPNServer.UserName = "admsander";
            //        //config.OpenVPNServer.Port = 7135;
            //        //config.OpenVPNServer.RemoveAuthenticationMethod(DSMAuthenticationMethod.None);
            //        //config.OpenVPNServer.RemoveAuthenticationMethod(DSMAuthenticationMethod.KeyboardInteractive);
            //        //config.OpenVPNServer.RemoveAuthenticationMethod(DSMAuthenticationMethod.Password);
            //        //config.OpenVPNServer.RemoveAuthenticationMethod(DSMAuthenticationMethod.PrivateKeyFile);

            //        //var k = config.OpenVPNServer.GetOrAddAuthenticationMethod(DSMAuthenticationMethod.Password);

            //        //config.Save();
        }
        internal static OpenVPNConfiguration ReadConfiguration(string entropy)
        {

            WrappedPassword<DSMAuthentication>.SetEntropy(entropy);
            WrappedPassword<DSMAuthenticationKeyFile>.SetEntropy(entropy);
            WrappedPassword<DefaultProxy>.SetEntropy(entropy);

            OpenVPNConfiguration.Initialize(GetSection<OpenVPNConfiguration>);

            return OpenVPNConfiguration.Profile;
        }
        private static string GetEntropy(string input)
        {
            FileInfo source = null;
            try
            {
                source = new FileInfo(input);
                if (source.Exists)
                {
                    using (var sr = new StreamReader(source.FullName))
                    {
                        return sr.ReadToEnd();
                    }
                }
            }
            catch (ArgumentException)
            {
                //don't care if it's not a filename
            }

            return input;

        }
    }
}