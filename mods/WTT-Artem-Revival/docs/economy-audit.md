# Artem Revival — Economy Audit

This audit is based on the repaired authoritative runtime core. It records the economy before rebalance; it does not change prices, rewards, loyalty levels, flea behavior, or PBS integration.

## Trader catalog baseline

- root offers: **281**;
- custom Artem templates offered as roots: **127**;
- custom Artem rouble offers: **126**;
- custom Artem barter offers: **1** (`Modernized Item Case`);
- loyalty distribution across all roots: LL1 **84**, LL2 **103**, LL3 **67**, LL4 **27**.

Custom rouble offer range is **200–324,250 RUB**. This range is not itself treated as a defect because the catalog spans patches/accessories through armor, rigs, backpacks and containers.

## Important pricing finding

Many custom item definitions carry placeholder-like handbook/flea values (commonly `handbookPriceRoubles: 200` / `fleaPriceRoubles: 300`) while trader prices are much higher. Examples include major plate carriers whose trader prices are roughly 150k–205k RUB.

Therefore handbook/flea values must **not** be used as the sole rebalance reference. Any future normalization must compare the item's cloned vanilla base, protection/capacity/slot properties, trader LL, buy limits, and comparable live SPT equipment.

No automatic normalization is applied during the compatibility port.

## Quest reward baseline

Across 23 quests, Success rewards contain:

- `AssortmentUnlock`: **40**;
- `Item`: **36**;
- `TraderStanding`: **25**;
- `Experience`: **23**;
- `Skill`: **5**.

Rouble cash rewards found in the authored chain range from **25,000 to 80,000 RUB**. XP rewards range from **4,000 to 35,000 XP**.

## Review targets — not proven defects

Several reward structures deserve explicit campaign review before any rebalance:

- `Puppets` contains two Experience rewards (`20,000` and `5,000`) and standing rewards for two traders;
- `Rags to Riches` gives Artem standing and `-0.02` standing to another trader;
- `The Keycard Holder` gives standing to Artem and another trader.

Cross-trader standing is plausible authored progression and must not be deleted merely because it is unusual. The duplicate XP in `Puppets` is likewise preserved until campaign intent/runtime payout is checked.

## Rebalance policy

Economy changes are gated behind runtime and campaign smoke tests. When that gate opens:

1. keep patches/cosmetics inexpensive unless scarcity/progression requires otherwise;
2. price functional armor/rigs/backpacks against comparable SPT items, not Artem's placeholder handbook values;
3. preserve meaningful quest unlock pacing;
4. inspect buy limits/stock together with price;
5. evaluate reward value as a whole (cash + items + unlocks + standing + XP), not cash in isolation;
6. do not inject Artem gear into PBS pools as part of economy normalization.
