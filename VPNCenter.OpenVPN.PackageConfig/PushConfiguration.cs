using DiskStationManager.SecureShell;
using Renci.SshNet;
using VPNCenter.OpenVPN.PackageConfig;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static System.Configuration.UserSectionHandler;
using static VPNCenter.OpenVPN.PackageConfig.OpenVPNConfiguration;
using System.IO;

namespace VPNCenter.OpenVPN.PackageConfig
{
    internal static class PushConfiguration
    {
        internal static void PushFromProfile(DirectoryInfo workLocation)
        {
            var serverCertificatesLocation = new DirectoryInfo(Path.Combine(workLocation.FullName, "Certificates", "Server"));
            var clientCertificatesLocation = new DirectoryInfo(Path.Combine(workLocation.FullName, "Certificates", "Users"));
            var templatesLocation = new DirectoryInfo(Path.Combine(workLocation.FullName, "Templates"));

            var serverTemplate = new FileInfo(Path.Combine(templatesLocation.FullName, "openvpn.conf"));
            var clientTemplate = new FileInfo(Path.Combine(templatesLocation.FullName, "openvpn.ovpn"));

            if (serverCertificatesLocation.Exists == false) throw new FileNotFoundException($"Expecting a subfolder ./Certificates/Server in {workLocation.FullName}");
            if (clientCertificatesLocation.Exists == false) throw new FileNotFoundException($"Expecting a subfolder ./Certificates/Client in {workLocation.FullName}");
            if (serverTemplate.Exists == false) throw new FileNotFoundException($"Expecting ./Templates/openvpn.conf in {workLocation.FullName}");
            if (clientTemplate.Exists == false) throw new FileNotFoundException($"Expecting ./Templates/openvpn.ovpn in {workLocation.FullName}");

            const string appArmour = "/usr/syno/etc.defaults/rc.sysv/apparmor.sh";

            var test = new ConfigParser(serverTemplate);

            DSMSession.ConsoleUI = true;
            DSMSession session = new DSMSession(Profile.OpenVPNServer, Session_HostKeyChange);
            const string vpnCenter = "VPNCenter";
            const string varpackages = "/var/packages";
            const string etcpackages = "/usr/syno/etc/packages/" + vpnCenter;
            const string vpnCertificates = "/./vpncerts";
            bool ta_created = false;

            var tmpFolder = $"~/{BConsoleCommand.GetTempPathName()}";
            var packageFiles = new List<ConsoleFileInfo>();
            var etcFiles = new List<ConsoleFileInfo>();

            ConsoleFileInfo? mgt = null;
            ConsoleFileInfo? ta_key = null;

            var tlsAuthKey = new FileInfo(Path.Combine(serverCertificatesLocation.FullName, "ta.key"));

            session.ClientExecute(sc =>
            {
                Console.WriteLine($"Starting SSH session for {OpenVPNConfiguration.Profile.OpenVPNServer.UserName}@{OpenVPNConfiguration.Profile.OpenVPNServer.Host} ...");
                sc.Connect();
                var console = session.GetConsole(sc);
                Console.WriteLine($"{OpenVPNConfiguration.Profile.OpenVPNServer.Host}: {console.GetVersionInfo().Version}");

                packageFiles.AddRange(console.GetDirectoryContentsRecursive(sc, varpackages + "/", ".", false)
                                    .Where(p => p.Folder.StartsWith("/./VPNCenter/")));

                mgt = packageFiles.SingleOrDefault(f => f.FileName.Contains("start-stop-status"));

                if (mgt != null)
                {
                    etcFiles.AddRange(console.GetDirectoryContentsRecursive(sc, etcpackages + "/"));
                    ta_key = etcFiles.SingleOrDefault(f => f.FileName.Contains("ta.key"));
                }
            });

            if (mgt is null)
            {
                System.Console.WriteLine("VPNCenter is not installed?");
                return;
            }

            //session.DownloadFile($"{tmpFolder}/etc/openvpn/openvpn.conf", out MemoryStream vpnConfig);
            //var serverConfig = new StreamReader(vpnConfig).ReadToEnd();
            //vpnConfig.Dispose();

            var checkVPNCenter = new SudoSession(session);
            try
            {
                Console.WriteLine("Stopping package ...");
                checkVPNCenter.Run(new string[]
                {
                    $"{appArmour} stop",
                    $"synopkg stop {vpnCenter}"
                });

                var createFolders = new List<string>
                {
                    $"mkdir {tmpFolder}",
                    $"chown -R {session.ConnectionInfo.Username} {tmpFolder}"
                };

                if (etcFiles.Where(p => p.Folder.Equals(vpnCertificates)).Any() == false)
                {
                    createFolders.Add($"mkdir {etcpackages}{vpnCertificates}");
                }
                checkVPNCenter.Run(createFolders.ToArray());

                if (ta_key is null)
                {
                    Console.WriteLine("Generating new tls-auth key ...");
                    checkVPNCenter.Run(new string[]
                    {
                    $"cd {etcpackages}{vpnCertificates}",
                 //   $"{appArmour} stop",
                    $"openvpn --genkey --secret ta.key",
                    "chmod 0400 ta.key",
                 //   $"{appArmour} start",
                    "chmod 0400 ta.key",
                    });
                    ta_created = true;
                }
                if (tlsAuthKey.Exists && ta_created) tlsAuthKey.Delete();

                checkVPNCenter.Run(new string[]
                {
                $"cp {etcpackages}{vpnCertificates}/ta.key {tmpFolder}",
                "cd /var/packages/VPNCenter/target/",
                "tar -zcf ~/VPNCenter.openvpn.tar.gz etc",
                $"cd {tmpFolder}",
                "tar -xf ~/VPNCenter.openvpn.tar.gz",
                "rm ~/VPNCenter.openvpn.tar.gz",
                 "cd ~",
                $"chown -R {session.ConnectionInfo.Username} {tmpFolder}",
                $"chmod -R 711 {tmpFolder}"
                });

                var installFiles = new List<string>();
                Console.WriteLine("Exchanging certificates ...");
                session.ClientExecute(cp =>
                {
                    cp.Connect();
                    foreach (var file in serverCertificatesLocation.GetFiles().Where(f => f.FullName != tlsAuthKey.FullName))
                    {
                        session.UploadFile(cp, $"{tmpFolder}/{file.Name}", file);
                        PlaceFile(installFiles, $"{tmpFolder}/{file.Name}", $"{etcpackages}{vpnCertificates}/{file.Name}");
                    }

                    session.UploadFile(cp, $"{tmpFolder}/etc/openvpn/openvpn.conf.user", serverTemplate);

                });

                session.ClientExecute(cp =>
                {
                    if (ta_created || tlsAuthKey.Exists == false)
                    {
                        session.DownloadFile(cp, $"{tmpFolder}/ta.key", tlsAuthKey);
                    }
                });
                Console.WriteLine("Writing server configuration ...");
                PlaceFile(installFiles, $"{tmpFolder}/etc/openvpn/openvpn.conf.user", $"{etcpackages}/openvpn/openvpn.conf.user");
                PlaceFile(installFiles, $"{tmpFolder}/etc/openvpn/openvpn.conf.user", $"{etcpackages}/openvpn/openvpn.conf");

                installFiles.AddRange(new string[]
                {
                    "cd ~",
                    $"rm -r {tmpFolder}"
                });

                checkVPNCenter.Run(installFiles.ToArray());
                Console.WriteLine("Starting package ...");
                checkVPNCenter.Run(new string[]
                {
                    $"synopkg start {vpnCenter}",
                    "cat /var/log/openvpn.log"
                });
                Console.WriteLine("Writing client profiles ...");
                foreach (var file in clientCertificatesLocation.GetFiles().Where(cert => cert.Extension == ".crt"))
                {
                    string user = Path.GetFileNameWithoutExtension(file.FullName);
                    var inline = new ClientConfigParser(clientTemplate, user);
                    using (var sw = new StreamWriter(Path.Combine(workLocation.FullName, $"{user}.ovpn")))
                    {
                        inline.RenderInline(sw);
                    };
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine(ex);
                checkVPNCenter.Run($"{appArmour} start");
            }
        }
        private static void PlaceFile(List<string> script, string source, string destination)
        {
            script.Add($"cp {source} {destination}");
            script.Add($"chmod 0400 {destination}");
        }

        private static void Session_HostKeyChange(object? sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Host Key changed");
            OpenVPNConfiguration.Profile.Save();
        }

    }


}
