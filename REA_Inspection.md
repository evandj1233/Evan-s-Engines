# REA inspection

REA 6.7.0 was used locally to inspect the existing CAD controller and the new application.

- Original controller SHA-256: b77c51f3a97ec7dca4539f35440b57073075098acc0e319819ffa85b51d95cfd
- Existing EngineKinematics CIL was inspected. It implements CAD poses and cosine valve motion, not an airflow/combustion solver.
- Engine Simulator CE's native executable was inventoried using REA's artifact inspection. This was a bounded static inventory, **not a decompilation of its engine physics**. Published MIT source was used instead.
- New application SHA-256: `9522bcf10da63c5d81de7484e7ec39625e98ea484aef9401c488f3ee8b112886`
- New app PE/CLI metadata coverage: complete. Managed architecture: x86_64.
- New `V10Lab.CamProfile` and `V10Lab.Model` methods were decoded as CIL with complete selected-type coverage.
- `CamProfile.cs` adapts Engine Simulator's published `src/function.cpp` triangle sampling and nearest-sample search, with finite-value and ordering checks.

REA does not contribute physics or validate outputs. Raw local evidence is retained with the installed app; this public summary omits local filesystem paths. Original source excerpts and their MIT license are retained locally. The complete Engine Simulator combustion/audio solver was not ported.
