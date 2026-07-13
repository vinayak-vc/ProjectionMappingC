#include "ProjectionMappingBlueprintLibrary.h"

// Include the C API from the SDK
#include "PMSDK/C_API.h"

FProjectionMappingContextHandle UProjectionMappingBlueprintLibrary::CreateContext()
{
    FProjectionMappingContextHandle Result;
    pmsdk_status status = pmsdk_context_create(&Result.Handle);
    if (status != PMSDK_SUCCESS)
    {
        UE_LOG(LogTemp, Error, TEXT("ProjectionMappingSDK: Failed to create context. Error code: %d"), (int)status);
    }
    return Result;
}

void UProjectionMappingBlueprintLibrary::DestroyContext(FProjectionMappingContextHandle& Context)
{
    if (Context.IsValid())
    {
        pmsdk_context_destroy(Context.Handle);
        Context.Handle = nullptr;
    }
}

FProjectionMappingWarpNodeHandle UProjectionMappingBlueprintLibrary::CreateWarpNode(const FString& Name)
{
    FProjectionMappingWarpNodeHandle Result;
    pmsdk_status status = pmsdk_warp_node_create(TCHAR_TO_UTF8(*Name), &Result.Handle);
    if (status != PMSDK_SUCCESS)
    {
        UE_LOG(LogTemp, Error, TEXT("ProjectionMappingSDK: Failed to create warp node. Error code: %d"), (int)status);
    }
    return Result;
}

void UProjectionMappingBlueprintLibrary::DestroyWarpNode(FProjectionMappingWarpNodeHandle& Node)
{
    if (Node.IsValid())
    {
        pmsdk_warp_node_destroy(Node.Handle);
        Node.Handle = nullptr;
    }
}

FString UProjectionMappingBlueprintLibrary::GetLastErrorMessage()
{
    const char* errorMsg = nullptr;
    pmsdk_get_last_error_message(&errorMsg);
    if (errorMsg)
    {
        return FString(UTF8_TO_TCHAR(errorMsg));
    }
    return FString();
}
