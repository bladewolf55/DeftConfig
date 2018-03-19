# ![](images/juggler-100x100.png) DeftConfig

>Web.config and app.config shouldn't be stored. They should be generated. DeftConfig helps with that.

[NuGet Package](https://www.nuget.org/packages/DeftConfig/)

[GitHub Repository](https://github.com/bladewolf55/DeftConfig)

## Briefly
The .Net configuration story has never been particularly good. It's improving significantly in .Net Core, but there's a lot of .Net Framework code that's built and going to be built.

The fundamental problems with .Net Framework config files are:

1. They require a physical file. A configuration can't be created in memory.
2. They don't work well with multiple environments, even using XML Transformations as intended.
3. They don't lend themselves to managing sensitive settings such as passwords, especially in a Continuous Integration environment.

DeftConfig makes it easier to use the "base config" approach to managing configuration files using XML Transformations. It follows a convention for how it finds the transformation file.

1. Look for a file in the format `web(or app).base.[Configuration].config`. Example: web.base.Release.config
2. Check the user profile's `%APPDATA%\DeftConfig\{Project GUID}` folder
3. Check the project folder
4. If not found, use web/app.base.Sample.config

![](images/deft-config-flow.png)

This approach allows for storing a production config, with sensitive settings, in a Windows Continuous Integration server's appropriate user profile rather than version control. The server folder can be restricted via permissions.

It also allows multiple developers to have their own, local Debug config; it also doesn't get stored in source control.

Finally, DeftConfig recommends maintaining a sample config that will help new developers understand what configurations need to be changed, and can provide a safe, default working experience.

The package is just two components:

1. An MSBuild targets file that applies the transformation and outputs the .config file.
2. A utility to help configure the project, including adding an `.ignore` file for either Git or TFVC.

>**Important**  
>I don't guarantee DeftConfig will work in your environment. You need to take the right steps to protect your source code, including backups, version control, and testing.


## Goals
DeftConfig achieves several goals. Web.config is used in the examples below, but DeftConfig applies equally to app.config.

1. Don't store web.config or web.Debug/Release.config in source control.
2. Don't store web.Debug.config in source control, allow independent developer configurations.
2. Use web.base.config file for XmlTransform source, and apply web.base.[Configuration].config transform file.
3. Make Windows-based Continuous Integration easier and more secure by searching for transform files in a user profile folder first, then in the project file.
4. Make it easier for new developers to use non-destructive settings.

## Cons
All isn't lemonade and corn dogs when using DeftConfig. There are a few challenges that you need to be aware of.

1. NuGet packages often modify the web.config file (EntityFramework, for example). It will be critical for developers who add packages to copy any changes from the local web.config into web.base.config.
2. If you change *only* one of the config files, then do a standard build (F6), web.config won't be updated. You'll need to do a Rebuild. (This is not an issue with TFS Build server, which always does a rebuild).

## Installation
1. Install the NuGet package. This adds the DeftConfig.targets file, and an executable named DeftConfigInitializer is copied to the project root folder.
2. Run DeftConfigInitializer, which will 
    1. Create a user profile folder for config files, if desired.
    2. Convert existing *.config files to use the *.base.config method. 
    3. Add a .gitignore or .tfignore file so that the standard *.config files are ignored by default.
3. Delete DeftConfigInitializer, if desired.

Typical project files before DeftConfig

![](images/2018-03-15%2019_49_52.png)

and after running DeftConfigInitializer

![](images/2018-03-15%2019_53_20.png)

The Debug and Release configs are still in the project folder, except they've been converted to "base" files.

![](images/2018-03-15%2019_55_47.png)


## Files
Any settings that were previously in web.config are now in web.base.config. 

Here are the recommended way of using the files, with an example. The original web.config had these appSettings. Notice that the production and dev credentials are being stored. Developers have been commenting/uncommenting.

```xml
</appSettings>
    <add key="CustomizationVersion" value="HGC1234" />
    <!--
    <add key="WebServiceUser" value="dev-user"/>
    <add key="WebServicePassword" value="DevPass"/>
    -->
    <add key="WebServiceUser" value="production-admin"/>
    <add key="WebServicePassword" value="Pr0ductionPassw0rd!"/>
</appSettings>
```

### web.base.config

The base config should retain values that are *not* transformed, and shouldn't have values for settings that will be tranformed. This is also a good place to document the settings. Copy those credential appSettings elsewhere, we'll need them later.

```xml
</appSettings>
    <!-- Don't change: It's specific to this client -->
    <add key="CustomizationVersion" value="HGC1234" />
    <!-- Credentials for the 3rd party web service -->
    <add key="WebServiceUser" value=""/>
    <add key="WebServicePassword" value="" />
</appSettings>
```

### web.base.Sample.config

The Sample config file *is* stored in source control. It acts as a default configuration. If a Configuration (Debug/Release) config file can't be found, the Sample will be used. These could be default values for a developer, or dummy values. 

```xml
</appSettings>
    <add key="WebServiceUser" value="user" xdt:Locater="Match(key)" xdt:Transform="Replace"/>
    <add key="WebServicePassword" value="pass" xdt:Locater="Match(key)" xdt:Transform="Replace"/>
</appSettings>
```

### web.base.Debug.config

The Debug file is for developer settings. Since it's not stored in source control, each developer can have her own settings.

```xml
</appSettings>
    <add key="WebServiceUser" value="dev-user" xdt:Locater="Match(key)" xdt:Transform="Replace"/>
    <add key="WebServicePassword" value="DevPass" xdt:Locater="Match(key)" xdt:Transform="Replace"/>
</appSettings>
```

### web.base.Release.config

Like the Debug file, the Release file is also not stored in source control. As shown below, if using a Windows-based continuous build system, it will be stored in the user profile of the automated build agent. This keeps sensitive credentials out of source control.

```xml
</appSettings>
    <add key="WebServiceUser" value="production-admin" xdt:Locater="Match(key)" xdt:Transform="Replace"/>
    <add key="WebServicePassword" value="Pr0ductionPassw0rd!" xdt:Locater="Match(key)" xdt:Transform="Replace"/>
</appSettings>
```

## Building a Configuration
As noted above, the safest way to build is to right-click the Solution and choose Rebuild. But in most cases you'll be able to just F6 or F5.

Which configuration is used is determined by the selected Configuration.

![](images/2018-03-15%2020_33_46.png)

Many development shops have a separate testing or staging environment. To configure that with DeftConfig, you'd simply add the configuration such as "Stage" using Configuration Manager, then manually create the web.base.Stage.config transform file (probably using copy/paste).

## Where and How Transform Files are Searched
The initializer utility will optionally create a folder in the user's profile. The folder is named using the project's GUID, since the project name could change. The path is:

`C:\Users\[user]\AppData\Roaming\DeftConfig\{PROJECT GUID}`

>Strictly speaking, the path is `%AppData%\DeftConfig\{PROJECT GUID}`

The DeftConfig build target checks for a matching config file in the following order. The diagram assumes the Release configuration is being built for MyAppProject.

![](images/deft-config-file-order.png)

## Why Isn't Web.config Removed From the Project?
Only files that are part of the project (that is, they're in the .csproj or .vsproj file) will be copied when using the project's Publish feature. 

```xml
  <ItemGroup>
    <Content Include="DeftConfigInitializer.exe" />
    <Content Include="Web.config" />
    <None Include="Web.base.config" />
    <None Include="Web.base.Sample.config" />
  </ItemGroup>
```

![](images/2018-03-15%2021_12_47.png)

However, just because there's an entry in the project file for web.config doesn't mean it *has* to be in the folder when you get the code from source control. It just has to be there when Publish is run.

This is why I recommend adding web.config to a Git or TFS ignore file in the project folder. Web.config will always be created as part of the build process, so it doesn't need to be stored in source control.

![](images/2018-03-15%2021_19_52.png)

DeftConfigInitializer will do this automatically if you choose.

**Using Git**

```
# Visual Studio 2015 Note: a pattern in the first line is ignored by Changes. Visual Studio Bug.
# DeftConfig - Ignore/allow certain config files when using *.base.[Build].config transform method

/[Ww]eb.config
/[Ww]eb.*.config
!/[Ww]eb.[Bb]ase.config
!/[Ww]eb.[Bb]ase.[Ss]ample.config

/[Aa]pp.config
/[Aa]pp.*.config
!/[Aa]pp.[Bb]ase.config
!/[Aa]pp.[Bb]ase.[Ss]ample.config
```

**Using TFVS**

```
#TFVS Ignore

\Web.config
\Web.*.config
!\Web.base.config
!\Web.base.Sample.config

\App.config
\App.*.config
!\App.base.config
!\App.base.Sample.config
```

