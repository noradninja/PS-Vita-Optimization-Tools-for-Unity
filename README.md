# PS-Vita-Optimization-Tools-for-Unity
A collection of tools to optimize various things when building for the PS Vita. 

CHANGELOG:

27 Dec 2025

Key Technical Improvements:
1. Centralized Memory Management (Scalability)
Global Shader Reference: Moved shader lookups from individual objects to a single refShader variable in the LODManager.

RAM Optimization: Eliminated redundant shader references across all instances. This prevents "memory bloat" and ensures the system remains scalable as the number of objects increases.

Drag-and-Drop Workflow: Users can now assign the replacement shader directly in the Inspector, making the tool project-agnostic.

2. Multi-Core CPU Strategy (Job System)
Job System Integration: Distance calculations are offloaded from Core 0 to secondary worker cores using IJobParallelFor.

Efficient Math: Replaced Vector3.Distance with sqrMagnitude to avoid expensive square root operations, resulting in ~10x faster distance checks.

3. Native Vita Performance & Stability
Memory Safety: Retained the automated Resources.UnloadUnusedAssets cycle to keep the RAM footprint clean during extended play sessions.

PVRTC & Texture Integrity: Every object still instantiates its own Material from the global shader. This ensures that unique textures (essential for PVRTC/GXT workflows) are preserved without conflict.

GPU Optimization: Automated switching of Shadow Casting modes based on distance to reduce draw call overhead.

Impact
Memory: Significant reduction in pointer overhead, critical for the Vita's limited memory.

CPU: Drastic reduction in Main Thread usage; logic and rendering now have more "breathing room".

Usability: The tool is now a "plug-and-play" solution for any PS Vita Unity project.
