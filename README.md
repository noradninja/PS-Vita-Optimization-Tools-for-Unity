# PS-Vita-Optimization-Tools-for-Unity

A collection of tools to optimize various systems when building for the PlayStation Vita in Unity.

---

# CHANGELOG

---

## 31 Jan 2026

### Key Technical Improvements

#### Centralized LOD Threshold System (Consistency & Control)

**Global Distance Thresholds:** Removed per-object LOD distance arrays and replaced them with centralized thresholds exposed directly on the `LODManager` Inspector.

**Single Source of Truth:** All LOD transitions are now controlled globally from the manager, ensuring consistent behavior across the entire scene.

**Editor Workflow Upgrade:** Added an Inspector button on the `LODManager` that applies updated thresholds to all registered objects instantly. No code edits required.

---

#### Object-Size-Aware LOD Switching (Visual Stability)

**Bounds-Aware Transitioning:** LOD switching now accounts for object size by calculating the largest horizontal bound (X or Z axis) per object and adding half its width to the threshold distance.

**Prevents Early Popping:** Large objects no longer switch LOD too early, eliminating noticeable visual popping and improving scene cohesion.

**Square Distance Preserved:** All size adjustments are applied while maintaining squared distance math for maximum efficiency.

---

#### Zero Runtime Material Mutation (Batching Safe)

**Shared Material Architecture:** Replaced per-object runtime material instancing with a shared, cached material system.

**Batching Preserved:** The system no longer creates new material instances during LOD switching, preventing dynamic batching breakage.

**Prebuilt Material Variants:**

Each object now caches:

* `originalFullMat` (Normal maps ON, shadows ON)
* `originalReducedMat` (Normal maps OFF, shadows OFF)
* `vertexOnlyMat` (Shared vertex-lit material, Ambient OFF)

These variants are created once and reused, eliminating runtime keyword mutation on shared materials.

---

#### Vertex-Only Mode Improvements (GPU Optimization)

**Ambient Disabled in VertexOnly:** The `_AmbientOn` keyword is explicitly disabled in VertexOnly mode to reduce fragment cost.

**Dedicated Lightweight Path:** Vertex-only LOD is now a clean, deterministic rendering state instead of a modified copy of the original material.

---

#### NativeArray Safety & Job System Stability

**Memory Leak Fix:** Resolved NativeArray disposal edge case that could occur during resize or destruction.

**Batch Index Safety:** Fixed IndexOutOfRange exception caused by mismatched batch indexing during job completion.

**Safe Resize Strategy:** NativeArrays are now resized only after pending jobs complete, preventing undefined behavior.

**Stable Cycle Completion:** Batch index and cycle tracking logic updated to prevent overrun during enumerator list changes.

---

#### Improved Runtime Scalability

**Dynamic Register/Unregister Safety:** Objects can now be added or removed during runtime without corrupting job buffers.

**Centralized State Management:** LOD state changes occur only when the state actually differs, preventing redundant material assignments.

**Cleaner Render-State Transitions:** Shadow casting modes are now deterministically applied per LOD state without modifying shared material internals.

---

### Architectural Overview (LOD System)

The LOD system is built around a centralized manager-driven architecture optimized specifically for PS Vita constraints.

**Core Design Goals:**

* Minimize main-thread CPU load
* Preserve batching
* Avoid runtime material mutations
* Eliminate unnecessary memory churn

#### LODManager

* Maintains a registered list of all `Shader_LOD_Enumerator` instances.
* Processes distance calculations in batches using Unity’s Job System (`IJobParallelFor`).
* Uses squared distance checks to avoid square root operations.
* Applies global thresholds with size-aware offsets.
* Manages NativeArray allocation and disposal safely.

#### Shader_LOD_Enumerator

* Registers itself with the manager on Start.
* Computes its horizontal bounds once and caches it.
* Stores prebuilt material variants instead of modifying shared materials at runtime.
* Switches rendering state only when LOD state changes.

#### Material Strategy

Instead of mutating keywords on shared materials at runtime (which breaks batching), the system:

1. Caches original material variants at startup.
2. Prepares a shared vertex-only material per object.
3. Switches between prepared materials without altering shader keywords dynamically.

This preserves batching and prevents per-frame material state thrashing.

---

### Impact

**Memory:**

* Eliminated unnecessary material instancing.
* Fixed NativeArray leak.
* Reduced long-session RAM instability.

**CPU:**

* Maintained multithreaded sqrMagnitude distance checks.
* Prevented main-thread spikes from array mismanagement.
* Removed redundant material keyword toggling.

**GPU:**

* Preserved batching across large object counts.
* Reduced fragment cost in mid and far LOD states.
* Disabled ambient and shadows where not required.

**Visual Stability:**

* LOD transitions now scale with object size.
* Eliminated large-object early pop-in.
* More consistent distance-based degradation.

**Usability:**

* Global thresholds adjustable from one location.
* Inspector-driven workflow.
* No per-object tuning required unless explicitly desired.

---

## 27 Dec 2025

### Key Technical Improvements

#### Centralized Memory Management (Scalability)

**Global Shader Reference:** Moved shader lookups from individual objects to a single `refShader` variable in the `LODManager`.

**RAM Optimization:** Eliminated redundant shader references across all instances. This prevents memory bloat and ensures the system remains scalable as the number of objects increases.

**Drag-and-Drop Workflow:** Users can now assign the replacement shader directly in the Inspector, making the tool project-agnostic.

---

#### Multi-Core CPU Strategy (Job System)

**Job System Integration:** Distance calculations are offloaded from Core 0 to secondary worker cores using `IJobParallelFor`.

**Efficient Math:** Replaced `Vector3.Distance` with `sqrMagnitude` to avoid expensive square root operations, resulting in ~10x faster distance checks.

---

#### Native Vita Performance & Stability

**Memory Safety:** Retained the automated `Resources.UnloadUnusedAssets` cycle to keep the RAM footprint clean during extended play sessions.

**PVRTC & Texture Integrity:** Every object still instantiates its own Material from the global shader. This ensures that unique textures (essential for PVRTC/GXT workflows) are preserved without conflict.

---

#### GPU Optimization

**Automated Shadow Switching:** Shadow casting modes are dynamically toggled based on LOD state to reduce draw call overhead.

---

### Impact

**Memory:** Significant reduction in pointer overhead, critical for the Vita's limited memory.

**CPU:** Drastic reduction in main thread usage; logic and rendering now have more breathing room.

**Usability:** The tool is now a plug-and-play solution for any PS Vita Unity project.
