// Minimal C API over Mozc's session handler.
//
// Commands and responses are mozc.commands.Command messages encoded as
// protobuf JSON (lowerCamelCase field names, enum names as strings,
// 64-bit integers as strings). This keeps callers free of protobuf runtimes
// and of the fixed Java class name required by Mozc's own JNI entry point.

#ifndef MOZC_SHIM_H_
#define MOZC_SHIM_H_

#if defined(_WIN32)
#define MOZC_SHIM_API __declspec(dllexport)
#else
#define MOZC_SHIM_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

// Loads the dictionary data file (mozc.data) and creates the engine.
// profile_dir receives learning data; it must be writable.
// Returns 1 on success, 0 on failure (see mozc_shim_last_error).
// Calling it again after success is a no-op that returns 1.
MOZC_SHIM_API int mozc_shim_initialize(const char* profile_dir,
                                       const char* data_file_path);

// Evaluates one command. Returns a newly allocated UTF-8 JSON string of the
// whole Command (input and output), or NULL on failure. Free the result with
// mozc_shim_free. Calls are serialized internally.
MOZC_SHIM_API char* mozc_shim_eval(const char* command_json);

MOZC_SHIM_API void mozc_shim_free(char* str);

// Data version of the loaded dictionary, or "" before initialization.
// The returned pointer stays valid until the next call into this library.
MOZC_SHIM_API const char* mozc_shim_data_version(void);

// Message for the last failure in this library ("" if none). Same lifetime
// rule as mozc_shim_data_version.
MOZC_SHIM_API const char* mozc_shim_last_error(void);

#ifdef __cplusplus
}  // extern "C"
#endif

#endif  // MOZC_SHIM_H_
