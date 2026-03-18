using DiskStationManager.SecureShell;
using Renci.SshNet;
using System;
using System.IO;
using System.Reflection;
using static VPNCenter.OpenVPN.PackageConfig.OpenVPNConfiguration;

namespace VPNCenter.OpenVPN.PackageConfig
{
    internal static class PushConfiguration
    {
        private static void Session_HostKeyChange(object? sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Host Key changed");
            Profile.Save();
        }
        private static void DebugDownload(DSMSession session, string file)
        {
#if DEBUG
            var path = new FileInfo(Path.Combine(new FileInfo(Assembly.GetExecutingAssembly().Location).Directory.FullName, file));
            if (path.Exists) path.Delete();
            session.DownloadFile(file, path);
#endif
        }

        public const string openvpn_conf = "openvpn.conf";
        public const string openvpn_conf_user = $"{openvpn_conf}.user";
        public const string openvpn_ovpn = "openvpn.ovpn";
        internal static void PushFromProfile(DirectoryInfo workLocation)
        {
            DSMSession.ConsoleUI = true;
            DSMSession session = new DSMSession(Profile.OpenVPNServer, Session_HostKeyChange);
            const string vpnCenter = "VPNCenter";
            const string varpackages = "/var/packages";
            const string varPackagesPackageTarget = $"{varpackages}/{vpnCenter}/target/";
            const string usrSynoEtcPackages = "/usr/syno/etc/packages/";
            const string usrSynoEtcPackagesPackage = $"/usr/syno/etc/packages/{vpnCenter}";
            const string vpnCertificates = "/./vpncerts";
            const string filesFrom_var = "VPNCenter.openvpn.tar.gz";
            const string filesFrom_usr = "usr-syno-etc-packages-VPNCenter.tar.gz";
            const string tlsAuthFile = "ta.key";


            var local = new ClientSideFiles(workLocation);

            bool ta_created = false;

            var tmpFolder = $"~/{BConsoleCommand.GetTempPathName()}";

            string tempOpenVpn = $"{tmpFolder}/etc/openvpn";
            string tempOpenVpnUserConfig = $"{tempOpenVpn}/{openvpn_conf_user}";

            string usrPackageOpenVpn = $"{usrSynoEtcPackagesPackage}/openvpn";
            string usrPackageOpenVpnConfig = $"{usrPackageOpenVpn}/{openvpn_conf}";
            string usrPackageOpenVpnUserConfig = $"{usrPackageOpenVpn}/{openvpn_conf_user}";

            var packageFiles = new List<ConsoleFileInfo>();
            var etcFiles = new List<ConsoleFileInfo>();

            ConsoleFileInfo? mgt = null;
            ConsoleFileInfo? ta_key = null;

            var tlsAuthKey = new FileInfo(Path.Combine(local.ServerCertificates.FullName, tlsAuthFile));

            session.ClientExecute(sc =>
            {
                Console.WriteLine($"Starting SSH session for {Profile.OpenVPNServer.UserName}@{Profile.OpenVPNServer.Host} ...");
                sc.Connect();
                var console = session.GetConsole(sc);
                Console.WriteLine($"{Profile.OpenVPNServer.Host}: {console.GetVersionInfo().Version}");

                packageFiles.AddRange(console.GetDirectoryContentsRecursive(sc, varpackages + "/", ".", false)
                                    .Where(p => p.Folder.StartsWith($"/./{vpnCenter}/")));

                mgt = packageFiles.SingleOrDefault(f => f.FileName.Contains("start-stop-status"));

                if (mgt != null)
                {
                    etcFiles.AddRange(console.GetDirectoryContentsRecursive(sc, usrSynoEtcPackagesPackage + "/"));
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
                    createFolders.CreateDirectory(usrSynoEtcPackagesPackage + vpnCertificates);
                }

                checkVPNCenter.Run(createFolders);

                if (ta_key is null)
                {
                    appArmorStopped = true;

                    Console.WriteLine("Generating new tls-auth key ...");

                    checkVPNCenter.Run(s => s
                    .ChangeDirectory(usrSynoEtcPackagesPackage + vpnCertificates)
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
                .Copy($"{usrSynoEtcPackagesPackage}{vpnCertificates}/{tlsAuthFile}", tmpFolder)
                .CopyWithZippedTarball(varPackagesPackageTarget, "etc", filesFrom_var, tmpFolder, cleanup)
                .CopyWithZippedTarball($"{usrSynoEtcPackages}", vpnCenter, filesFrom_usr, tmpFolder, cleanup)
                .ChangeToHomeDirectory()
                .ChangeOwner(tmpFolder, session)
                .ChangeFileMode(tmpFolder, "711", true)
                );

                DebugDownload(session, filesFrom_var);
                DebugDownload(session, filesFrom_usr);

                var serverConfig = VPNCenterConfiguration.PrepareConfiguration(local, session, $"{tmpFolder}/{vpnCenter}");
                Console.WriteLine($"IPv6 enabled             : {(serverConfig.IPv6Enabled ? "yes" : "no")}");
                Console.WriteLine($"LAN access enabled       : {(serverConfig.LANAccessEnabled ? "yes" : "no")}");
                Console.WriteLine($"OpenVPN enabled          : {(serverConfig.ProtocolEnabled ? "yes" : "no")}");
                Console.WriteLine($"Maximum #clients         : {serverConfig.MaxClients}");
                Console.WriteLine($"Maximum #connections/user: {serverConfig.MaxAuthenticatedConnections}");
                if (serverConfig.PortConfigurationAvailable)
                {
                    Console.WriteLine($"Incoming traffic         : {serverConfig.PortConfiguration}");
                }

                var installScript = ScriptBuffer.Create();

                Console.WriteLine("Exchanging certificates ...");
                session.ClientExecute(cp =>
                {
                    cp.Connect();

                    foreach (var file in local.ServerCertificates.GetFiles().Where(f => f.FullName != tlsAuthKey.FullName))
                    {
                        installScript.UploadViaTempFolder(cp, file, tmpFolder, $"{usrSynoEtcPackagesPackage}{vpnCertificates}", fileMode);
                    }
                    installScript.UploadViaTempFolder(cp, local.ServerConfigurationUser, tmpFolder, tempOpenVpn, fileMode);

                    if (ta_created || tlsAuthKey.Exists == false)
                    {
                        DSMSession.DownloadFile(cp, $"{tmpFolder}/{tlsAuthFile}", tlsAuthKey);
                    }
                });
                Console.WriteLine("Writing server configuration ...");

                installScript.CopyFileAndChangeFileMode(tempOpenVpnUserConfig, usrPackageOpenVpnUserConfig, fileMode);
                installScript.CopyFileAndChangeFileMode(tempOpenVpnUserConfig, usrPackageOpenVpnConfig, fileMode);

                checkVPNCenter.Run(installScript);

                checkVPNCenter.Run(s => s
                        .SynoPackage(RunVerb.Start, vpnCenter)
                        .Add("cat /var/log/openvpn.log"));

                checkVPNCenter.Run("cat /var/log/upstart/pkg-VPNCenter-openvpn-server.log");

                Console.WriteLine("Writing client profiles ...");
                foreach (var file in local.ClientCertificates.GetFiles().Where(cert => cert.Extension == ".crt"))
                {
                    string user = Path.GetFileNameWithoutExtension(file.FullName);
                    var inline = new ClientConfigParser(local, user, serverConfig.PortConfiguration);
                    using (var sw = new StreamWriter(Path.Combine(workLocation.FullName, $"{user}{local.Extension}")))
                    {
                        inline.RenderInline(sw);
                    }
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
    }
}
