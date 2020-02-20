///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target = Argument<string>("target", "Default");
var configuration = Argument<string>("configuration", "Release");

var artifactsDir = Directory("./artifacts");
var packages = "./artifacts/packages";
var solutionPath = "./Curiosity.SPSS.sln";
var framework = "netstandard2.0";

var isMasterBranch = StringComparer.OrdinalIgnoreCase.Equals("master",
    BuildSystem.TravisCI.Environment.Build.Branch);

var nugetApiKey = Argument<string>("nugetApiKey", null);
var nugetSource = "https://api.nuget.org/v3/index.json";


Task("Clean")
    .Does(() => 
    {            
        DotNetCoreClean(solutionPath);        
        DirectoryPath[] cleanDirectories = new DirectoryPath[] {
            artifactsDir
        };
    
        CleanDirectories(cleanDirectories);
    
        foreach(var path in cleanDirectories) { EnsureDirectoryExists(path); }
    
    });

Task("Build")
    .IsDependentOn("Clean")
    .Does(() => 
    {
        var settings = new DotNetCoreBuildSettings
          {
              Configuration = configuration
          };
          
        DotNetCoreBuild(
            solutionPath,
            settings);
    });

Task("UnitTests")
    .IsDependentOn("Build")
    .Does(() =>
    {        
        Information("UnitTests task...");
        var projects = GetFiles("./tests/UnitTests/**/*csproj");
        foreach(var project in projects)
        {
            Information(project);
            
            DotNetCoreTest(
                project.FullPath,
                new DotNetCoreTestSettings()
                {
                    Configuration = configuration,
                    NoBuild = false
                });
        }
    });
     
Task("IntegrationTests")
    .IsDependentOn("Build")
    .IsDependentOn("UnitTests")
    .Does(() =>
    {        
        Information("IntegrationTests task...");
		
        Information("Running docker...");
        StartProcess("docker-compose", "-f ./tests/IntegrationTests/env-compose.yml up -d");
		Information("Running docker completed");
		
        var projects = GetFiles("./tests/IntegrationTests/**/*csproj");
        foreach(var project in projects)
        {
            Information(project);
            
            DotNetCoreTest(
                project.FullPath,
                new DotNetCoreTestSettings()
                {
                    Configuration = configuration,
                    NoBuild = false
                });
        }
    })
    .Finally(() =>
    {  
        Information("Stopping docker...");
        StartProcess("docker-compose", "-f ./tests/IntegrationTests/env-compose.yml down");
        Information("Stopping docker completed");
    });  
    
Task("Pack")
    .Does(() =>
    {        
         Information("Packing to nupkg...");
         var settings = new DotNetCorePackSettings
          {
              Configuration = configuration,
              OutputDirectory = packages
          };
         
          DotNetCorePack(solutionPath, settings);
    });
    
Task("Publish")
    .IsDependentOn("Pack")
    .WithCriteria(isMasterBranch)
    .Does(() => {
    
        var pushSettings = new DotNetCoreNuGetPushSettings 
        {
            Source = nugetSource,
            ApiKey = nugetApiKey
        };
        Information(packages);
        var pkgs = GetFiles($"{packages}/*.nupkg");
        foreach(var pkg in pkgs) 
        {
            Information($"Publishing \"{pkg}\".");
            DotNetCoreNuGetPush(pkg.FullPath, pushSettings);
        }
    });

 
Task("Default")
    .IsDependentOn("Build")
    .IsDependentOn("UnitTests")
    .IsDependentOn("IntegrationTests");

RunTarget(target);
