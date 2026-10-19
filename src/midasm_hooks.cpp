#include "midasm_hooks.h"

#include <cstdint>

#include <rex/logging.h>
#include <rex/memory/utils.h>
#include <rex/runtime.h>

namespace {

uint8_t* GuestPointer(uint32_t address) {
  auto* runtime = rex::Runtime::instance();
  if (!runtime || !runtime->memory()) {
    return nullptr;
  }
  return runtime->memory()->TranslateVirtual(address);
}

uint32_t LoadGuestU32(uint32_t address) {
  const uint8_t* ptr = GuestPointer(address);
  return ptr ? rex::memory::load_and_swap<uint32_t>(ptr) : 0;
}

}  // namespace

bool mx_alive_loading_screen_has_bink(PPCRegister& obj_reg) {
  const uint32_t obj = obj_reg.u32;
  if (obj == 0) {
    return false;
  }
  const uint32_t value = LoadGuestU32(obj + 148);
  REXLOG_DEBUG("mx_alive: loading-screen bink resource = 0x{:08X}{}", value,
               value == 0 ? " (skipping loading-screen init)" : "");
  return value != 0;
}

void mx_alive_dump_thunk_region() {
  REXLOG_INFO("mx_alive: dumping guest code 0x82327B00..0x82328000");
  for (uint32_t address = 0x82327B00; address < 0x82328000; address += 4) {
    REXLOG_INFO("  {:08X}: {:08X}", address, LoadGuestU32(address));
  }
}

bool mx_alive_loading_screen_object_valid(PPCRegister& manager, PPCRegister& obj_reg) {
  const uint32_t obj = obj_reg.u32;
  if (obj == 0) {
    return true;  // The guest function handles a null object itself.
  }
  if (LoadGuestU32(obj + 412) != 0) {
    return true;
  }

  // Leave manager+3600 unset so the per-frame loading-screen update sees "no
  // loading screen" and skips its own body instead of dereferencing this same
  // null +412 sub-object.
  REXLOG_DEBUG(
      "mx_alive: skipping null loading-screen sub-object deref "
      "(manager=0x{:08X}, obj=0x{:08X})",
      manager.u32, obj);
  return false;
}
