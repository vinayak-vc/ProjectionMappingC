// Minimal native consumer of the SDK. Grows with each milestone; currently it
// creates a Context, wires SDK logging to stdout, and verifies the version
// handshake across the binary boundary.
#include <PMSDK/PMSDK.h>

#include <cstdlib>
#include <iostream>

namespace {

void PrintLog(pmsdk::LogLevel level, std::string_view message, void* /*userData*/) {
    std::cout << "[" << pmsdk::ToString(level) << "] " << message << '\n';
}

} // namespace

int main() {
    const pmsdk::Version runtime = pmsdk::GetVersion();
    std::cout << "ProjectionMappingSDK " << pmsdk::GetVersionString() << '\n';

    if (runtime != pmsdk::kHeaderVersion) {
        std::cerr << "Header/binary version mismatch: headers are "
                  << pmsdk::kHeaderVersion.major << '.' << pmsdk::kHeaderVersion.minor << '.'
                  << pmsdk::kHeaderVersion.patch << '\n';
        return EXIT_FAILURE;
    }

    pmsdk::ContextDesc desc;
    desc.name = "native-example";
    desc.logLevel = pmsdk::LogLevel::Debug;
    desc.logCallback = &PrintLog;

    pmsdk::Context context{desc};
    context.GetConfig().SetInt("projector-count", 2);
    context.GetLogger().Info("example configured " +
                             std::to_string(context.GetConfig().GetInt("projector-count", 0)) +
                             " projectors");

    return EXIT_SUCCESS;
}
