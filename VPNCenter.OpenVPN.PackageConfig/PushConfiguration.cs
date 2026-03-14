using System;
using DiskStationManager.SecureShell;
using static VPNCenter.OpenVPN.PackageConfig.OpenVPNConfiguration;

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



            //var test = new ConfigParser(serverTemplate);

            DSMSession.ConsoleUI = true;
            DSMSession session = new DSMSession(Profile.OpenVPNServer, Session_HostKeyChange);
            const string vpnCenter = "VPNCenter";
            const string varpackages = "/var/packages";
            const string etcpackages = "/usr/syno/etc/packages/" + vpnCenter;
            const string vpnCertificates = "/./vpncerts";
            const string tlsAuthFile = "ta.key";
            bool ta_created = false;

            var tmpFolder = $"~/{BConsoleCommand.GetTempPathName()}";
            var packageFiles = new List<ConsoleFileInfo>();
            var etcFiles = new List<ConsoleFileInfo>();

            ConsoleFileInfo? mgt = null;
            ConsoleFileInfo? ta_key = null;

            var tlsAuthKey = new FileInfo(Path.Combine(serverCertificatesLocation.FullName, tlsAuthFile));

            session.ClientExecute(sc =>
            {
                Console.WriteLine($"Starting SSH session for {Profile.OpenVPNServer.UserName}@{Profile.OpenVPNServer.Host} ...");
                sc.Connect();
                var console = session.GetConsole(sc);
                Console.WriteLine($"{OpenVPNConfiguration.Profile.OpenVPNServer.Host}: {console.GetVersionInfo().Version}");

                packageFiles.AddRange(console.GetDirectoryContentsRecursive(sc, varpackages + "/", ".", false)
                                    .Where(p => p.Folder.StartsWith($"/./{vpnCenter}/")));

                mgt = packageFiles.SingleOrDefault(f => f.FileName.Contains("start-stop-status"));

                if (mgt != null)
                {
                    etcFiles.AddRange(console.GetDirectoryContentsRecursive(sc, etcpackages + "/"));
                    ta_key = etcFiles.SingleOrDefault(f => f.FileName.Contains(tlsAuthFile));
                }
            });

            if (mgt is null)
            {
                Console.WriteLine("VPNCenter is not installed?");
                return;
            }


            bool appArmorStopped = false;
            var cleanup = ScriptBuffer.Create()
                .ChangeToHomeDirectory()
                .Add($"rm -r {tmpFolder}");

            const string fileMode = "0400";
            var checkVPNCenter = new SudoSession(session);
            try
            {
                checkVPNCenter.Run(s => s.SynoPackage(RunVerb.Stop, vpnCenter));

                var createFolders = ScriptBuffer.Create()
                    .CreateDirectory(tmpFolder)
                    .ChangeOwner(tmpFolder, session);


                if (etcFiles.Where(p => p.Folder.Equals(vpnCertificates)).Any() == false)
                {
                    createFolders.CreateDirectory(etcpackages + vpnCertificates);
                }

                checkVPNCenter.Run(createFolders);

                if (ta_key is null)
                {
                    appArmorStopped = true;

                    Console.WriteLine("Generating new tls-auth key ...");

                    checkVPNCenter.Run(s => s
                    .ChangeDirectory(etcpackages + vpnCertificates)
                    .AppArmor(RunVerb.Stop)
                    .Add($"openvpn --genkey --secret {tlsAuthFile}")
                    .ChangeFileMode(tlsAuthFile, fileMode)
                    .AppArmor(RunVerb.Start)
                    .ChangeFileMode(tlsAuthFile, fileMode));

                    appArmorStopped = false;

                    ta_created = true;
                }
                if (tlsAuthKey.Exists && ta_created) tlsAuthKey.Delete();

                checkVPNCenter.Run(s => s
                .Copy($"{etcpackages}{vpnCertificates}/{tlsAuthFile}", tmpFolder)
                .CopyWithZippedTarball("/var/packages/VPNCenter/target/", "etc", "VPNCenter.openvpn.tar.gz", tmpFolder, cleanup)
                .CopyWithZippedTarball($"{etcpackages}/../", vpnCenter, "usr-syno-etc-packages-VPNCenter.tar.gz", tmpFolder, cleanup)
                .ChangeToHomeDirectory()
                .ChangeOwner(tmpFolder, session)
                .ChangeFileMode(tmpFolder, "711", true)
                );

                var serverConfig = VPNCenterConfiguration.ReadConfiguration(session, $"{tmpFolder}/{vpnCenter}");
                var portConfig = serverConfig.PortConfiguration.DestinationPorts.Single();

                var installScript = ScriptBuffer.Create();

                Console.WriteLine("Exchanging certificates ...");
                session.ClientExecute(cp =>
                {
                    cp.Connect();
                    foreach (var file in serverCertificatesLocation.GetFiles().Where(f => f.FullName != tlsAuthKey.FullName))
                    {
                        installScript.UploadViaTempFolder(cp, file, tmpFolder, $"{etcpackages}{vpnCertificates}", fileMode);
                    }
                    var test = new ConfigParser(serverTemplate);
                    DSMSession.UploadFile(cp, $"{tmpFolder}/etc/openvpn/openvpn.conf.user", serverTemplate);

                });

                session.ClientExecute(cp =>
                {
                    if (ta_created || tlsAuthKey.Exists == false)
                    {
                        session.DownloadFile(cp, $"{tmpFolder}/{tlsAuthFile}", tlsAuthKey);
                    }
                });
                Console.WriteLine("Writing server configuration ...");
              
                installScript.CopyFileAndChangeFileMode($"{tmpFolder}/etc/openvpn/openvpn.conf.user", $"{etcpackages}/openvpn/openvpn.conf.user", fileMode);
                installScript.CopyFileAndChangeFileMode($"{tmpFolder}/etc/openvpn/openvpn.conf.user", $"{etcpackages}/openvpn/openvpn.conf", fileMode);

                checkVPNCenter.Run(installScript);

                checkVPNCenter.Run(s => s
                        .SynoPackage(RunVerb.Start, vpnCenter)
                        .Add("cat /var/log/openvpn.log"));

                Console.WriteLine("Writing client profiles ...");
                foreach (var file in clientCertificatesLocation.GetFiles().Where(cert => cert.Extension == ".crt"))
                {
                    string user = Path.GetFileNameWithoutExtension(file.FullName);
                    var inline = new ClientConfigParser(clientTemplate, user, portConfig);
                    using (var sw = new StreamWriter(Path.Combine(workLocation.FullName, $"{user}.ovpn")))
                    {
                        inline.RenderInline(sw);
                    }
                    ;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);

            }
            finally
            {
                if (appArmorStopped)
                {
                    Console.WriteLine("Starting App Armor ...");
                    checkVPNCenter.Run(s => s.AppArmor(RunVerb.Start));
                }

                Console.WriteLine("Performing cleanup ...");
                checkVPNCenter.Run(cleanup);
            }
        }

        private static void Session_HostKeyChange(object? sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Host Key changed");
            OpenVPNConfiguration.Profile.Save();
        }
    }
}
