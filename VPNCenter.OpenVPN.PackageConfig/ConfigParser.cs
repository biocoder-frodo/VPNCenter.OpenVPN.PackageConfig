using DiskStationManager.SecureShell;
using Renci.SshNet.Security;
using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace VPNCenter.OpenVPN.PackageConfig
{
    class Route
    {
        public string Network { get; set; }
        public string NetMask { get; set; }
    }
    internal class ConfigParser
    {
        private static readonly Regex regexRoute = new Regex(@"^push\s+""route\s+(?<net>\S*)\s+(?<mask>\S*)""\s?$");
        private static readonly Regex regexServer = new Regex(@"^server\s+(?<net>\S*)\s+(?<mask>\S*)\s?$");
        private static readonly Regex regexPort = new Regex(@"^port\s+(?<port>[0-9]+)\s?$");
        private Route server;
        private List<Route> routes = new List<Route>();
        private int port;

        public ConfigParser(FileInfo path)
        {


            using (var sr = new StreamReader(path.FullName))
            {
                while (sr.EndOfStream == false)
                {
                    string line = sr.ReadLine();
                    var isServer = regexServer.Match(line);
                    var isRoute = regexRoute.Match(line);
                    var isPort = regexPort.Match(line);

                    if (isServer.Success)
                    {
                        server = new Route() { Network = isServer.Groups["net"].Value, NetMask = isServer.Groups["mask"].Value };
                    }
                    if (isRoute.Success)
                    {
                        routes.Add(new Route() { Network = isRoute.Groups["net"].Value, NetMask = isRoute.Groups["mask"].Value });
                    }
                    if (isPort.Success)
                    {
                        port = int.Parse(isPort.Groups["port"].Value);
                    }
                }
            }

        }
    }

    internal class ClientConfigParser
    {
        private readonly List<string> config = new List<string>();
        private readonly DirectoryInfo root;
        private static readonly Regex regexFile = new Regex(@"^(?<key>\S+)\s+(?<file>\S+)\s?.*$", RegexOptions.Compiled);
        public ClientConfigParser(FileInfo clientTemplate, string userName)
        {
           
            root = clientTemplate.Directory;

            using (var sr = new StreamReader(clientTemplate.FullName))
            {
                while (sr.EndOfStream == false)
                {
                    config.Add(sr.ReadLine().Replace("{user}",userName));
                }
            }
        }
        private void AddInlineCertificate(List<string> document, Match match)
        {
            AddInlineCertificate(document, match.Groups["key"].Value, match.Groups["file"].Value);
        }
        private FileInfo FindCertificate(DirectoryInfo root, string file) 
        {
            var paths = new List<DirectoryInfo>() 
            { 
                new DirectoryInfo(Path.Combine(root.Parent.FullName,"Certificates","Users")),
                new DirectoryInfo(Path.Combine(root.Parent.FullName,"Certificates","Server")),
                root
            };
            for (int i = paths.Count-1; i >= 0; i--)
            {
                //System.Diagnostics.Debug.WriteLine(paths[i].FullName);
                {
                    if (paths[i].Exists == false)
                    {
                        paths.RemoveAt(i);
                    }
                }
            }
            foreach (var path in paths)
            {
                var certificate = new FileInfo(Path.Combine(path.FullName, file));
                if (certificate.Exists) return certificate;
                if (certificate.Extension.Equals(".key")) certificate = new FileInfo(Path.GetFileNameWithoutExtension(certificate.FullName) + ".pem");
                if (certificate.Exists) return certificate;
            }
            
            return new FileInfo(Path.Combine(root.FullName, file));
        }
        private void AddInlineCertificate(List<string> document, string key, string file)
        {
            document.Add($"<{key}>");

            var keyFile = FindCertificate(root, file);
            System.Diagnostics.Debug.WriteLine(keyFile.FullName);
            using (var sr = new StreamReader(keyFile.FullName))
            {
                while (sr.EndOfStream == false)
                    document.Add(sr.ReadLine());
            }
            document.Add($"</{key}>");
        }
        public void RenderInline(StreamWriter writer)
        {
            var inline = new List<string>();
            foreach (var line in config)
            {
                if (line.StartsWith("cert ")
                    || line.StartsWith("key ")
                    || line.StartsWith("ca ")
                    || line.StartsWith("tls-auth "))
                {
                    var doInline = regexFile.Match(line);

                    switch (doInline.Groups["key"].Value)
                    {
                        case "tls-auth":
                            // I can't figure out why the OpenVPN Android app would not take a fully inlined profile..
                            writer.Write($"{line}\n");
                            break;
                        default:
                            AddInlineCertificate(inline, doInline);
                            writer.Write($"#{line}\n");
                            break;
                    }
                }
                else
                {
                    writer.Write($"{line}\n");
                };
            }
            if (inline.Any())
            {
                writer.Write("key-direction 1\n");
                writer.Write("\n");
                foreach (var line in inline)
                {
                    writer.Write($"{line}\n");
                }
            }

        }
    }
}
