# ADR-006 — Goldberg Topology Invariants

**Status:** Accepted
**Date:** 2026-09-19
**Last revised:** 2026-09-19

## Context

M2 precisa transformar a ideia de planeta Goldberg em contratos topológicos verificáveis antes de implementar geradores, IDs ou estruturas de produção.

O projeto exige simultaneamente:

- topologia planetária fechada;
- camada estratégica baseada em regiões Goldberg;
- camada tática de resolução muito superior;
- fileiras compartilhadas de subtiles nas fronteiras;
- navegabilidade sem bordas artificiais;
- determinismo e execução headless;
- independência entre topologia lógica e representação gráfica.

A hipótese de refinamento estratégico → tático para famílias `G(m,n)` ainda não foi demonstrada.

## Decision

### Família matemática de referência

O baseline de M2 usa a família icosaédrica de poliedros de Goldberg com pentágonos e hexágonos.

Para parâmetros inteiros não negativos `m` e `n`, não ambos zero:

`T = m² + mn + n²`

Para a família icosaédrica:

- faces Goldberg: `F = 10T + 2`;
- arestas Goldberg: `E = 30T`;
- vértices Goldberg: `V = 20T`;
- faces pentagonais: exatamente `12`;
- faces hexagonais: `10(T - 1)`.

A identidade de Euler deve permanecer:

`V - E + F = 2`

Cada vértice Goldberg possui incidência de três faces.

### Correspondência com o domínio

`StrategicCell` corresponde a uma face Goldberg.

`StrategicEdge` corresponde à fronteira compartilhada por duas faces Goldberg.

`StrategicVertex` corresponde a um vértice Goldberg.

O grafo utilizado por algoritmos estratégicos possui um nó por `StrategicCell` e adjacência quando duas células compartilham um `StrategicEdge`.

Portanto:

- uma célula pentagonal possui cinco vizinhas;
- uma célula hexagonal possui seis vizinhas;
- cada `StrategicEdge` possui exatamente duas células incidentes;
- cada `StrategicVertex` possui exatamente três células incidentes;
- o grafo de células deve ser conexo.

### Caso mínimo de referência

`G(1,0)` é o dodecaedro.

Nesse caso:

- `T = 1`;
- `12` StrategicCells;
- `30` StrategicEdges;
- `20` StrategicVertices;
- `12` células pentagonais;
- `0` células hexagonais.

O número `20` no dodecaedro corresponde aos vértices, não às faces.

### Topologia antes da geometria

A topologia lógica não depende das coordenadas usadas para renderização.

A implementação topológica deverá ser baseada em relações combinatórias e identidades estáveis.

Coordenadas de ponto flutuante não serão fonte de verdade para:

- identidade;
- adjacência;
- incidência;
- pertencimento;
- ordering;
- navegação.

Uma realização geométrica pode usar projeção esférica, embedding poliédrico ou triangulação de renderização.

M2 não assume que todos os vértices possam ser simultaneamente forçados a uma esfera exata e que todas as faces poligonais permaneçam exatamente planas.

### Determinismo

Para os mesmos parâmetros topológicos e a mesma versão do gerador:

- as mesmas entidades lógicas devem existir;
- suas identidades devem ser estáveis;
- as mesmas relações de adjacência e incidência devem ser produzidas;
- a ordem física de criação em memória não pode alterar a identidade lógica.

### Fronteiras táticas compartilhadas

O V1 exige fileiras compartilhadas de subtiles entre regiões estratégicas vizinhas.

M2 deverá preservar esse requisito sem duplicar silenciosamente uma mesma entidade lógica em duas regiões.

Qualquer subtile, faixa ou elemento de fronteira compartilhado deverá possuir:

- identidade canônica única;
- incidência explícita às entidades estratégicas relevantes;
- correspondência determinística vista pelos dois lados da fronteira.

A forma exata de ownership permanece aberta.

São candidatos válidos para investigação:

- ownership por `StrategicEdge`;
- incidência múltipla explícita;
- estrutura própria de border band.

Nenhum desses modelos é congelado por este ADR.

### Refinamento hierárquico

Não é assumido que qualquer `G(p,q)` de maior resolução forme automaticamente um refinamento pai-filho válido de qualquer `G(m,n)`.

Compatibilidade de refinamento deverá ser demonstrada.

O projeto poderá:

- suportar apenas famílias compatíveis;
- restringir combinações de parâmetros;
- usar relações de submalha explicitamente demonstradas.

Uma restrição desse tipo não viola a arquitetura de duas escalas.

### Invariantes mínimos de validação

Todo gerador de topologia estratégica deverá permitir verificar:

- contagens esperadas de faces, arestas e vértices;
- exatamente 12 pentágonos;
- grau 5 para pentágonos;
- grau 6 para hexágonos;
- incidência 2 por aresta;
- incidência 3 por vértice;
- Euler igual a 2;
- conectividade global;
- ausência de self-loops;
- ausência de adjacências duplicadas;
- reciprocidade de adjacência;
- identidades estáveis;
- geração reproduzível.

## Consequences

Positive:

- a geometria visual não contamina a verdade topológica;
- erros podem ser detectados por invariantes matemáticos;
- pathfinding estratégico pode operar sobre um grafo pequeno;
- determinismo de M1 é preservado;
- fronteiras táticas podem evoluir sem duplicar identidade lógica;
- famílias Goldberg incompatíveis podem ser rejeitadas explicitamente.

Negative:

- refinamento estratégico/tático exige spike próprio;
- representação de border bands permanece uma decisão futura;
- projeção visual esférica pode exigir triangulação ou tratamento geométrico separado;
- IDs canônicos estão definidos, mas a regra concreta de atribuição durante a geração ainda precisa ser provada.

## Review

M2.1.2 materializou o contrato de parâmetros e contagens definido por este ADR.

Evidência de referência:

- commit: `ea690f6ba8fbd13d2ca5f485ae4e47a23eb60f6b`;
- workflow: `Cross-Platform Kernel Regression Validation`;
- run ID: `35446789732`;
- .NET SDK: `10.0.401`;
- Ubuntu 24.04.5: 139/139;
- Microsoft Windows Server 2025: 139/139;
- macOS 26.6.2 arm64: 139/139;
- total: 417 execuções aprovadas;
- falhas: 0;
- skipped: 0.

Essa evidência valida o contrato de parâmetros e contagens.

M2.1.3 materializou também o contrato de identidade topológica estratégica.

Evidência de M2.1.3:

- commit: `11f7260ea8e7bfe0761e87cecfbe1f45a5f38a8c`;
- workflow: `Cross-Platform Kernel Regression Validation`;
- run ID: `35447941816`;
- .NET SDK: `10.0.401`;
- Ubuntu 24.04.5 / x64: 154/154;
- Microsoft Windows Server 2025 10.0.26100 / x64: 154/154;
- macOS 26.6.2 / arm64: 154/154;
- total: 462 execuções aprovadas;
- falhas: 0;
- skipped: 0.

O contrato de identidade cobre tipos distintos, ordinal canônico one-based, rejeição explícita de zero e sentinela `default` inválida.

A evidência ainda não demonstra geração de faces, arestas e vértices, incidência, adjacência ou refinamento estratégico/tático.

Reavaliar quando:

- o spike de refinamento hierárquico for concluído;
- o modelo de fileiras compartilhadas for congelado;
- uma família Goldberg oficialmente suportada for restringida ou ampliada.
