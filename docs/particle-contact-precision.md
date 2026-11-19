# Particle contact precision

2026-11-05. Common CPU/GPU correction with5000 bodies,2 collision iterations,MSAA4,60 steps and the unchanged0.005 tolerance.

.NET10 full regression failed at step44/body3715. A preserved input snapshot reproduced the exact single-step failure. After one collision iteration, a near-boundary pair had penetration approximately0.0001707077 versus contact tolerance0.00017161811. Evidence narrows the cause to rounding in the sqrt/subtraction contact predicate being amplified into a finite impulse. GPU sqrt bits were not directly captured, so exact instruction-level rounding is not claimed. Adding precise only to distanceSquared did not fix the replay and was rejected.

CPU and GPU now share `closing < 0 && contactMinimum > 0 && distanceSquared < contactMinimum * contactMinimum`, where `contactMinimum = minimumDistance - contactTolerance`. This is the mathematically equivalent contact boundary, preserving tolerance, strength, visual quality and workload. Relevant shader intermediates use precise. No FP64 or new Vulkan capability is required; vulkan1.2/SPIR-V1.5 remain the targets. Original GLSL/SPV were protected; temporary compilation, spirv-val, disassembly, correctness and measurement preceded publishing matching source and binary.

A two-body regression fails with the original GPU shader. The published shader passes all1035.NET10 checks and19 pinned RC3 NativeAOT particle checks. See `artifacts/particle-replay-2026-11-05/native-published-full/result.json`. The new Native executable consumes its embedded production module with no test override. The existing artifact guard still rejects unpublished modules.

Matched Native executable/input,5000 bodies,2 iterations,MSAA4,VSync off,4096 warmup/8192 measured steps; three independent processes per variant, AB/BA/AB. Submission median0.1024 to0.1026ms; median/p95/p99 run ranges overlap. No speed improvement is claimed. Hidden-surface submission timing is not foreground application FPS. See `benchmark-comparison.json`, `published-shader.json` and raw CSVs. Local NVIDIA validation does not establish AMD/ARM/Pi hardware results.

References: [Khronos SPIR-V environment](https://docs.vulkan.org/spec/latest/appendices/spirvenv.html), [GLSL standard instructions](https://registry.khronos.org/SPIR-V/specs/1.0/GLSL.std.450.html), [GLSL4.60 precise](https://registry.khronos.org/OpenGL/specs/gl/GLSLangSpec.4.60.html).
