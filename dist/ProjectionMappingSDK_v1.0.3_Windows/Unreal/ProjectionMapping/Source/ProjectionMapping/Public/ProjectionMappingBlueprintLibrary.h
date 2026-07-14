#pragma once

#include "CoreMinimal.h"
#include "Kismet/BlueprintFunctionLibrary.h"

// Forward declaration of C_API handles to avoid including SDK headers globally in Unreal headers
typedef struct pmsdk_context_t* pmsdk_context_handle;
typedef struct pmsdk_warp_node_t* pmsdk_warp_node_handle;

#include "ProjectionMappingBlueprintLibrary.generated.h"

/**
 * UStruct representing a handle to the SDK context.
 */
USTRUCT(BlueprintType)
struct FProjectionMappingContextHandle
{
    GENERATED_BODY()

    pmsdk_context_handle Handle = nullptr;

    bool IsValid() const { return Handle != nullptr; }
};

/**
 * UStruct representing a handle to a Warp Node.
 */
USTRUCT(BlueprintType)
struct FProjectionMappingWarpNodeHandle
{
    GENERATED_BODY()

    pmsdk_warp_node_handle Handle = nullptr;

    bool IsValid() const { return Handle != nullptr; }
};

/**
 * Blueprint library exposing Projection Mapping SDK functionality.
 */
UCLASS()
class PROJECTIONMAPPING_API UProjectionMappingBlueprintLibrary : public UBlueprintFunctionLibrary
{
    GENERATED_BODY()

public:
    /** Initializes the SDK context. */
    UFUNCTION(BlueprintCallable, Category = "Projection Mapping|Context")
    static FProjectionMappingContextHandle CreateContext();

    /** Destroys the SDK context. */
    UFUNCTION(BlueprintCallable, Category = "Projection Mapping|Context")
    static void DestroyContext(UPARAM(ref) FProjectionMappingContextHandle& Context);

    /** Creates a root warp node. */
    UFUNCTION(BlueprintCallable, Category = "Projection Mapping|Warping")
    static FProjectionMappingWarpNodeHandle CreateWarpNode(const FString& Name);

    /** Destroys a warp node. */
    UFUNCTION(BlueprintCallable, Category = "Projection Mapping|Warping")
    static void DestroyWarpNode(UPARAM(ref) FProjectionMappingWarpNodeHandle& Node);
    
    /** Gets the last error message from the SDK. */
    UFUNCTION(BlueprintPure, Category = "Projection Mapping|Errors")
    static FString GetLastErrorMessage();
};
