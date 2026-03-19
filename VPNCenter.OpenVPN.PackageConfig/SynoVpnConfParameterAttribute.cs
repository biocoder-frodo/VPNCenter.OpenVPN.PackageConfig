namespace VPNCenter.OpenVPN.PackageConfig
{
    [AttributeUsage(AttributeTargets.Property)]
    internal class SynoVpnConfParameterAttribute : Attribute
    {
        public string Name { get; }
        public SynoVpnConfParameterAttribute(string name)
        {
            Name = name;
        }
    }
}
