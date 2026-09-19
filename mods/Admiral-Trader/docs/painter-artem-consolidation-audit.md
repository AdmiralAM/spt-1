# Painter/TGC and Artem consolidation audit

## Decision

Painter/TGC and Artem persistent item, offer, clothing, quest and message IDs are retained, while their trader-facing state is consolidated into the permanent Admiral trader `d5c27bb3169f8dfbc13f6b69`. Artem's complete runtime data is embedded in Admiral Trader and no separate Artem DLL or active mod folder is required. The existing Admiral campaign and storefront are preserved unchanged. Belt, armband and secure-container compatibility remains outside this module.

The source set is Painter 3.0.0 (`4f4735…d2d3be`), TGC 3.0.0 (`932aaa…d2726`) and WTT-Artem 3.0.2 (`11e5bf…6318d`). The measured installed Artem runtime supplied the preserved 703-row repaired assortment used by the embedded contract. Its distributed trader, quest, offer, item, clothing and zone identities are unchanged.

## Actual scope

| Source | Quests | Root offers | Assort rows | Quest unlocks | Clothing suits | Runtime role |
|---|---:|---:|---:|---:|---:|---|
| Existing Admiral | 172 core + 10 optional Icebreaker | 144 with installed optional WTT storefront | existing | 18 native Admiral gates | existing | Preserved authority |
| Painter 3.0.0 | 12 | 7 | 7 | 3 | 0 | Quest and small specialty stock provider |
| TGC 3.0.0 | 0 | 114 | 236 | 0 | 4 | Painter-owned gear and clothing provider |
| Artem | 23 | 281 | 703 | 41 | 64 | Quest, gear and clothing provider |
| Consolidated external addition | **35** | **402** | **946** | **44** | **68** | Re-owned by Admiral |

The resulting campaign is 207 quests without Icebreaker and 217 with the installed ten-quest Icebreaker chain. This replaces the obsolete 43-quest assumption with the actual current source count.

## Quest graph

Painter contains two roots. `Taped Up` starts the main ten-quest workshop/cosmetics line; `Resistance Is Futile` starts a separate two-quest material line. `Born Free` branches into `Shrink Wrap` and `The Dark Knight`, after which `The Final Touch` completes the latter branch.

| Painter quest ID | Name | Direct prerequisite | Map |
|---|---|---|---|
| `668aacd1dee3de3ce276fdef` | Taped Up | — | Any |
| `668aace8ff74aecfbcfbe9e6` | Topographics | Taped Up | Any |
| `668aad1d97c0b19780ebf9c2` | Dig Deep | Topographics | Reserve |
| `668aad2b0f0c52ff9b51625d` | Workshop Refurbishment | Dig Deep | Any |
| `668aad328a1b4ad3818169cf` | Radio Silence | Workshop Refurbishment | Any |
| `668aad49a21e4d37c83d4ffc` | Dangerous Waters | Radio Silence | Shoreline |
| `668aad3c3ff8f5b258e3a65b` | Born Free | Dangerous Waters | Any |
| `668c18eb12542b3c3ff6e20f` | Shrink Wrap | Born Free | Any |
| `6848f88e54cef2b50b3a2589` | The Dark Knight | Born Free | Any |
| `684dcdde28c416fb7410b974` | The Final Touch | The Dark Knight | Any |
| `684f091cc78564e2180a2eb6` | Resistance Is Futile | — | Any |
| `685862c625c24fd649b370c6` | Moscovium | Resistance Is Futile | Any |

Artem has one introduction root. It branches immediately into `Expanding Wardrobe` and the main `Grab n' Tag` line. The main line later branches at `Uncovered Businesses` into the seasonal detour and the longer equipment/intelligence sequence.

| Artem quest ID | Name | Direct prerequisite | Map |
|---|---|---|---|
| `673f06ffd971eef67d8cc504` | Introduction | — | Any |
| `6740b1c44382955ae7e87eb9` | Expanding Wardrobe | Introduction | Factory |
| `673f0b6c85753ffd03ad5f34` | Grab n' Tag | Introduction | Lighthouse |
| `673f0daebb711e9700b60899` | Uncovered Businesses | Grab n' Tag | Any |
| `673f0f4d219756e158de7ab3` | Eye for an eye | Uncovered Businesses | Any |
| `6740b5875ef4d62a019c51f2` | Spooky Season | Uncovered Businesses | Shoreline |
| `6740b70d0c2f488b92d69575` | Wanderer | Spooky Season | Reserve |
| `6741d2a8ee39dbd555b6468d` | Fetch it | Wanderer | Any |
| `6741d50ae7f5bd50d5034129` | Communication is Key | Fetch it | Any |
| `6741d6176c4b417a408d290f` | Rags to Riches | Communication is Key | Shoreline |
| `6741d74a7ca8a4abfdf908c6` | The Lost Package | Rags to Riches | Any |
| `6741d86720827d719dafa146` | Puppets | The Lost Package | Any |
| `6741da44d68092ca9722521a` | Secret Formula | Puppets | Any |
| `6741dbba9e6d28c8ff0a700f` | The Keycard Holder | Secret Formula | Any |
| `6741dd080a4b6768df84d4ed` | Bigger Fish | The Keycard Holder | Reserve |
| `6741de5af218c3a4a71f659e` | Abandoned Enterance | Bigger Fish | Factory |
| `6745faad87494344ac6986e7` | Finding Equipment | Abandoned Enterance | Any |
| `6745fdae9426c1e4a9fd5772` | Rearming, Preparing. | Finding Equipment | Any |
| `67462d52d129aa9ebc6eb98d` | Audio interference | Rearming, Preparing. | Interchange |
| `67463059a075ba4f50c0e396` | Gathering Information - Part 1 | Audio interference | Reserve |
| `6746319d051c463a810728fc` | Gathering Information - Part 2 | Part 1 | The Lab |
| `67464750efd379bfffb1d539` | Gathering Information - Part 3 | Part 2 | The Lab |
| `674652a3507f4bb1aecd1086` | False Prophets - Part 1 | Part 3 | Any |

## Runtime ownership and safety

1. Admiral registers temporary compatibility shells for Painter and Artem before profile validation. A removed provider therefore cannot trigger `InvalidModdedTraderException` while migration is pending.
2. After external providers publish their templates, quests, assortments and clothes, Admiral merges all persistent offer/item trees, loyalty mappings, quest unlocks and suits without regenerating IDs.
3. Quest ownership, trader-standing conditions and trader-specific rewards are re-owned by Admiral. Quest IDs, prerequisites, objective data, rewards and localisations remain unchanged.
4. Existing profile standing and sales keep the strongest recorded value. Purchase ledgers, dialogue/messages, insurance and repeatable-quest ownership move to Admiral. The operation is marked and idempotent.
5. Legacy trader database records are removed only after every loaded profile migration saves successfully. This removes the two extra trader tabs while preserving the content and historical profile state.
6. Stale Painter/Artem profile ownership is migrated when present, empty compatibility shells are removed, clean profiles are not written, and the original Admiral campaign/store remain unchanged.
7. Painter and Artem quest locales, image routes, custom items, clothing and zones are published by Admiral. Artem zone IDs and objective references are retained unchanged.
8. Every external assort is checked for complete parent trees, barter keys and loyalty keys. Every external quest prerequisite is checked against the final quest table. Missing external templates remove only the affected optional offer tree or external quest graph; the 172-quest Admiral core remains active.

## Validation result

The exact integration contract uses Admiral TGC Integration PR #362 head `bd1500b86c356f5e97fade75cf0c1df974ae9621`, its 117 upstream TGC templates, a content-only Painter layer without `Painter-4.0.dll`, and embedded Artem data owned by Admiral. It must consolidate **402 offers / 946 item rows / 35 quests / 44 quest unlocks / 68 suits**, remove the legacy trader records, and reach `Server has started, happy playing`. The content-only Painter layer also preserves its five template identities, Hall of Fame compatibility, two non-lootable quest figurines, and the authored 175-entry/20-draw Special Delivery pool. A deterministic synthetic-profile test proves quest state, relation, standing, loyalty, sales, purchase and dialogue preservation and confirms the second pass is a no-op. `external-content-identities.json` records every source quest, offer, item, unlock and suit ID used by this contract.
