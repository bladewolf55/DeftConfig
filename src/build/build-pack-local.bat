@echo off
rem add potential paths to PATH
path=%path%;C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin
path=%path%;C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin
path=%path%;C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin
path=%path%;C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin
path=%path%;C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin
path=%path%;C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin

rem Build Initializer
msbuild /property:Configuration=Release;OutDir=..\DeftConfig\content ..\DeftConfigInitializer\DeftConfigInitializer.csproj 

rem Clean up DeftConfig\content
del ..\DeftConfig\content\DeftConfigInitializer.exe.config
del ..\DeftConfig\content\DeftConfigInitializer.pdb

rem Package
nuget pack ..\DeftConfig\DeftConfig.nuspec -o Package -Prop Configuration=Release
