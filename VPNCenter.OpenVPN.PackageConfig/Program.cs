using System;
using DiskStationManager.SecureShell;
using System.Security.Cryptography;

using static System.Configuration.UserSectionHandler;
using static VPNCenter.OpenVPN.PackageConfig.Settings;
using static VPNCenter.OpenVPN.PackageConfig.OpenVPNConfiguration;

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
                            Profile.OpenVPNServer.Host = cmdLine.Dequeue(); break;
                        case "port":
                            Profile.OpenVPNServer.Port = int.Parse(cmdLine.Dequeue()); break;
                        case "user":
                            Profile.OpenVPNServer.UserName = cmdLine.Dequeue(); break;
                        case "continue":
                            return;
                        case "pass":
                            var input = Profile.OpenVPNServer.GetOrAddAuthenticationMethod(DSMAuthenticationMethod.Password);
                            Console.WriteLine($"Please enter the password for {Profile.OpenVPNServer.UserName}:");
                            input.setPassword(DpApiString.SecureStringFromConsole());
                            break;
                        case "reset":
                            Profile.OpenVPNServer.RemoveAuthenticationMethod(DSMAuthenticationMethod.None);
                            Profile.OpenVPNServer.RemoveAuthenticationMethod(DSMAuthenticationMethod.KeyboardInteractive);
                            Profile.OpenVPNServer.RemoveAuthenticationMethod(DSMAuthenticationMethod.Password);
                            Profile.OpenVPNServer.RemoveAuthenticationMethod(DSMAuthenticationMethod.PrivateKeyFile);
                            Profile.OpenVPNServer.Port = 22;
                            Profile.OpenVPNServer.Host = string.Empty;
                            Profile.OpenVPNServer.UserName = string.Empty;
                            break;
                        case "list":
                            Console.WriteLine($"Authentication methods in profile for {Profile.OpenVPNServer.UserName}@{Profile.OpenVPNServer.Host}:");
                            foreach (var method in Profile.OpenVPNServer.AuthenticationMethods)
                            {
                                Console.WriteLine(method.Method);
                            }
                            break;
                        case "save":
                            Profile.Save();
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

                PushConfiguration.PushFromProfile(workFolder);
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

            Initialize(GetSection<OpenVPNConfiguration>);

            return Profile;
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