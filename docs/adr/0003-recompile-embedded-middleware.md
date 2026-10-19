# Recompile embedded middleware rather than replacing it

Status: accepted

FMOD, Bink, and any other statically linked middleware are recompiled as part of
the guest. Replacing them with host libraries was rejected initially because
their APIs are reached through internal direct calls that would each need
interception, and because behavior parity matters more than using modern
libraries. A component may still be replaced later if its platform-specific path
blocks progress, recorded as its own decision.
