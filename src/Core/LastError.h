#pragma once

#include "PMSDK/Core/Status.h"

// Internal, not exported from the SDK binary. Thread-local last-error storage
// backing the future C API's PM_GetLastError(). Each thread observes only the
// errors set on that thread (errno-style semantics).
namespace pmsdk::detail {

/// Records `status` as the calling thread's last error.
void SetLastStatus(Status status) noexcept;

/// The calling thread's last recorded error; a success Status if none was set.
[[nodiscard]] const Status& LastStatus() noexcept;

/// Resets the calling thread's last error to success.
void ClearLastStatus() noexcept;

} // namespace pmsdk::detail
