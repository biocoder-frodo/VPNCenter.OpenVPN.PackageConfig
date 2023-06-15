# VPNCenter.OpenVPN.PackageConfig
 Should make it pretty easy to put a custom OpenVPN configuration in the VPNCenter package
 
 This is a work in progress for DSM 6.2.4

## VPNCenter.OpenVPN.PackageConfig commandline options

profile reset save 
Deletes all authentication and connection details from the config file in your userprofile.

profile reset host .. port .. user .. pass save
Saves your authentication and connection details in your userprofile.

The last argument to the .exe is the location of the workfolder.