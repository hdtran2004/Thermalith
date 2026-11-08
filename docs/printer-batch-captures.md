# NIIMBOT Windows USB batch captures, 2026-11-09

Local captures using USBPcap 1.5.4.0. The user verified continuous printing without between-label retraction for three identical copies and three distinct labels on both M2_H and M3.

M2_H: PrintStart payload 0003000000000000 (8 bytes). Each distinct page: PageStart, six-byte SetPageSize with copies=1, image rows, PageEnd. Identical copies upload once with copies=3. Poll A3/B3 (completed-label u16 followed by print/feed percentages). One PrintEnd after all labels complete.

M3: PrintStart payload 000300000000000000 (9 bytes). No PageStart. SetPageSize has 13 size bytes plus the same 32 ASCII hex job identifier per page (45 total). PageEnd after each page. Poll 89/0000, reply 88 with completed count at offsets 3..4 and print/feed percentages at 5..6 in the observed 13-byte payload. One PrintEnd at completion.

Official M3 image transfer uses compression. Thermalith retains its working uncompressed raster encoding and no repeated rows, adopting the captured job/page setup and polling. This combination still needs physical validation; it is not a byte-for-byte reproduction of the official image transfer.

The implementation bounds page lookahead and persists parameter progress only from printer-reported completed-label counts. Upload acknowledgements do not advance counters. Explicit negative setup acknowledgements may be retried; page submissions and ambiguous failures are not automatically retried. Cancellation ends the job after the send loop stops.

Raw captures remain outside this repository in the local captures folder.
