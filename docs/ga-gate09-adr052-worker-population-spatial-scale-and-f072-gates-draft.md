# Global Arena — ADR-052: população física, demanda espacial e agregação econômica

**Data:** 15/11/2026. **Base:** ADR-051 PR #340 (10 trabalhadores, F072 original PASS; R13 240 grupos e 120 mercados PASS). **Escopo:** NOVO laboratório Python em PR Draft empilhado, sem merge e sem alterar kernel autoritativo C#/Godot, R13, F072, tarifas, F084F ou F059.

## Objetivo

O jogo real deve ter muitos milhares de nós, trabalhadores físicos que produzem quando alocados a essa atividade e demandam SEMPRE, inclusive em viagem. O mercado NÃO pode nascer de listas sintéticas independentes da população. O objetivo agora é demonstrar que podemos gerar uma população de verdade, calcular a demanda por tempo em cada tile, agregar trabalhadores numa interface econômica e quantificar o custo antes de escalar o solucionador.

## Regra individual inalterada

Para trabalhador w, item k, atividade a, multiplicador pessoal M_w, coeficiente POSITIVO de teste d(a,k), tempo de permanência t(w,n) no nó n e duração de ciclo T:

    demanda(w,n,k) = M_w * d(a,k) * t(w,n) / T
    soma_n t(w,n) = T
    soma_n demanda(w,n,k) = M_w * d(a,k)

Assim, a viagem desloca a demanda mas NÃO a multiplica. Todo trabalhador produtivo também demanda; não produtivos (construção, deslocamento, militar e ócio) geram demanda sem produção. No nível de NÓS físicos, P = nós que produziram, D = nós com demanda, e P é subconjunto de D. Um mercado F072 é um PAR (nó,item), não um trabalhador.

O protótipo reutiliza seis slots de turno de presença no tile, validando arestas reais do Goldberg. Não modela frações de turno em aresta, saldo em dinheiro, posse de estoques, demanda pós-chegada, sincronização de ciclos ou seleção de atividades do jogador. Coeficientes de demanda e oferta são somente dados de fixture, nunca calibração canônica do catálogo de itens do jogo.

## Três níveis de prova deliberadamente separados

| Perfil | Goldberg | Trabalhadores físicos | Nós produzindo | Nós demandantes | Grupos agregados (nó,item,vendedor) | Mercados (nó,item) | O que se certifica |
|---|---:|---:|---:|---:|---:|---:|---|
| Unit | 162 nós | 128 | 4 | 16 | 8 | 32 | Oferta/demanda e F072 independente obrigatório |
| Pilot econômico | **5.762 nós** | **2.048** | 12 | 48 | 24 | 96 | Oferta/demanda e tentativa LIMITADA de F072 independente |
| Stress espacial | **5.762 nós** | **8.192** | **512** | **2.048** | **1.024** | **4.096** | Demanda, conservação, rotas, agregação, preços iniciais; **NÃO resolver F072 aqui** |

O stress grande permite validar trabalhadores por MILHARES de nós físicos com demanda real e grupos agregados, mas **não prova solução do equilíbrio F072 para 4.096 mercados**. O piloto contém somente 48 nós com demanda, apesar de estar numa malha de 5.762 nós; não vender esse piloto como população uniformemente distribuída no mapa. O stress espacial representa 2.048/5.762 tiles demandantes, ainda não o mundo completo densamente habitado.

O gerador escolhe pequenos aglomerados disjuntos de quatro nós vizinhos de forma determinística sobre a malha Goldberg. Cada aglomerado possui trabalhadores produtores no centro e trabalhadores não produtores em rotas locais: viajante faz (centro,a,centro,b,centro,c) ao longo de seis turnos; construtor permanece em a; militar em b ou ocioso em c. Os mercados em cada item de cada nó são SOMENTE a soma da presença real. Nada é atribuído a compradores fictícios.

Cada trabalhador produtivo cria uma oferta real, agregada em um Group por combinação (nó,mercadoria,vendedor) usando soma com precisão; isso preserva a massa total de produção, mas **NÃO prova equivalência de decisões individuais** caso trabalhadores de um mesmo grupo tenham futuras políticas, impostos, tesouros e propósitos diferentes. Essas dimensões ainda exigirão definição de chave de agregação. O núcleo F072 utiliza a interface Group/Market original; nunca recebe um Market por trabalhador.

## Geografia, transporte e seed

O único elemento reutilizado do R13 original é a **topologia Goldberg com suas arestas, capacidades, titularidade e tarifas por vendedor**. As listas sintéticas R13 de mercados e produtores são DESCARTADAS, substituídas por trabalhadores físicos. A função de custo segue exatamente c_free*(1+(B/K)^4); preço original P=sqrt(D/Q) para Q>0. Todas as demandas são positivas por item e cada tile coberto recebe produção real na seed dos grupos locais (sem criação de oferta). A seed não é equilíbrio certificado, apenas condição inicial para auditoria/solver.

Há checks de hash do ledger de trabalhadores e do fluxo, população/rotas/tempo/mercados determinísticos, soma total de demanda por item, oferta por grupo, P ⊆ D e preços iniciais finitos, com contagem explícita de trabalhadores versus grupos.

## Gate comercial e limites numéricos

**Unit (obrigatório):** solver ADR-051 de sub-blocos de largura 2 sob orçamento H=320, scans=450, ADR042=5, colunas=160, gerações=4, Newton=95, produtos Krylov=9.500, CG/direção=95, coeficientes triangulares=24.000. Sem certificado ORIGINAL ADR042/F072 global e pior grupo <=1e-4, CI falha.

**Pilot 2.048 trabalhadores (exploratório no primeiro gate):** H=620, scans=1.600, certificados=5, colunas=440, gerações=5, Newton=145, Krylov=26.000, CG=150, blocos triangulares=150.000. Só se usa PASS quando o certificado original ADR042 comprova ambos os gaps F072 <=1e-4, caminhos globais, R13/R14, produção e F084F. Uma eventual falha é NO-GO comercial, nunca publicada como sucesso, mesmo que a CI da agregação passe.

**Stress 8.192 trabalhadores:** o F072 não é executado — a quantidade de mercados está muito acima das últimas amostras certificadas, e executar uma busca não orçada seria irresponsável. O gate é REAL de construção e contabilidade, não GO para escala de equilíbrio.

A CI poderá passar na fase exploratória do pilot mesmo se seu F072 receber NO-GO, mas isso deve ficar EXPLÍCITO no relatório. Se o piloto passar nas duas plataformas, pode ser promovido a regressão obrigatória em outro commit. Nunca equiparar o número de trabalhadores ao número de grupos numéricos.

## Orçamento do jogo e próximas escolhas

GitHub Actions mede tempos Python dos runners, NÃO a performance real do motor C# e NÃO a carga simultânea da UI, guerra, diplomacia ou logística. O solver ainda não pode interromper e retomar uma VI em meio a ciclo econômico com snapshot certificado. Antes de GO de produto são necessários: benchmark C# com reserva de CPU, população espacial mais representativa e mercados abrangentes, agrupamento econômico que preserve tesouro e autonomia local, passos de viagem subturno e replay cross-OS.

**Governança:** PR Draft stacked sobre #340. Sem merge nem código autoritativo. F059 financeiro segue não ratificado. Não alterar a fórmula quartica sem decisão de produto.



---

## Primeira homologação e promoção à regressão obrigatória — 15/11/2026

**[GitHub Actions #43715476618](https://github.com/ahtohiofilho/GlobalArena/actions/runs/43715476618): SUCCESS Windows e Ubuntu**, commit inicial \`8a7191925bbdd0b9225ab8a6b1f2cbb5e610ac36\`; 26/26 testes por plataforma e três experimentos com as classificações de escopo esperadas.

| Gate | Windows | Ubuntu |
|---|---:|---:|
| 162 nós, 128 trabalhadores, certificado original F072 | **PASS** | **PASS** |
| 5.762 nós, 2.048 trabalhadores, certificado original F072 | **PASS** | **PASS** |
| 5.762 nós, 8.192 trabalhadores, conservação/ledger espacial | **PASS** | **PASS** |
| 8.192 trabalhadores / 4.096 mercados, F072 comercial COMPLETO | **NÃO EXECUTADO** | **NÃO EXECUTADO** |
| GAP global final F072 piloto 2.048 | 5,163731446e-12 | 5,163731446e-12 |
| GAP pior grupo final F072 piloto 2.048 | 1,175502157e-11 | 1,175502157e-11 |
| Tempo piloto solver Python no GitHub Actions | 18,864 s | 18,546 s |
| Construção do ledger de 8.192 trabalhadores | 607,6 ms | 578,3 ms |

A população grande foi gerada a partir de trabalhadores efetivos de diferentes atividades, com trilhas legítimas, demanda temporal conservada, 512 nós produtores, 2.048 nós com demanda, 1.024 grupos agregados e 4.096 mercados. A execução de 8.192 trabalhadores confirma a infraestrutura de dados e oferta/demanda, MAS **não soluciona 4.096 mercados**. Não confundir uma semente de mercado positivo com equilíbrio.

**Evidências ZIP:** \`ga-gate09-adr052-windows-latest\` ID **14162590721**; \`ga-gate09-adr052-ubuntu-latest\` ID **14162895124**.

**Mudança de governança após homologação inicial:** como o piloto de 2.048 trabalhadores recebeu certificado original F072 em ambos os sistemas, o runner foi promovido a exigir tanto o certificado F072 da fixture unitária quanto o **certificado F072 do piloto** e o ledger físico de 8.192 trabalhadores. Caso qualquer um falhe, deve retornar código diferente de zero. A suíte cresceu de 26 para **30 testes**, incluindo cenários negativos de regressão. A CI dessa atualização precisa ser reexecutada para comprovar o bloqueio.

**Decisão técnica:** GO para o laboratório de demanda física em milhares de trabalhadores e para o equilíbrio do piloto concentrado de 2.048 trabalhadores/48 nós demandantes; não há GO econômico para 8.192 trabalhadores/4.096 mercados. O próximo gate deverá aumentar progressivamente o número de nós com demanda efetivamente equilibrada, medir variação de memória/CPU em C# e considerar estrutura esparsa ou ciclos cooperativos, sem relaxar F072 nem o congestionamento quartico. F059 ainda não ratificado.
