using DiskStationManager.SecureShell;
using Renci.SshNet;
using System.IO;

namespace VPNCenter.OpenVPN.PackageConfig
{
    internal static partial class Extensions
    {
        private static readonly string appArmor = "/usr/syno/etc.defaults/rc.sysv/apparmor.sh";
        private static readonly Dictionary<RunVerb, string> verbs = Enum.GetValues<RunVerb>().ToDictionary(k => k, v => v.ToString().ToLower());
        internal static ScriptBuffer AppArmor(this ScriptBuffer @this, RunVerb verb)
        {
            if (verb == RunVerb.Stop) Console.WriteLine("Stopping App Armor ...");
            if (verb == RunVerb.Start) Console.WriteLine("Starting App Armor ...");
            return @this.Add($"{appArmor} {verbs[verb]}");
        }
        internal static ScriptBuffer SynoPackage(this ScriptBuffer @this, RunVerb verb, string packageName)
        {
            if (verb == RunVerb.Stop) Console.WriteLine($"Stopping package {packageName}...");
            if (verb == RunVerb.Start) Console.WriteLine($"Starting package {packageName}...");
            return @this.Add($"synopkg {verbs[verb]} {packageName}");
        }
        internal static ScriptBuffer CopyWithZippedTarball(this ScriptBuffer @this, string sourcePath, string subPath, string gz, string destination, ScriptBuffer cleanup)
        {
            cleanup.Add($"rm ~/{gz}");

            return @this
                .ChangeDirectory(sourcePath)
                .Add($"tar -zcf ~/{gz} {subPath}")
                .ChangeDirectory(destination)
                .Add($"tar -xf ~/{gz}");
        }
        internal static ScriptBuffer UploadViaTempFolder(this ScriptBuffer @this, ScpClient client, FileInfo file, string sourceFolder, string destinationFolder, string fileMode)
        {
            DSMSession.UploadFile(client, $"{sourceFolder}/{file.Name}", file);
            return @this.CopyFileAndChangeFileMode($"{sourceFolder}/{file.Name}", $"{destinationFolder}/{file.Name}", fileMode);
        }
        internal static ScriptBuffer ChangeOwner(this ScriptBuffer @this, string folderName, DSMSession session, bool recursive = true) => @this.ChangeOwner(folderName, session.ConnectionInfo.Username, recursive);

    }
}
