namespace VPNCenter.OpenVPN.PackageConfig
{
    internal class ClientSideFiles
    {
        public DirectoryInfo Root { get; }
        public DirectoryInfo ServerCertificates { get; }
        public DirectoryInfo ClientCertificates { get; }

        public FileInfo ServerTemplateConfiguration { get; }
        public FileInfo ClientTemplateConfiguration { get; }
        public FileInfo ServerConfigurationUser { get; }

        public ClientSideFiles(DirectoryInfo workLocation)
        {
            Root = CheckFolder(workLocation);
            ServerConfigurationUser = CheckFile(Root, "openvpn.conf.user", false);
            ServerCertificates = CheckFolder(workLocation, "Certificates", "Server");
            ClientCertificates = CheckFolder(workLocation, "Certificates", "Users");
            var templates = CheckFolder(workLocation, "Templates");
            ServerTemplateConfiguration = CheckFile(templates, "openvpn.conf");
            ClientTemplateConfiguration = CheckFile(templates, "openvpn.ovpn");
        }
        private DirectoryInfo CheckFolder(DirectoryInfo root, params string[] subFolder)
        {
            var path = new List<string>() { root.FullName };
            path.AddRange(subFolder);
            var result = new DirectoryInfo(Path.Combine(path.ToArray()));
            if (result.Exists == false)
            {
                if (subFolder.Length == 0)
                {
                    throw new DirectoryNotFoundException($"{root.FullName} does not exist.");
                }
                else
                {
                    throw new DirectoryNotFoundException($"Expecting a subfolder ./{string.Join('/', subFolder)} in {root.FullName}.");
                }
            }
            return result;
        }
        private FileInfo CheckFile(DirectoryInfo root, string fileName, bool throwIfNotFound = true)
        {
            var result = new FileInfo(Path.Combine(root.FullName, fileName));
            if (result.Exists == false && throwIfNotFound) throw new FileNotFoundException($"Expecting {fileName} in {root.FullName}");
            return result;
        }
    }
}
