# Global Arena — ADR-051: demanda do trabalhador no tempo e sub-blocos SPD

**Data:** 15/11/2026. **Base:** ADR-050 PR #339 NO-GO em 2.562 nós, 240 grupos e 120 mercados no orçamento de 248.000 coeficientes de blocos. **Escopo:** POC Python, sem merge, sem código C#/Godot.

## Trabalho, produção e consumo

Todo trabalhador gera demanda independentemente de estar produzindo, construindo, viajando, na atividade militar ou ocioso. Trabalhador produtor gera oferta e demanda no tile de produção. Durante viagens, demanda é distribuída pelos tiles da presença física proporcionalmente ao tempo em cada tile, sem aumento do consumo total.

Para trabalhador w, item k, atividade a, multiplicador M_w e coeficiente positivo de demanda d(a,k), com T turnos e t(w,n) permanência no nó n:

    D(w,n,k) = M_w * d(a,k) * t(w,n) / T
    soma_n t(w,n) = T
    soma_n D(w,n,k) = M_w * d(a,k)

O protótipo usa uma timeline de seis turnos e exige que os nós de turnos consecutivos sejam iguais ou vizinhos no grafo Goldberg. A regra de alocação de frações de turno sobre arestas, horários de chegada e interrupções ainda requer gate posterior. Coeficientes de demanda neste laboratório são pesos ilustrativos por atividade, NÃO calibração definitiva de itens.

Defina P como conjunto de NÓS físicos que produziram e D como conjunto de NÓS físicos que registraram demanda no ciclo. Então P é subconjunto de D e |P|<=|D|. Isso é distinto de grupo por item versus mercado (nó,item), entidades sintéticas do R13. Trabalhadores não produtivos podem criar novos nós demandantes sem produzir oferta.

## Fixture worker-coupled implementada

O módulo experiments/gate09/adr051_worker_block/ci/worker_coupled.py constrói 42 nós Goldberg e seis turnos. Possui 10 trabalhadores, seis produtores distribuídos em três nós, quatro não produtores (viagem, construção, militar e ócio) e quatro nós com demanda. Demanda positiva de DOIS itens de teste é gerada em cada nó com presença, totalizando oito mercados (nó,item). O viajante atravessa quatro tiles físicos, cada um recebendo sua fração do consumo; a demanda total é conservada. Os seis produtores criam seis grupos, e a oferta real é inicialmente dividida entre mercados compatíveis, sem produção adicional ou preço fictício. Essa distribuição é só semente para o solver, não equilíbrio garantido.

Esta fixture é laboratório, não motor autoritativo de trabalhadores do Global Arena. O catálogo de itens e pesos canônicos do jogo permanece intacto.

## Melhoria numérica proposta no laboratório

O ADR-050 atingiu o teto de 248 mil unidades cobradas dos blocos do pré-condicionador. A rotina ADR-047 monta apenas a metade triangular inferior da matriz simétrica e espelha os produtos internos, mas debitava largura ao quadrado em seu orçamento. O novo experimento cobra o número EXATO de produtos internos executados, w*(w+1)/2, e registra em separado a cobrança anterior w*w.

Além de corrigir a contabilidade, sub-blocos SPD de largura máxima 2 substituem os grandes blocos por produtor apenas no PRECONDICIONADOR. Cada sub-bloco é a submatriz principal EXATA da matriz normal J transposta J, obtida por incidências de arestas e mercados. A correção global LM/CG continua usando o operador Jv/Jtv EXATO, as mesmas equações Fischer–Burmeister, oferta física, rotas, tarifas e congestionamento quartico originais.

A implementação experimental faz substituição temporária das classes de trabalho/precondicionador do ADR-047 com restauração garantida por try/finally. Nenhuma fonte ADR-047 é alterada. Essa abordagem pode poupar coeficientes e fatoração Cholesky, mas pode também aumentar iterações CG; o ganho real deve ser medido, não presumido.

**Comparação honesta:** 248 mil contagens de produtos triangulares NÃO equivalem a 248 mil unidades de largura ao quadrado da medição ADR-050. O novo orçamento mede trabalho de modo diferente; comparações de desempenho considerarão também tempo Python, Krylov e iterações CG.

## Gates

Os 27 testes cobrem demanda total no ciclo, rateio por tile, não produtores, P subconjunto D, adjacência, ausência de teletransporte, mercado positivo, oferta integral, matriz principal JtJ exata, fatoração SPD, economia original e trabalho numérico contabilizado.

Perfil smoke: mundo worker-coupled 42 nós e um controle de dois produtores no núcleo antigo e novo. Perfil ci: também mede a fixture R13 2.562 nós, 240 grupos e 120 mercados. Usa os mesmos valores numéricos de limites do ADR-050: H 980, scans 6500, certificados 6, colunas 1600, gerações 6, Newton 185, Krylov 58000, CG por direção 180, produtos dos blocos 248000, com unidade do último limite explicitamente alterada para produtos triangulares.

**PASS econômico somente após ADR-042 independente:** P=sqrt(D/Q), custo físico c_free*(1+(B/K)^4), tarifas originais por vendedor, todas as rotas separadas, R13/R14, F084F, venda integral, GAP global e pior grupo <=1e-4. NO-GO gera relatório mas não publica fluxo final. CI verde dos testes de integridade não equivale a certificado comercial do mundo 240/120.

F059 financeiro continua não ratificado. Nenhuma fonte R13/R20–R50, C#/Godot ou main alterada. O ciclo econômico cooperativo, a reserva efetiva de CPU e a aplicação da demanda temporal no motor ainda não estão implementados. PR Draft sobre #339, sem merge.



---

## Homologação ADR-051 e proteção de regressão — 15/11/2026

**[GitHub Actions #43712915937](https://github.com/ahtohiofilho/GlobalArena/actions/runs/43712915937): SUCCESS** Ubuntu/Windows, 23/23 testes antes da promoção formal das novas verificações de regressão, e dois cenários com PASS do certificador econômico original ADR-042/F072 em cada sistema.

| Cenário sintético R13 2.562 nós / 240 grupos / 120 mercados | Windows | Ubuntu |
|---|---:|---:|
| GAP global F072 final | 3,99289941e-9 | 4,31117097e-9 |
| GAP pior grupo F072 final | 7,21790091e-7 | 6,25067617e-7 |
| Resultado original ADR042 | PASS | PASS |
| Produtos triangulares SPD realmente calculados | 152.634 | 153.468 |
| Cobrança quadrada antiga contrafactual | 365.667 | 367.929 |
| Produtos Krylov Jv/Jtv | 51.339 | 52.250 |
| Tempo Python | 184,91 s | 227,44 s |

**Fixture worker-coupled:** dez trabalhadores físicos, seis produzindo e quatro em atividades não produtivas; três nós com produção e quatro demandantes, dois itens de teste, itinerários válidos e distribuição temporal conservadora da demanda. O solver encontrou equilíbrio original ADR-042 com PASS em ~52 ms Windows e ~46 ms Ubuntu.

**Interpretação:** o NO-GO no cenário 240/120 do ADR-050 foi superado por um método diferente de precondicionamento, com contabilização mais fiel dos produtos SPD. Isso NÃO é aceleração comparável em orçamento igual, pois os contadores medem unidades diferentes; o método poderá demandar mais produtos Krylov. Tampouco é prova de convergência geral.

**Regressão agora obrigatória:** a partir da primeira homologação, se o cenário trabalhador-coupled ou o cenário R13 240/120 deixar de passar pelo certificado original F072, o runner retornará código de erro. As novas verificações aumentam a suíte para 27 testes, e o novo commit deve ter CI própria antes de ser declarado homologado.

**Limite de representatividade:** o mundo de 240 grupos ainda utiliza demanda sintética do R13, não a demanda gerada por trabalhadores; a fixture real worker-coupled ainda é pequena. Precisamos unir as duas escalas e definir rateio contínuo de tempo em arestas, persistência de ciclos, determinismo e orçamento real de CPU em C#. Nenhuma alteração do congestionamento quartico é necessária por estes resultados.

Artefatos iniciais: Windows ID 14161238220 e Ubuntu ID 14161073508. F059 permanece sem ratificação e o PR Draft não foi mergeado.
