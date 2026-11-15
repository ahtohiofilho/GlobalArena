# Gate09 ADR-044 — executable NCP contract + tiny bracketed equilibrium

**Scope:** direct complementarity representation of ORIGINAL ADR043 continuous inverse score H=(1+C)*sqrt(Q/D), with exact simplex supply, unchanged tariffs/paths/congestion. This is NOT a fourth R38–R40 best-response solver.

The general \`complementarity_contract\` is **read-only**: checks every funded column plus canonical seller-specific path to every compatible reachable market, computes the minimum H and normalized Fischer–Burmeister residual phi(x/s,(H−minH)/max(1,maxH)), records original VI regret and independent route scans. It never claims original economic equilibrium using an NCP threshold.

For Q>0 the **ADR042 original F072 static certificate** is invoked, with original R13/R14, F084F, global route separation and both original regret gaps ≤1e−4. For reachable Q=0 the NCP H extension is finite, but ORIGINAL price remains infinite and the certification stays BLOCKED.

A deliberately limited \`bracketed_two_market_reference\` is the only executable calculation that CREATES a new physical candidate. It supports one group, two markets, exactly two direct disjoint edges and one commodity. It solves a 1D equation H1(a)=H2(s−a) with a strict maximum of 64 charged H evaluations (including boundary evaluations), then requires ADR042 PASS on the new state. Tests include a .6/.4 nonoptimal start, a start with Q=0, and an intentionally insufficient budget that returns NO-GO. It cannot run on ordinary multi-producer maps and does not change its input flows.

**Do not confuse:** the analytic Brouwer proof of existence in ADR043, this bounded reference, or the full-general MCP/VI with global route generation still to be engineered.

## Reproduce from repository root — Python 3.12 stdlib

    python -m unittest discover -s experiments/gate09/adr044_ncp_contract/tests -p 'test_*.py' -v
    python experiments/gate09/adr044_ncp_contract/ci/run_adr044.py --output .

20 tests including original stressed R34, frozen R20 manifest SHA16, F059 separation and independent F072/F084F. GitHub Actions runs on Ubuntu and Windows and uploads JSON/ZIP evidence.

Draft stacked over PR #332, no merge, no changes to original F072 or C#/Godot.
