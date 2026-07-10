#include "PMSDK/Core/Context.h"

namespace pmsdk {

struct Context::Impl {
    Logger logger;
    Config config;

    explicit Impl(const ContextDesc& desc) : logger(desc.name) {
        logger.SetLevel(desc.logLevel);
        if (desc.logCallback != nullptr) {
            logger.SetCallback(desc.logCallback, desc.logUserData);
        }
    }
};

Context::Context(const ContextDesc& desc) : impl_(std::make_unique<Impl>(desc)) {
    impl_->logger.Info("PMSDK context created");
}

Context::~Context() {
    impl_->logger.Info("PMSDK context destroyed");
}

Logger& Context::GetLogger() noexcept {
    return impl_->logger;
}

const Logger& Context::GetLogger() const noexcept {
    return impl_->logger;
}

Config& Context::GetConfig() noexcept {
    return impl_->config;
}

const Config& Context::GetConfig() const noexcept {
    return impl_->config;
}

const std::string& Context::GetName() const noexcept {
    return impl_->logger.GetName();
}

} // namespace pmsdk
