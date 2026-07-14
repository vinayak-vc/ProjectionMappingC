// MSVC workaround for OpenCV static linkage
// Provides symbols that are missing from some versions of the MSVC runtime.
// Compiled as C to avoid conflicting with <string_view> C++ headers.

extern "C" {

unsigned long long __std_find_first_not_of_trivial_pos_1(const char* const a, unsigned long long b, const char* const c, unsigned long long d) {
    (void)a; (void)b; (void)c; (void)d;
    return (unsigned long long)-1;
}

unsigned long long __std_find_last_not_of_trivial_pos_1(const char* const a, unsigned long long b, const char* const c, unsigned long long d) {
    (void)a; (void)b; (void)c; (void)d;
    return (unsigned long long)-1;
}

const unsigned long long* __std_min_element_8u(const unsigned long long* a, const unsigned long long* b) {
    (void)b;
    return a; // Dummy implementation
}

}
