# R203 — City selection by physical polygon

Manual R203 review of R202 e3a498f: default operational dock visible, approximately 19% of horizontal space, stable width and height when selecting a city and expanding commands. Legacy diagnostic controls still require horizontal scrolling; this is not accepted as the final operational design.

Approved interaction: the physical node containing a city is the city interaction surface. Clicking anywhere in its polygon opens City; marker and label are cartographic affordances for the same identity, not the required hit target. Empty nodes update selection without opening City. The right dock reflects selection and never opens itself when a city is clicked. Explicit individual worker selection remains separate. In the current signed construction preview city identity uses the existing occupied-node catalogue, not a new simulated entity. Existing legacy QA behavior is retained outside this preview. No navigation or simulation changes.

Pending manual checks: click away from marker but inside city polygon, close City and repeat, hide dock and select city again, select empty node, check dimensions and completed-construction visibility. Remove legacy diagnostics from the narrow dock in a subsequent focused UI increment; do not widen the dock to accommodate them.
