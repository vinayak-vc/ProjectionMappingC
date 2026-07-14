#include "Modules/ModuleManager.h"

class FProjectionMappingModule : public IModuleInterface
{
public:
	virtual void StartupModule() override
	{
		// This code will execute after your module is loaded into memory.
	}

	virtual void ShutdownModule() override
	{
		// This function may be called during shutdown to clean up your module.
	}
};

IMPLEMENT_MODULE(FProjectionMappingModule, ProjectionMapping)
