# MIDI Sources

All three MIDI files in this folder were sourced from the [Mutopia Project](https://www.mutopiaproject.org/), which publishes user-contributed engravings of public-domain classical compositions. The arrangements themselves are also released under Public Domain (no attribution required), unlike CC-BY arrangements seen elsewhere.

Per the KeyFlow v2 design spec §11.2, only Public Domain or CC0 MIDI arrangements are acceptable for this project — CC-BY arrangements are excluded to keep redistribution frictionless.

## Per-file attribution

| File | Composition | Composer | Mutopia Piece | License |
|---|---|---|---|---|
| `ode_to_joy.mid` | Symphony No. 9, 4th movement ("Ode to Joy") theme | Ludwig van Beethoven | 528 | Public Domain |
| `clair_de_lune.mid` | Suite bergamasque, L. 75 — "Clair de Lune" | Claude Debussy | 1778 | Public Domain |
| `the_entertainer.mid` | The Entertainer (ragtime) | Scott Joplin | 263 | Public Domain |

To look up each piece on Mutopia, search the [Mutopia archive](https://www.mutopiaproject.org/) by piece number or composer name; the License field in the search results should read `Public Domain`.

## Note on Für Elise

`Für Elise` (WoO 59) does NOT have a Mutopia MIDI in this folder. Its chart (`Assets/StreamingAssets/charts/beethoven_fur_elise.kfchart`) is hand-authored directly as JSON for the W3 device verification milestone — composition is PD (Beethoven), arrangement is original self-authored work by the project author.

## Note on Canon in D

`Canon in D` (Pachelbel) was evaluated as a 4th song candidate but deferred: the Mutopia archive lists ALL Canon in D MIDI arrangements as CC-BY or CC-BY-SA (modern harmonic realizations from the figured bass are protectable creative choices, even though the composition itself is PD). Per spec §11.2 constraints, no compliant MIDI source is currently available for this piece.
