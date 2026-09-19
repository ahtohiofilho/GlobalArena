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

M2.1.4 materializou a topologia estratégica mínima `G(1,0)` como dual combinatório de um icosaedro canônico.

Evidência de M2.1.4:

- commit: `a83eb7566b4128ac2d3a803b5008327123cf2eab`;
- workflow: `Cross-Platform Kernel Regression Validation`;
- run ID: `35450592513`;
- .NET SDK: `10.0.401`;
- Ubuntu 24.04.5 / x64: 171/171;
- Microsoft Windows Server 2025 / x64: 171/171;
- macOS 26.6.2 / arm64: 171/171;
- total: 513 execuções aprovadas;
- falhas: 0;
- skipped: 0.

O caso mínimo prova 12 cells, 30 edges, 20 vertices, incidência, adjacência, reciprocidade, conectividade, Euler e atribuição canônica de IDs sem coordenadas de ponto flutuante.

M2.1.5.A generalizou a geração estratégica para a família Class I `G(m,0)` / `G(0,n)` usando subdivisão inteira determinística da seed icosaédrica.

Evidência de M2.1.5.A:

- commit: `22ba5cf7f6b4604d3356ecb786dbd6a181e15f1f`;
- workflow: `Cross-Platform Kernel Regression Validation`;
- run ID: `35455738612`;
- Ubuntu: job concluído com `success`;
- Windows: job concluído com `success`;
- macOS: job concluído com `success`;
- suíte local: 179/179, 0 falhas, 0 skipped;
- artefato Ubuntu: ID `10588466857`, SHA-256 `43e8c0c2bcd5afd1c73ec2ba028ed6ca743055b0f3b7bb1ae6930198db98c48b`;
- artefato Windows: ID `10588277250`, SHA-256 `4bf68f2e2be167f4c334e054da2fc57b66fb896c51015deb5b137224f26553bb`;
- artefato macOS: ID `10588506829`, SHA-256 `0da37493fd417ef1847e073458138a9aafe2123a1db2c7fb0a7b798b73520f60`.

Casos validados na tranche:

- `G(2,0)`;
- `G(0,2)`;
- `G(3,0)`.

A implementação preserva contagens, 12 pentágonos, graus 5/6, incidência 2 por edge e 3 por vertex, reciprocidade, conectividade, Euler e IDs canônicos determinísticos.

Class II `G(1,1)` e Class III `G(2,1)` permanecem explicitamente não suportados nesta tranche.

M2.1.5.B adicionou suporte à família Class II `G(k,k)` por meio de uma seed triangular canônica derivada combinatoriamente da seed icosaédrica.

Evidência de M2.1.5.B:

- commit: `5897e929d46e964ef544404c2f6b14b2dc3fc436`;
- workflow: `Cross-Platform Kernel Regression Validation`;
- run ID: `35457406818`;
- Ubuntu: job concluído com `success`;
- Windows: job concluído com `success`;
- macOS: job concluído com `success`;
- suíte local: 185/185, 0 falhas, 0 skipped;
- artefato Ubuntu: ID `10588865373`, SHA-256 `f1a81d5c9fdf7eb2ec9ef92f6d4f219a5cdd1a5abd981f392b6b5849577551c2`;
- artefato Windows: ID `10589015239`, SHA-256 `cf02455df2e9d23ca2d487a0c4c4c5b4136a22ad349b0985889dd8c834b1d330`;
- artefato macOS: ID `10589020214`, SHA-256 `c75fc36e1646f8eaa97b02b2d53171a9e92a7b5f4d3a23953ca1759dfd705568`.

Casos validados na tranche:

- `G(1,1)`;
- `G(2,2)`.

A implementação preserva contagens, 12 pentágonos, graus 5/6, incidência 2 por edge e 3 por vertex, reciprocidade, conectividade, Euler e IDs canônicos determinísticos.

Class I permanece suportada. Class III continua explicitamente não suportada.

A evidência ainda não demonstra geração Goldberg geral para todas as famílias nem refinamento estratégico/tático.

Reavaliar quando:

- Class III receber validação própria;
- o spike de refinamento hierárquico for concluído;
- o modelo de fileiras compartilhadas for congelado;
- uma família Goldberg oficialmente suportada for restringida ou ampliada.
