namespace VPNCenter.OpenVPN.PackageConfig
{
    internal static partial class Extensions
    {
        public static string? KeywordReplace(this string? @this, string keyword, string assignedValue)
        {
            if (@this is null) return null;
            //.Replace("{proto}", $"proto {portDefinition.ProtoName(configuration)}");
            return @this.Replace("{" + keyword + "}", keyword + " " + assignedValue);
        }
        public static string? KeywordReplace(this string? @this, string keyword, int assignedValue)
        {
            if (@this is null) return null;
            return @this.Replace("{" + keyword + "}", keyword + " " + assignedValue.ToString());
        }
        public static string? KeywordReplace(this string? @this, string keyword, int? assignedValue)
        {
            if (assignedValue.HasValue == false || @this is null) return @this;
            return @this.Replace("{" + keyword + "}", keyword + " " + assignedValue.Value.ToString());
        }
    }
}
