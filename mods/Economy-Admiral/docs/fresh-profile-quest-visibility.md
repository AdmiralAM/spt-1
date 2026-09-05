# Fresh-profile onboarding, PR #327

The reported 2026-09-05 session makes `/client/quest/list` request 42 at
09:18:01.886 +03:00 and receives a response at 09:18:02.409. Its body is not
recorded (`responseText: .`); these logs cannot establish the actual response
contents. The corresponding profile is level 1, Usec, with an empty quest list
and Admiral unlocked at loyalty 1, standing 0.

All 31 installed Admiral quest templates require level >=5 or higher. The
first Access quest, `5d404ebd654de4efecef71d2`, requires only level >=5.
It has no prerequisite quest, loyalty or standing condition, is not secret,
and has side `Pmc`. Subsequent quests keep their level and completion gates.

The installed SPT 4.1.4 assembly's `QuestHelper.GetClientQuests` checks existing
profile quest status first. For quests absent from the profile, it checks
side, edition blacklist/whitelist, event restrictions and player level, then
trader presence, quest prerequisites, loyalty and standing. A qualifying
initial quest is returned with `AvailableForStart`; it need not already be
stored in the profile. `QuestController.AcceptQuest` creates its Started entry.
Thus the level-1 profile fails the level gate for every Admiral quest before
client filtering. Registration of the 31 templates does not prove visibility.

The combined packaging step now adjusts only the staged Fundamentals level
condition to 1. It checks the expected identity and condition shape first and
does not edit the frozen Trader checkout. A regression verifies all 31 staged
templates, unchanged downstream conditions and idempotent staging.

The installed WTT `HideSecretLockedQuestsPatch` hides a quest only when
`Template.ServerOnly` is true and status is 0 (Locked). It cannot explain the
server's level filtering. Quest Tracker's QuestsScreen Show postfix sets its
screen-active flag and collects raycasters. TraderModding's client patches
target weapon modding screens. Ref Friendly Quests edits its explicit Ref
quest IDs and Ref loyalty. Economy's client exposes configuration controls;
its registration/audit logs do not establish a client quest visibility result.
AllQuestsCheckmarks logs an item specification checkmark error, not a quest-list
response error. These observations are not an exhaustive compatibility proof
for Quest Tracker, AllQuestsCheckmarks, QuestManiac or other installed patches.

An isolated copy of the installed runtime, including its server mods and a copy
of the level-1 profile, was started on localhost port 6979. Compressed requests
using the copied profile session produced the following actual HTTP results:

- Before the staging correction: `/client/quest/list` returned HTTP 200 with
  zero Admiral quests.
- After the correction and restart: the same route returned exactly Fundamentals
  with `sptStatus: 1` (AvailableForStart), level >=1.
- Posting QuestAccept to `/client/game/profile/items/moving` returned `err: 0`
  and no warnings. The subsequent quest list returned Fundamentals with
  `sptStatus: 2` (Started).

This exercise included Economy Admiral, QuestManiac, Ref Friendly Quests,
AllQuestsCheckmarks and TraderModding server components from the installed
runtime. Only the isolated profile was used for acceptance; the game installation
was not modified. Original logs lack response bodies, so this is reproduced
HTTP evidence, not a reconstruction of the historical response body.

Remaining acceptance evidence: verify that the real client visibly shows the
corrected quest and allows the player to accept it. HTTP acceptance does not
establish rendering or every client-patch interaction.
