using UnrealBuildTool;
using System.IO;

public class ProjectionMapping : ModuleRules
{
	public ProjectionMapping(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = ModuleRules.PCHUsageMode.UseExplicitOrSharedPCHs;
		
		PublicIncludePaths.AddRange(
			new string[] {
				// ... add public include paths required here ...
			}
			);
				
		
		PrivateIncludePaths.AddRange(
			new string[] {
				// ... add other private include paths required here ...
			}
			);
			
		
		PublicDependencyModuleNames.AddRange(
			new string[]
			{
				"Core",
				"Projects"
				// ... add other public dependencies that you statically link with here ...
			}
			);
			
		
		PrivateDependencyModuleNames.AddRange(
			new string[]
			{
				"CoreUObject",
				"Engine",
				"Slate",
				"SlateCore",
				// ... add private dependencies that you statically link with here ...	
			}
			);
		
		
		DynamicallyLoadedModuleNames.AddRange(
			new string[]
			{
				// ... add any modules that your module loads dynamically here ...
			}
			);

		// Link to ProjectionMappingSDK
		string PluginPath = Utils.MakePathRelativeTo(ModuleDirectory, Target.RelativeEnginePath);
		string ThirdPartyPath = Path.Combine(PluginPath, "..", "ThirdParty", "ProjectionMappingSDK");

		PublicIncludePaths.Add(Path.Combine(ThirdPartyPath, "include"));

		if (Target.Platform == UnrealTargetPlatform.Win64)
		{
			string LibraryPath = Path.Combine(ThirdPartyPath, "Win64", "ProjectionMappingSDK.lib");
			PublicAdditionalLibraries.Add(LibraryPath);
			
			string DLLPath = Path.Combine(ThirdPartyPath, "Win64", "ProjectionMappingSDK.dll");
			RuntimeDependencies.Add(DLLPath);
			PublicDelayLoadDLLs.Add("ProjectionMappingSDK.dll");
		}
	}
}
