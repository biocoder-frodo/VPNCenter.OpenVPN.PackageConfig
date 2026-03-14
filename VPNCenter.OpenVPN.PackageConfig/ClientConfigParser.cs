using System.Text.RegularExpressions;

namespace VPNCenter.OpenVPN.PackageConfig
{
    internal class ClientConfigParser
    {
        private readonly List<string> config = new List<string>();
        private readonly DirectoryInfo root;
        private static readonly Regex regexFile = new Regex(@"^(?<key>\S+)\s+(?<file>\S+)\s?.*$", RegexOptions.Compiled);
        public ClientConfigParser(FileInfo clientTemplate, string userName, ProtocolPort portDefinition)
        {

            root = clientTemplate.Directory;

            using (var sr = new StreamReader(clientTemplate.FullName))
            {
                while (sr.EndOfStream == false)
                {
                    config.Add(sr.ReadLine()
                        .Replace("{user}", userName)
                        .Replace("{port}", portDefinition.Port.ToString())
                        .Replace("{proto}", $"proto {(portDefinition.Protocol == Protocol.UDP ? "udp":"tcp-client")}")
                        );
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
            for (int i = paths.Count - 1; i >= 0; i--)
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
                if (certificate.Extension.Equals(".key")) certificate = new FileInfo(Path.Combine(certificate.Directory.FullName, Path.GetFileNameWithoutExtension(certificate.FullName) + ".pem"));
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
                            AddInlineCertificate(inline, doInline);
                            writer.Write($"#{line}\n");
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
