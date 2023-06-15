using DiskStationManager.SecureShell;
using Renci.SshNet;
using System.Net.Http.Headers;
using System.Numerics;
using System.Security.Cryptography;

using static System.Configuration.UserSectionHandler;
using static VPNCenter.OpenVPN.PackageConfig.Settings;

namespace VPNCenter.OpenVPN.PackageConfig
{

    internal static class Program
    {
        private static void SetProfile(Queue<string> cmdLine)
        {
            bool fail = false;
            while (cmdLine.Count > 0 && fail == false)
            {
                if (cmdLine.TryPeek(out string? option))
                {
                    _ = cmdLine.Dequeue();
                    switch (option)
                    {
                        case "host":
                            OpenVPNConfiguration.Profile.OpenVPNServer.Host = cmdLine.Dequeue(); break;
                        case "port":
                            OpenVPNConfiguration.Profile.OpenVPNServer.Port = int.Parse(cmdLine.Dequeue()); break;
                        case "user":
                            OpenVPNConfiguration.Profile.OpenVPNServer.UserName = cmdLine.Dequeue(); break;
                        case "continue":
                            return;
                        case "pass":
                            var input = OpenVPNConfiguration.Profile.OpenVPNServer.GetOrAddAuthenticationMethod(DSMAuthenticationMethod.Password);
                            //System.Console.WriteLine($"Please enter the password for {OpenVPNConfiguration.Profile.OpenVPNServer.UserName}:");
                            //string text = System.Console.ReadLine();
                            input.Password = cmdLine.Dequeue(); 
                            break;
                        case "reset":
                            OpenVPNConfiguration.Profile.OpenVPNServer.RemoveAuthenticationMethod(DSMAuthenticationMethod.None);
                            OpenVPNConfiguration.Profile.OpenVPNServer.RemoveAuthenticationMethod(DSMAuthenticationMethod.KeyboardInteractive);
                            OpenVPNConfiguration.Profile.OpenVPNServer.RemoveAuthenticationMethod(DSMAuthenticationMethod.Password);
                            OpenVPNConfiguration.Profile.OpenVPNServer.RemoveAuthenticationMethod(DSMAuthenticationMethod.PrivateKeyFile);
                            break;
                        case "list":
                            System.Console.WriteLine($"Authentication methods in profile for {OpenVPNConfiguration.Profile.OpenVPNServer.UserName}@{OpenVPNConfiguration.Profile.OpenVPNServer.Host}:");
                            foreach (var method in OpenVPNConfiguration.Profile.OpenVPNServer.AuthenticationMethods)
                            {
                                Console.WriteLine(method.Method);
                            }
                            break;
                        case "save":
                            OpenVPNConfiguration.Profile.Save();
                            break;
                        default:
                            fail = true;
                            break;
                    }
                }
                else
                {
                    fail = true;

                }
            }
            if (fail)
            {
                Console.WriteLine("Failed to parse your profile command.");
                Environment.Exit(-1);
            }
        }
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            DirectoryInfo workFolder = new DirectoryInfo(Environment.CurrentDirectory);

            string entropy = string.IsNullOrWhiteSpace(Default.DPAPIVector)
                ? "synologyforums"
                : GetEntropy(Default.DPAPIVector);

            try { _ = ReadConfiguration(entropy); } catch { }
            
            try
            {
                var cmdLine = new Queue<string>(args);
                while (cmdLine.Count > 0)
                {
                    if (cmdLine.TryPeek(out string? option))
                    {
                        switch (option)
                        {
                            case "e":
                                _ = cmdLine.Dequeue();
                                entropy = cmdLine.Dequeue();
                                try { _ = ReadConfiguration(entropy); } catch { }
                                break;
                            case "profile":
                                _ = cmdLine.Dequeue();
                                SetProfile(cmdLine);
                                break;
                            default:
                                workFolder = new DirectoryInfo(cmdLine.Dequeue());
                                break;
                        }
                    }
                }



                _ = ReadConfiguration(entropy);


                PushConfiguration.PushFromProfile(new DirectoryInfo(@"C:\Users\Sander\Desktop\openvpn\"));
            }
            catch (Exception ex)
            {
                System.Console.WriteLine(ex);
            }




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