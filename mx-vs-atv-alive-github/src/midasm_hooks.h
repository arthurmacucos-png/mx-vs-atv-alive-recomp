#pragma once

#include <cstdint>

#include <rex/ppc/context.h>

// Skip the save/loading-screen setup when its Bink resource lookup failed
// (field +148 is null). The guest function otherwise writes through the null
// pointer and crashes.
bool mx_alive_loading_screen_has_bink(PPCRegister& obj);

// The loading-screen object's +412 sub-object is only created by the function
// above. If it is null (because that setup was skipped), skip the dereference
// while preserving the manager fields the guest function sets first.
bool mx_alive_loading_screen_object_valid(PPCRegister& manager, PPCRegister& obj);

// Temporary diagnostic: dump the guest code words around the menu/race thunk
// cluster so missed entry points can be identified from the log.
void mx_alive_dump_thunk_region();
