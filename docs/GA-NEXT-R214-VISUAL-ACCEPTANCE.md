# R214 — Consolidated visual acceptance

Date: 2026-11-24. R213 PR #606, exact HEAD 3455813f9a258d6ac2c870fe4fb6bdd873353e1f, 12/12 CI workflows successful. Manual Windows Godot DEBUG, signed fine-first economy lab, persistent queues v20. Evidence: user screenshots in the R214 review conversation; screenshots are not attached to this repository.

## Observed sequence

| Opening turn | Revision | Cash | Observation |
|---|---:|---:|---|
| 1 | 0 | 0.000000 | Map centered at Site 0178, compact dock, queues initially empty |
| 3 | 4 | 5.200184 | Connection 178–251, edge 700, freight 0.250887, three workers |
| 4 | 7 | 5.400276 | Project 1 funded 2.4 with worker 2; work 1/2.4 (42%), active capital 0 |
| 5 | 9 | 8.000368 | No new assignment; work 2/2.4 (83%) |
| 6 | 12 | 10.600461 | Explicit interruption; awaiting workforce, work 2 and funding 2.4 preserved |
| 6 (pending) | 12 | 10.600461 | Worker 2 queue Build #1 then Agriculture |
| 7 | 16 | 13.536608 | Build removed, Agriculture active, connection owner 1, active capital 2.4; construction section hidden |
| 8 | 21 | 16.139033 | Worker 3 move to 317 active, current node 251 |
| 9 | 23 | 18.651964 | Same node, move still active |
| 10 | 26 | 21.100573 | Worker 3 node 249, move still active |
| 11 | 28 | 23.712566 | Worker 3 arrived at 317; queue empty |
| 12 | 32 | 23.659305 | Project 2 funded 2.4, remote worker 3 active Build #2, work zero; prior capital 2.4 remains active |
| 14 | 37 | 28.480171 | Worker 3 at 251, work 0.2/2.4 (8%), automatic approach without explicit move |

## Disposition

Manual scenario accepted. Fractional construction after arrival directly observed; 80% travel inferred from neutral work rate and remaining budget. Project 1 residual agriculture 60% expected and compatible with cash, not independently measured visually; covered by R211 automated test. Actual freight after activation inferred from displayed projection 0.136251 plus reduction 0.033078 = 0.169329, not directly visible due to scroll. Manual reorder/insert not exercised; CI covers reorder/remove. Trade edges zero throughout; no claim of cross-node trade validation. Project 2 completion not exercised. No production calibration or large-scale acceptance implied.

## UI debt

English canonical: Human + IA and Portuguese temporal readout remain. Empty queue takes excessive height; insertion button enabled without selection; legacy adjacent-only guidance and physical-step wording stale. No crossing progress, journey estimate or route overlay. Temporary worker presence produces Site markers, not necessarily permanent cities. Construction status says Under construction while assigned workforce still traveling. Long connection panel scrolls; IDs and text lists are lab affordances, not desired primary gameplay interaction. Preserve approved navigation and fixed dock behavior.
