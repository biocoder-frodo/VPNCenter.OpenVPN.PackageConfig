using System.Text.RegularExpressions;

namespace VPNCenter.OpenVPN.PackageConfig
{
    internal class VPNCenterPortConfiguration
    {
        private static Regex regexParamValue = new Regex(@"(?<param>(\w|\.)+)=""?(?<value>.*)""?", RegexOptions.Compiled);
        private static Regex regexPorts = new Regex(@"(?<ports>(\d+,)*(\d+))\/(?<proto>(udp|tcp))", RegexOptions.Compiled);
        private static Dictionary<string, VPNCenterPortConfiguration> settings = new Dictionary<string, VPNCenterPortConfiguration>();
        public string? Title { get; private set; }
        public string? Description { get; private set; }
        public bool PortForward { get; private set; }
        public IReadOnlyCollection<ProtocolPort>? DestinationPorts { get; private set; }
        private VPNCenterPortConfiguration() { }
        public static Dictionary<string, VPNCenterPortConfiguration> ReadConfiguration(Stream stream)
        {
            VPNCenterPortConfiguration? current = null;
            string currentName = string.Empty;

            using (var sr = new StreamReader(stream))
            {
                while (sr.EndOfStream == false)
                {
                    var line = sr.ReadLine();
                    if (line is not null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        if (line.StartsWith("[") && line.EndsWith("]"))
                        {
                            if (current is not null)
                            {
                                settings.Add(currentName, current);
                            }
                            current = new VPNCenterPortConfiguration();
                            currentName = line.Substring(1, line.Length - 2);
                        }

                        if (current is not null)
                        {
                            var match = regexParamValue.Match(line);
                            if (match.Success)
                            {
                                string value = match.Groups["value"].Value;
                                switch (match.Groups["param"].Value)
                                {
                                    case "title": current.Title = value; break;
                                    case "desc": current.Description = value; break;
                                    case "port_forward": current.PortForward = value == "yes"; break;
                                    case "dst.ports": current.DestinationPorts = ParsePorts(value); break;
                                    default: break;
                                }
                            }
                        }
                    }
                }

                if (current is not null)
                {
                    settings.Add(currentName, current);
                }
            }
            return settings;
        }

        private static IReadOnlyList<ProtocolPort> ParsePorts(string value)
        {
            var match = regexPorts.Match(value);
            if (match.Success)
            {
                var result = new List<ProtocolPort>();
                var ports = match.Groups["ports"].Value.Split(',');
                foreach (var port in ports)
                {
                    result.Add(new ProtocolPort(match.Groups["proto"].Value, port));
                }
                return result;
            }
            return new List<ProtocolPort>();
        }

    }
}
