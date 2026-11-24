# Operational sidebar — approved UI direction

2026-11-24, R200 visual acceptance. Default workspace is map plus a narrow right operational sidebar. The sidebar has stable width and height; content or selection never resizes it. Viewport changes may adapt the overall layout, but selection must not. Manual hide/show is independent of selection; selecting a city or unit must never automatically reopen a hidden sidebar. Internal scrolling handles overflow.

The map is the primary selection and interaction surface. The sidebar identifies the current selected city/unit and offers concise context-sensitive command support, buttons and shortcuts when useful. Detailed information, planning and management belong in dedicated windows (City, Connection, Civilization, etc.). Avoid duplicating detailed data in the sidebar. English is canonical for new UI. Exact width and command set will be calibrated visually, not prematurely fixed in code.

This is a documented design decision only. Do not change the R199 visual construction acceptance build or navigation. Resume R200 construction verification before implementing the sidebar redesign.
