# DPXTool prebuild script, for versioning and metadata stuff
# This Overwrites AssemblyInfo.cs, so if you need to modify that class, modify it in here
param(
    [string] $projectDir,
    [string] $configurationName
)

#############################################################
# Write your product information here                       #
# Versioning is handled using GIT                           #
#############################################################
$productName = "DPXTool"
$productVendor = "shadow578"
$productDesc = "A Tool for working with DPX"
$copyrightYear = (Get-Date).Year
#############################################################
#############################################################
#############################################################

function ExitWithCode($exitcode) {
    $host.SetShouldExit($exitcode)
    exit
}


# info: print call details to console (visible in build log)
Write-Output @"
Running scripts\prebuild.ps1 with arguments:
 ProjectDir: $projectDir
 Configuration: $configurationName
"@

# check git command is available
if (!(Get-Command "git" -ErrorAction SilentlyContinue)) {
    Write-Error "GIT command is not available! Is git initialized?"
    ExitWithCode(1)
}

# check we are in a git repo
git status | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Error "GIT status returned with error! Is a git repo initialized?"
    ExitWithCode(2)
}

# run git describe to get version info
$describe = (git describe --dirty --long)

# run git tag to get latest tag (for file version)
$tag = (git tag)

# run git diff to get insertions and deletions
$diff = (git diff HEAD^ --shortstat)

# check if result of git describe contains "dirty" keyword
# if not, we dont include insertions/deletions in our version string (is not needed)
$version = "$($describe)_$($configurationName)"
if ($describe.Contains("dirty")) {
    $version = "$version [$diff]"
}

# info: write version info to output
Write-Output "Version info: $tag // $version"

# build & format AssemblyInfo.cs source
$assemblyInfo = @"
//This class is automatically generated by scripts\prebuild.ps1
//Do not modify this class directly. Instead, modify scripts\prebuild.ps1
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: ComVisible(false)]
[assembly: Guid("9c2209b1-dfdd-4d20-b63f-f7301eb1e312")]

[assembly: AssemblyTitle("$productName")]
[assembly: AssemblyProduct("$productName")]
[assembly: AssemblyDescription("$productDesc")]
[assembly: AssemblyCopyright("Copyright (C) $copyrightYear $productVendor")]

[assembly: AssemblyVersion("$tag")]
[assembly: AssemblyFileVersion("$tag")]
[assembly: AssemblyInformationalVersion("$version")]
"@

# write AssemblyInfo.cs
Write-Output "Writing AssemblyInfo.cs"
$assemblyInfo | Out-File -FilePath "$projectDir\AssemblyInfo.cs"
ExitWithCode(0)