# Rambo red HeadBand asset

This directory contains the reproducible source and runtime output for the
B&A&HB Utility HeadBand visual. The runtime item keeps its existing persistent
template ID; only its `Prefab.path` changes to the owned bundle key
`HeadBand/headband_rambo_red.bundle`.

## Rebuild

1. Run `generate_headband.py` with Blender 4.2 in background mode. It produces
   the `.blend`, `.fbx`, and transparent inventory icon under `generated/`.
2. Copy the generated FBX to `unity/Assets/HeadBand/headband_rambo_red.fbx`.
3. Run Unity `2022.3.62f2` in batch mode with
   `BuildHeadBandBundle.Run`, passing `-template=<belt_fannypack.bundle>` and
   `-output=<unity/AssetBundles>`.
4. Copy the resulting `headband_rambo_red.bundle` to
   `runtime/bundles/HeadBand/` and run the deterministic tests.

The reference Pack 'n' Strap bundle is a local build input and is ignored by
Git. The published runtime bundle contains the B&A&HB mesh/material and a
minimal inspect/loot prefab; it does not add a Pack 'n' Strap dependency.
