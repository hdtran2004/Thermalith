# M3 single-row diagnostic build

Based on Thermalith 1.2.0. This is an independent build, not an official upstream release.

For model ID 6400 (M3) only, identical adjacent raster rows are sent separately rather than compressed into a repeat packet. Density, paper type, pixel counts, indexed-row behavior, and the print task are unchanged. Other models retain compression.

Close the official Thermalith before running this build. It uses the same application settings folder; back up important labels and reopen a saved copy for testing. Enable Help > Connection Logging and print ONE copy of the existing failing label with the same settings. Compare the print and save the log. No firmware changes are made.

Hardware result: the user confirmed that this build removes the vertical streaks on an M3, firmware 2.85, connected over USB on Windows 11, using a 40 x 20 mm transparent label. The same label streaked in the official 1.2.0 build and printed correctly in NIIMBOT software. This supports an M3 repeated-row compatibility issue; it does not establish the exact firmware behavior or coverage of other firmware versions.

Validation: all 45 Niimbot.Net tests passed, including the added check for uncompressed blank/content rows and their positions. The self-contained Windows x64 application published successfully.

Revert by closing this build and launching the official application.

GPL-3.0-or-later; original copyright and licensing are retained in LICENSE and the source tree.
