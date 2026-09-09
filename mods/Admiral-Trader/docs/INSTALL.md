# Admiral Trader 0.1.0+milestones installation

## Clean install or upgrade

1. Stop the SPT server and launcher.
2. Back up `SPT_Runtime/user/profiles` before changing the installed mod set.
3. Remove both known old install directories if present:
   - `SPT_Runtime/user/mods/Admiral Trader`
   - `SPT_Runtime/user/mods/Admiral-Trader`
4. Extract the release-candidate ZIP into the SPT installation root. The resulting canonical server path must be:
   - `SPT_Runtime/user/mods/Admiral-Trader/Admiral Trader Server.dll`
5. Start the server and confirm that exactly one `Admiral Trader` mod and one trader ID `d5c27bb3169f8dfbc13f6b69` are registered.

Deleting the previous directory before copying is required for upgrades. Copying over an older install can leave retired quest or manifest files in place.

## Clean removal

1. Stop the SPT server and launcher.
2. Remove both directories listed above if either exists.
3. Do not delete or edit profile files. Admiral Trader performs no direct profile migration or cleanup writes.
4. Start the server once and confirm that Admiral Trader is absent and unrelated mods still load.

Completed quest history and trader data may remain in the profile as native SPT historical state. Reinstalling the same persistent trader and quest IDs restores their normal interpretation. Do not use a profile editor to delete Admiral records.

## Compatibility

- Runtime metadata: `~4.1.0` (compatible SPT 4.1.x patches).
- Exact build and release validation baseline: SPT 4.1.5.
- No external mod dependency is required.
