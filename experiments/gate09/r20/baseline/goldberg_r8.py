"""Deterministic class-I Goldberg G(m,0) *logical* graph fixture.

Construction subdivides the 20 icosahedron triangles into m^2 triangles.
Its vertices are the 10*m^2+2 strategic nodes, with 12 degree-five nodes
and all other nodes degree six. Geodesic length and rendering LOD play no
role in the economic graph. This is an independent QA topology fixture,
NOT the game's actual WorldGen or a geodesic economic distance function.
"""
from __future__ import annotations
from collections import Counter


def _faces():
    # 0 is north; 1..5 upper ring; 6..10 lower ring; 11 south.
    for i in range(5):
        u = 1 + i
        un = 1 + (i + 1) % 5
        lo = 6 + i
        lop = 6 + (i - 1) % 5
        lon = 6 + (i + 1) % 5
        yield (0, u, un)
        yield (u, un, lo)
        yield (u, lo, lop)
        yield (11, lo, lon)


def logical_goldberg(m: int):
    """Return (node_count, edges) with stable IDs/endpoints for G(m,0)."""
    if type(m) is not int or not 1 <= m <= 40:
        raise ValueError('Class-I Goldberg m must be integer in [1,40]')
    nodes = set()
    edges = set()
    for corners in _faces():
        local = {}
        for i in range(m + 1):
            for j in range(m - i + 1):
                k = m - i - j
                key = tuple(sorted((vertex, amount) for vertex, amount in zip(corners, (i, j, k)) if amount))
                local[(i,j,k)] = key
                nodes.add(key)
        for (i,j,k), left in local.items():
            for di,dj,dk in ((1,-1,0),(1,0,-1),(0,1,-1)):
                right = local.get((i+di,j+dj,k+dk))
                if right is not None:
                    edges.add(tuple(sorted((left,right))))
    keys = sorted(nodes)
    ids = {k:i for i,k in enumerate(keys)}
    pairs = sorted(tuple(sorted((ids[a], ids[b]))) for a,b in edges)
    if len(keys)!=10*m*m+2 or len(pairs)!=30*m*m:
        raise AssertionError('Goldberg topology Euler count mismatch')
    degree=Counter(v for a,b in pairs for v in (a,b))
    if sum(1 for d in degree.values() if d==5)!=12 or any(d not in (5,6) for d in degree.values()):
        raise AssertionError('Goldberg expected 12 pentagons, others hexagons')
    return len(keys), tuple((i, a, b) for i,(a,b) in enumerate(pairs))


def as_network(m:int, *, demand:float=2.0, supply:float=2.0):
    """A single-producer single-market fixture on the logical Goldberg network."""
    from gate09 import Edge,Group,Market,Network
    n, lines=logical_goldberg(m)
    src=0; dst=n-1
    edges=[Edge(eid,a,b,c_free=1.,capacity=1000000.) for eid,a,b in lines]
    return Network(edges,[Group(1,src,0,1,supply)],[Market(dst,0,demand)])
