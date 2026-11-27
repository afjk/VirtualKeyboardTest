#include "android/jni/mozc_shim.h"

#include <cstdlib>
#include <cstring>
#include <memory>
#include <string>
#include <utility>

#include "absl/base/no_destructor.h"
#include "absl/status/status.h"
#include "absl/status/statusor.h"
#include "absl/strings/str_cat.h"
#include "absl/synchronization/mutex.h"
#include "base/file_util.h"
#include "base/system_util.h"
#include "data_manager/data_manager.h"
#include "engine/engine.h"
#include "google/protobuf/util/json_util.h"
#include "protocol/commands.pb.h"
#include "session/session_handler.h"

namespace {

struct State {
  absl::Mutex mutex;
  std::unique_ptr<mozc::SessionHandler> handler ABSL_GUARDED_BY(mutex);
  std::string data_version ABSL_GUARDED_BY(mutex);
  std::string last_error ABSL_GUARDED_BY(mutex);
  std::string returned ABSL_GUARDED_BY(mutex);  // Backing store for const char*.
};

State& GetState() {
  static absl::NoDestructor<State> state;
  return *state;
}

char* CopyToHeap(const std::string& s) {
  char* out = static_cast<char*>(std::malloc(s.size() + 1));
  if (out != nullptr) std::memcpy(out, s.c_str(), s.size() + 1);
  return out;
}

}  // namespace

extern "C" {

int mozc_shim_initialize(const char* profile_dir, const char* data_file_path) {
  State& state = GetState();
  absl::MutexLock lock(&state.mutex);
  if (state.handler) return 1;
  if (profile_dir == nullptr || data_file_path == nullptr) {
    state.last_error = "profile_dir and data_file_path are required";
    return 0;
  }
  // Mozc stores learning data here and does not create the directory itself.
  if (absl::Status s = mozc::FileUtil::CreateDirectory(profile_dir);
      !s.ok() && !absl::IsAlreadyExists(s)) {
    state.last_error = absl::StrCat("cannot create profile directory ",
                                    profile_dir, ": ", s.ToString());
    return 0;
  }
  mozc::SystemUtil::SetUserProfileDirectory(profile_dir);

  // Unlike Mozc's JNI entry point, do not fall back to a dictionary-less
  // engine: a missing or unreadable data file must be visible to the caller.
  absl::StatusOr<std::unique_ptr<const mozc::DataManager>> data_manager =
      mozc::DataManager::CreateFromFile(data_file_path);
  if (!data_manager.ok()) {
    state.last_error = absl::StrCat("failed to load data file ", data_file_path,
                                    ": ", data_manager.status().ToString());
    return 0;
  }
  std::string version((*data_manager)->GetDataVersion());
  absl::StatusOr<std::unique_ptr<mozc::Engine>> engine =
      mozc::Engine::CreateEngine(*std::move(data_manager));
  if (!engine.ok()) {
    state.last_error =
        absl::StrCat("failed to create engine: ", engine.status().ToString());
    return 0;
  }
  state.handler = std::make_unique<mozc::SessionHandler>(*std::move(engine));
  state.data_version = std::move(version);
  state.last_error.clear();
  return 1;
}

char* mozc_shim_eval(const char* command_json) {
  State& state = GetState();
  absl::MutexLock lock(&state.mutex);
  if (!state.handler) {
    state.last_error = "not initialized";
    return nullptr;
  }
  if (command_json == nullptr) {
    state.last_error = "command_json is null";
    return nullptr;
  }
  mozc::commands::Command command;
  google::protobuf::util::JsonParseOptions parse_options;
  parse_options.ignore_unknown_fields = true;
  if (absl::Status s = google::protobuf::util::JsonStringToMessage(
          command_json, &command, parse_options);
      !s.ok()) {
    state.last_error = absl::StrCat("invalid command json: ", s.ToString());
    return nullptr;
  }
  if (!state.handler->EvalCommand(&command)) {
    state.last_error = "EvalCommand returned false";
    // The output may still carry an error code; return it to the caller.
  } else {
    state.last_error.clear();
  }
  std::string json;
  if (absl::Status s = google::protobuf::util::MessageToJsonString(command, &json);
      !s.ok()) {
    state.last_error = absl::StrCat("failed to encode output: ", s.ToString());
    return nullptr;
  }
  return CopyToHeap(json);
}

void mozc_shim_free(char* str) { std::free(str); }

const char* mozc_shim_data_version(void) {
  State& state = GetState();
  absl::MutexLock lock(&state.mutex);
  state.returned = state.data_version;
  return state.returned.c_str();
}

const char* mozc_shim_last_error(void) {
  State& state = GetState();
  absl::MutexLock lock(&state.mutex);
  state.returned = state.last_error;
  return state.returned.c_str();
}

}  // extern "C"
