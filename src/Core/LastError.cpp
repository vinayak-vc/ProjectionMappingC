#include "Core/LastError.h"

#include <utility>

namespace pmsdk::detail {

namespace {

Status& Storage() noexcept {
    // Function-local thread_local avoids static initialization order issues
    // and keeps the storage private to this translation unit.
    thread_local Status lastStatus;
    return lastStatus;
}

} // namespace

void SetLastStatus(Status status) noexcept {
    Storage() = std::move(status);
}

const Status& LastStatus() noexcept {
    return Storage();
}

void ClearLastStatus() noexcept {
    Storage() = Status::Ok();
}

} // namespace pmsdk::detail
