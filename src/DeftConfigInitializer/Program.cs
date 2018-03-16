using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml.Linq;

namespace DeftConfigInitializer
{
    class Program
    {
        const string BaseName = "base";
        const string UserProfileBaseFolder = @"%APPDATA%\DeftConfig";
        const ConsoleColor ErrorColor = ConsoleColor.Red;
        const ConsoleColor HeadingColor = ConsoleColor.White; // ConsoleColor.DarkBlue;
        const ConsoleColor PromptColor = ConsoleColor.DarkGreen;

        static string ProjectFile;
        static string ProjectGuid;
        static string UserProfileFolder;
        static string ConfigType;

        static void Main(string[] args)
        {
#if DEBUG
            //Set this for testing
            Environment.CurrentDirectory = @"C:\Users\charl\Google Drive\Documents\Clients\WebApplication1\WebApplication1";
#endif 

            string input = "";

            string openingMsg = @"
Welcome to DeftConfig Initializer!

This utility attempts to set up the existing project using the 'base.config' method. You'll
be prompted whether to create a folder in your user profile to store your version
of the debug and/or release (or other!) configurations.

You can exit the utility at any time using CTRL-C. If you're being prompted for input, you can
also enter 'exit'.
";
            Show(openingMsg);

            ConfigType = GetConfigType();

            var projectFiles = GetProjectFiles();
            if (projectFiles.Count() > 1)
            {
                SelectProjectFile(projectFiles);
            }
            if (projectFiles.Count() == 0)
            {
                Show("There are no project files in this folder.", ErrorColor);
                Exit();
            }
            Show();
            ProjectFile = projectFiles[0];
            ProjectGuid = GetProjectGuid();
            if (ProjectGuid.Length == 0)
            {
                Show("Couldn't find the ProjectGuid in the project file", ErrorColor);
                Exit();
            }
            UserProfileFolder = Path.Combine(Environment.ExpandEnvironmentVariables(UserProfileBaseFolder), ProjectGuid);
            bool useUserProfile = false;
            if (!Directory.Exists(UserProfileFolder))
            {
                Show("Your user profile folder would be at");
                Show(UserProfileFolder);
                Show();
                input = Read("Would you like to create it? Answer (y)es, otherwise it's not created:", PromptColor).ToLower();
                useUserProfile = (input == "y" | input == "yes");
                if (useUserProfile)
                {
                    if (!Directory.Exists(UserProfileFolder))
                    {
                        try
                        {
                            Directory.CreateDirectory(UserProfileFolder);
                            Show("Folder created");
                        }
                        catch (Exception ex)
                        {
                            Show($"Folder not created. {ex.Message}", ErrorColor);
                            useUserProfile = false;
                        }
                    }
                }
            }
            else
            {
                useUserProfile = true;
                Show("Your user profile folder already exists! The folder is:");
                Show(UserProfileFolder);
                Show();
                Read("Press a key to continue the initialization");
            }


            string configFile = Path.Combine(Environment.CurrentDirectory, $"{ConfigType}.config");
            string configBase = Path.Combine(Environment.CurrentDirectory, $"{ConfigType}.{BaseName}.config");
            string configSample = Path.Combine(Environment.CurrentDirectory, $"{ConfigType}.{BaseName}.Sample.config");
            string[] otherConfigFiles = Directory.GetFiles(Environment.CurrentDirectory, $"{ConfigType}.*.config");
            otherConfigFiles = otherConfigFiles.Where(a => !a.Contains(BaseName)).ToArray();

            string configFileName = Path.GetFileName(configFile);
            string configBaseName = Path.GetFileName(configBase);
            string configSampleName = Path.GetFileName(configSample);
            string otherConfigFilesMsg = otherConfigFiles.Count() == 0 ? "no files found" : String.Join("\r\n    ", otherConfigFiles.Select(a => Path.GetFileName(a)));
            string convertFilesMsg = $@"
The next step will attempt to migrate your files to use the .base.config method.

1. Copy {configFileName} to {configBaseName}.
2. Copy {ConfigType}.Debug.config, if it exists, to {configSampleName}. Otherwise, write a default sample.
3. Rename other configs into {ConfigType}.{BaseName}.[BuildDefinition].config files. Those other files are:
    {otherConfigFilesMsg}
4. If you created a profile folder, you can optionally copy the build-specific config files into there.
5. Modify your project file to:
    1. Remove the build definition file entries
    2. Remove <SubType> elements from the {ConfigType}.config entry.
    3. Add entries for {configBaseName} and {configSampleName} with BuildAction = None.

At the end, your Solution Explorer will show:

{configBaseName}
{configSampleName}
{configFileName}

Your folder may have more config files that are now not part of the project. 
If you want to re-include them, show all files in Solution Explorer, right-click
the file and choose 'Include in Project.'
";
            Show(convertFilesMsg);
            Show();
            input = Read("Do you want to proceed? Answer (y)es, otherwise nothing will change.", PromptColor);
            bool modifyFiles = (input == "y" | input == "yes");
            bool copyToProfile = false;
            if (modifyFiles & useUserProfile)
            {
                input = Read("Do you want to copy files to your user profile? Answer (y)es, otherwise files are copied locally.", PromptColor);
                copyToProfile = (input == "y" | input == "yes");
            }
            Show();
            Show("Results:");
            Show();
            if (modifyFiles)
            {
                //list of config files is used in both steps
                CopyConfigFiles(copyToProfile, configFile, configBase, configSample, otherConfigFiles);
                Show();
                ModifyProjectFile(configFile, configBase, configSample, otherConfigFiles);
            }

            Show();

            bool hasVcs = UsingGit() | UsingTfvc();
            bool hasIgnore = HasGitIgnore() | HasTfIgnore();
            VCS vcs = UsingGit() ? VCS.Git : VCS.TFVC;
            string vcsName = vcs.ToString();
            string ignoreFile = UsingGit() ? ".gitignore" : (UsingTfvc() ? ".tfignore" : "");

            if (hasVcs)
            {
                if (hasIgnore)
                {
                    string hasIgnoreMsg = $@"
It looks like you're using {vcsName}, and you already have a {ignoreFile} file. 
Do you want to append the DeftConfig ignore patterns? Answer (y)es to append, or <enter> to leave alone.";
                    input = Read(hasIgnoreMsg, PromptColor);
                    bool appendIgnore = (input == "y" | input == "yes");
                    if (appendIgnore) { AppendIgnore(vcs); }
                }
                else
                {
                    string hasIgnoreMsg = $@"
It looks like you're using {vcsName}. Do you want to create a {ignoreFile} file with the DeftConfig ignore patterns?
Answer (y)es to create, or <enter> to do nothing.";
                    input = Read(hasIgnoreMsg, PromptColor);
                    bool appendIgnore = (input == "y" | input == "yes");
                    if (appendIgnore) { AppendIgnore(vcs); }
                }
            }

            Show();

            string finalMsg = $@"
The recommended final steps for you are:
1. Delete the project file backup.
2. Commit changes to your version control (if any)
3. Test the changes.
";

            Show(finalMsg);
            Exit();
        }


        static void CopyConfigFiles(bool copyToProfile, string configFile, string configBase, string configSample, string[] otherConfigFiles)
        {
            Show("Copying config files", HeadingColor);
            //base file            
            Copy(configFile, configBase);
            //sample file, try using the Debug version first.
            string debugFile = otherConfigFiles.Where(a => a.ToLower().Contains(".debug.")).FirstOrDefault();
            if (debugFile != null)
            {
                Copy(debugFile, configSample);
            }
            else
            {
                //create the sample file
                Show("Creating a sample file");
                File.WriteAllText(configSample, SampleTransform());
            }

            foreach(string file in otherConfigFiles)
            {
                //normalize prefix                
                string prefix = Path.GetFileName(file).Split('.')[0];
                string newFileName = Path.GetFileName(file).Replace(prefix, $"{ConfigType}.{BaseName}");
                string destination = "";
                if (copyToProfile) { destination = Path.Combine(UserProfileFolder, newFileName); }
                else { destination = Path.Combine(Environment.CurrentDirectory, newFileName); }
                Move(file, destination);                
            }
        }

        static void ModifyProjectFile(string configFile, string configBase, string configSample, string[] otherConfigFiles)
        {
            Show("Modifying project file", HeadingColor);
            Show($"Backing up project file to {Path.GetFileName(ProjectFile)}.backup ");
            Copy(ProjectFile, ProjectFile + ".backup");
            XDocument newFile = XDocument.Load(ProjectFile);
            var ns = newFile.Root.Name.Namespace;
            IEnumerable<XAttribute> matches;
            XAttribute match;
            XElement fileElement = null;
            XElement itemGroup = null;
            string fileName = "";
            //looking for element ItemGroup, any child element with attribute Include="[filename]"
            foreach (string file in otherConfigFiles)
            {
                fileName = Path.GetFileName(file);
                Show($"Searching for element with Include='{fileName}'");
                matches = GetMatchingIncludeFiles(newFile, file);
                if (matches.Count() != 1)
                {
                    Show($"{fileName} has {matches.Count()} entries, which is invalid. Skipping.", ErrorColor);
                    continue;
                }
                match = matches.Single();
                fileElement = match.Parent;
                fileElement.Remove();
                Show($"{fileName} removed");
            }
            //remove child elements from [app/web].config
            //Get the parent element at the same time.
            fileName = Path.GetFileName(configFile);
            Show($"Searching for {fileName}");
            matches = GetMatchingIncludeFiles(newFile, configFile);
            if (matches.Count() != 1)
            {
                Show($"{fileName} has {matches.Count()} entries, which is invalid. Skipping.", ErrorColor);
            }
            else
            {
                match = matches.Single();
                fileElement = match.Parent;
                itemGroup = fileElement.Parent;
                fileElement.RemoveNodes();
                Show($"Removed child elements from {fileName}");
            }
            //add base and sample
            Show($"Adding base and sample file elements", HeadingColor);
            AddIncludeFile(newFile, itemGroup, configBase);
            AddIncludeFile(newFile, itemGroup, configSample);

            //write file
            newFile.Save(ProjectFile);
        }

        static IEnumerable<XAttribute> GetMatchingIncludeFiles(XDocument doc, string file)
        {
            file = Path.GetFileName(file);
            var ns = doc.Root.Name.Namespace;
            var x1 = doc.Descendants().Attributes("Include").ToList();
            var x2 = x1.Where(a => a.Value == file).ToList();
            return x2;
        }

        /// <summary>
        /// Adds if doesn't exist
        /// </summary>
        /// <param name="file"></param>
        static void AddIncludeFile(XDocument doc, XElement itemGroup, string file)
        {
            var ns = doc.Root.Name.Namespace;
            string fileName = Path.GetFileName(file);
            Show($"Searching for {fileName}");
            var matches = GetMatchingIncludeFiles(doc, file);
            if (matches.Count() > 0)
            {
                Show($"{fileName} has {matches.Count()} entries, which is invalid. Skipping.", ErrorColor);
            }
            else
            {

                file = Path.GetFileName(file);
                //Weird, but including the namespace is what causes it to be removed
                //because the parent element already has it.
                //https://stackoverflow.com/a/5956063/1628707
                XElement include = new XElement(ns + "None");
                include.AddAnnotation("Include");
                include.SetAttributeValue("Include", file);
                itemGroup.Add(include);
                Show($"Added {fileName}");
            }
        }

        static string SampleTransform()
        {
            return @"<?xml version=""1.0""?>
<!-- For more information on using Web.config transformation visit http://go.microsoft.com/fwlink/?LinkId=301874 -->

<configuration xmlns:xdt=""http://schemas.microsoft.com/XML-Document-Transform"">
  <connectionStrings>
  </connectionStrings>

  <appSettings>
  </appSettings>
</configuration>
";
        }

        static void Exit()
        {
            Read("Press any key to exit.");
            Environment.Exit(0);
        }

        static void SelectProjectFile(string[] projectFiles)
        {
            Show("There are multiple project files. Please choose one.");
            int choices = 0;
            foreach (string file in projectFiles)
            {
                choices++;
                string fileName = Path.GetFileName(file);
                Show($"{choices} {fileName}");
            }
            Show();
            string input = Read("Enter the number:");
            int choice = 0;
            try
            {
                if (!int.TryParse(input, out choice))
                {
                    throw new Exception("Invalid choice. Must be a number.");
                }
                if (choice < 1 || choice > choices)
                {
                    throw new Exception($"Choice must be between 1 and {choices}");
                }
            }
            catch (Exception ex)
            {
                Show(ex.Message, ErrorColor);
                Show();
                SelectProjectFile(projectFiles);
            }
            //will throw error if out of range
            if (String.IsNullOrEmpty(ProjectFile))
            {
                ProjectFile = projectFiles[choice - 1];
            }
        }

        static string GetConfigType()
        {
            var files = Directory.GetFiles(Environment.CurrentDirectory, "*.config")
                .Select(a => Path.GetFileNameWithoutExtension(a));
            string app = files.Where(a => a.Equals("app", StringComparison.InvariantCultureIgnoreCase)).SingleOrDefault() ?? "";
            string web = files.Where(a => a.Equals("web", StringComparison.InvariantCultureIgnoreCase)).SingleOrDefault() ?? "";
            if (app.Length > 0 & web.Length > 0)
            {
                Show("Something's wrong. There are both app.config and web.config files.", ErrorColor);
                Exit();
            }
            if (app.Length == 0 & web.Length == 0)
            {
                Show("Something's wrong. There's no app.config or web.config.", ErrorColor);
                Exit();
            }
            return app + web;
        }

        static string GetProjectGuid()
        {
            XDocument doc = XDocument.Parse(File.ReadAllText(ProjectFile));
            var ns = doc.Root.Name.Namespace;
            return doc.Descendants(ns + "ProjectGuid").First().Value;
        }

        static string[] GetProjectFiles()
        {
            return Directory.GetFiles(Environment.CurrentDirectory, "*.??proj");
        }

        static bool HasGitIgnore()
        {
            return File.Exists(Path.Combine(Environment.CurrentDirectory, ".gitignore"));
        }

        static bool UsingGit()
        {
            return HasFolder(".git");
        }

        static bool HasTfIgnore()
        {
            return File.Exists(Path.Combine(Environment.CurrentDirectory, ".tfignore"));
        }

        static bool UsingTfvc()
        {
            return HasFolder("$tf");
        }

        static bool HasFolder(string folderName)
        {
            //look in current folder, and up two folders
            string testDir = Environment.CurrentDirectory;
            int maxLevel = 2;
            int level = 0;
            while (level <= maxLevel)
            {
                if (Directory.Exists(Path.Combine(testDir, folderName)))
                {
                    return true;
                }
                testDir = Directory.GetParent(testDir).FullName;
                level++;
            }
            return false;
        }

        static void AppendIgnore(VCS vcs)
        {
            string file = "";
            string patterns = "";
            switch (vcs)
            {
                case VCS.Git:
                    file = Path.Combine(Environment.CurrentDirectory, ".gitignore");
                    patterns = GitIgnore;
                    break;
                case VCS.TFVC:
                    file = Path.Combine(Environment.CurrentDirectory, ".tfignore");
                    patterns = TfIgnore;
                    break;
            }

            File.AppendAllText(file, patterns);
        }

        public enum VCS { Git, TFVC}

        const string GitIgnore = @"
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
";

        const string TfIgnore = @"
#TFVS Ignore

\Web.config
\Web.*.config
!\Web.base.config
!\Web.base.Sample.config

\App.config
\App.*.config
!\App.base.config
!\App.base.Sample.config";


        static void Show(string value = null, ConsoleColor color = ConsoleColor.White)
        {
            Console.ForegroundColor = color;
            if (value == null) Console.WriteLine();
            else Console.WriteLine(value);
            Console.ResetColor();
        }

        /// <summary>
        /// Reads the input. If user types "exit", exits the program
        /// </summary>
        /// <param name="inlinePrompt"></param>
        /// <returns></returns>
        static string Read(string inlinePrompt, ConsoleColor color = ConsoleColor.White)
        {
            Console.ForegroundColor = color;
            Console.Write(inlinePrompt + " ");
            Console.ResetColor();
            string input = Console.ReadLine();
            if (input.ToLower() == "exit") { Environment.Exit(0); return null; }
            else return input;
        }

        /// <summary>
        /// Does not coy if file exists and overwrite = false
        /// </summary>
        /// <param name="source"></param>
        /// <param name="destination"></param>
        /// <param name="overwrite"></param>
        static void Copy(string source, string destination, bool overwrite = false)
        {
            if (!overwrite)
            {
                if (File.Exists(destination))
                {
                    Show($"File {Path.GetFileName(destination)} already exists. Not copying.", ErrorColor);
                    return;
                }
            }
            File.Copy(source, destination, overwrite);
        }

        /// <summary>
        /// Does not move if file exists and overwrite = false
        /// </summary>
        /// <param name="source"></param>
        /// <param name="destination"></param>
        /// <param name="overwrite"></param>
        static void Move(string source, string destination, bool overwrite = false)
        {
            if (!overwrite)
            {
                if (File.Exists(destination))
                {
                    Show($"File {Path.GetFileName(destination)} already exists. Not moving.", ErrorColor);
                    return;
                }
            }
            if (File.Exists(destination))
            {
                File.Delete(destination);
            }
            File.Move(source, destination);
        }
    }
}
