# Pin rexglue and keep game glue in-tree

Status: accepted

We consume rexglue as a pinned git dependency (submodule or FetchContent) rather
than copying or hard-forking it; game-specific glue, patches, and generated code
live in this repository, and only generic fixes are proposed upstream. A hard
fork was rejected because it makes upstream improvements hard to absorb, and
requiring an unmodified SDK was rejected because Alive may need engine-specific
fixes.
