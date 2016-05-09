//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

// Define directories.
var buildDir = Directory("./build") + Directory(configuration);
var packDir = Directory("./nupkg");

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
    {
        CleanDirectory(buildDir);
        CleanDirectory(packDir);
    });

Task("Restore-NuGet-Packages")
    .IsDependentOn("Clean")
    .Does(() =>
    {
        NuGetRestore("./OkHttpClient.sln");
    });

Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(() =>
    {
        var guid = Guid.NewGuid();
        

        Information($"##vso[task.setprogress value=75;]Upload Log");

        Information($"##vso[task.logdetail id={guid};name=OkHttpClient;type=build;order=1]Build");

        if(IsRunningOnWindows())
        {
            // Use MSBuild
            MSBuild("./OkHttpClient.sln", settings =>
                settings.SetConfiguration(configuration));
        }
        else
        {
            // Use XBuild
            XBuild("./OkHttpClient.sln", settings =>
                settings.SetConfiguration(configuration));
        }
    });
    
Task("Create-NuGet-Package")
    .IsDependentOn("Build")
    .Does(() =>
    {
        var settings = new NuGetPackSettings
        { 
            OutputDirectory = packDir
        };
        
        CreateDirectory(packDir);
        NuGetPack("OkHttpClient.nuspec", settings); 
    });

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default").IsDependentOn("Create-NuGet-Package");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);