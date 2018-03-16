@echo off
rem add potential paths to PATH
path=%path%;C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin
path=%path%;C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin
path=%path%;C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin

rem Build Initializer
msbuild /property:Configuration=Release;OutDir=..\DeftConfig\content ..\DeftConfigInitializer\DeftConfigInitializer.csproj 

rem Clean up DeftConfig\content
del ..\DeftConfig\content\DeftConfigInitializer.exe.config
del ..\DeftConfig\content\DeftConfigInitializer.pdb

rem Delete everything in Package
del Package\*.* /q

rem Package
nuget pack ..\DeftConfig\DeftConfig.nuspec -o Package

rem Publish
nuget push Package\*.nupkg -Source https://int.nugettest.org/api/v2/package
