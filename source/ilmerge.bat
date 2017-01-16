rd /q /s ilmerged 2>nul
mkdir ilmerged

"C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\devenv.exe" Jobbr.Runtime.sln /Build "Release|Any CPU"

packages\ILMerge.Tools.2.14.1208\tools\ilmerge.exe /out:ilmerged/Jobbr.Runtime.dll Jobbr.Runtime/bin/Release/Jobbr.Runtime.dll Jobbr.Runtime/bin/Release/Jobbr.Common.dll Jobbr.Runtime/bin/Release/Jobbr.Shared.dll Jobbr.Runtime/bin/Release/CommandLine.dll Jobbr.Runtime/bin/Release/Newtonsoft.Json.dll /target:library /targetplatform:v4,C:\Windows\Microsoft.NET\Framework64\v4.0.30319 /wildcards /internalize:internalize_exclude.txt
