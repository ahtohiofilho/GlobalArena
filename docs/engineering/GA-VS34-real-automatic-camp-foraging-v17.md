# GA-VS34 — Acampamentos automáticos reais e coleta física V17

Data: 31/10/2026. Base: GA-VS33/PR #138 (suprimentos físicos V16).

## Regras implementadas

1. O jogador escolhe no Godot **COMPRAR E VIAJAR COM COLETA AUTOMÁTICA (V17)**, distinto de compra simples V16 e de previsão não vinculante VS32. A comida é comprada e liquidada à vista através da mesma transação de partida V16.
2. Antes de comprometer a compra, a projeção VS32 demonstra se a estratégia de coleta primitiva pode sustentar a rota. Falha de estratégia não impõe bloqueio global ao destino: será possível replanejar com mais comida, outro fornecedor ou futuro modal.
3. A rota é mantida como uma única ordem. A cada início de trecho físico, se o saldo da carga não cobre os ciclos econômicos que ocorrerão antes de completar esse trecho, a expedição **espera automaticamente no waypoint**. Nunca para no meio de uma aresta, nem acampa porque é de uma era histórica.
4. Cada turno de espera aumenta em um o ETA e o prazo do trecho seguinte. Nenhum avanço de movimento é fabricado. A coleta acontece somente quando o calendário existente liquida um ciclo econômico. Durante esse ciclo os próprios viajantes consomem de sua carga, de modo que a coleta bruta deve exceder a alimentação da espera.
5. `PrimitiveForageFoodSource` da VS32, com coeficiente inicial **75% da produtividade agrícola local**, fornece comida obtida fisicamente pela expedição; ela entra em seu lote privado, **não** em `Stocks` do mercado local. A conta material verifica `comprado + coletado = consumido + remanescente`.
6. `PhysicalJourneyCampState` grava a espera acumulada, produção efetiva e ciclos dedicados à coleta. O calendário de cada trecho permanece com a velocidade V15/VS31, somada à espera quando necessária. A chegada remove os trabalhadores do trânsito mas preserva os lotes privados e os totais de acampamento.
7. A ação `depart_remote_camp` e o save/hash V17 têm domínio e replay próprios, sem alterar os hashes e saves de V9–V16. Ordens legadas V15 sem carga, V16 com carga mas sem acampamento e V17 com coleta podem coexistir.

## Limites e qualidade

- Sem infraestrutura de acampamento permanente nem cidades fundadas nos waypoints.
- Sem compras em paradas, renda futura, bancos, crédito, dívida ou criação monetária.
- Sem alterar a taxa base de movimento, contornos, visão tática, gerador de mundo, curva de mercado ou ritmo dos ciclos.
- CI Windows/Linux roda xUnit completo e compila Godot; testes V17 cobrem ausência de acampamentos quando não são necessários, escassez, coleta real por ciclo, ETA, save/replay, conservação e coexistência legada.

**Marco em draft empilhado sobre PR #138. Não integrar na main antes de todos os gates.**

## Resultado do diagnóstico Windows (31/10/2026)

O gate local GA-VS33/34 no commit `b35a74e3` compilou todos os projetos e o Godot (Release), mas apresentou **7 testes aprovados e 1 falha**. A falha foi no fixture `ProductiveCampDestination`: para a seed `20261012`, não havia rota de dois trechos que demonstrasse a coleta sustentável exigida pelo teste, sob a fonte geográfica default (75% da produtividade agrícola) e demanda real de 0,5 alimento/trabalhador/ciclo.

**Correção sem fabricação de alimentos:** o cenário agora testa o bloqueio atômico quando a fonte não sustenta a expedição, sem compra, sem movimento, sem alteração monetária e sem falsificar o save. A lógica de lançamento V17 continua proibindo partidas que o forecast demonstrou inviáveis. Um teste separado exercita a contabilidade de um acampamento produtivo com rendimento de fixture explícito: só cresce alimento em um ciclo econômico, débito físico ocorre no consumo, espera altera o calendário, a conservação material fecha e produção insuficiente é rejeitada.

**Gate remanescente para aprovação de gameplay positivo:** validar uma **rota realmente produtiva com a fonte default** em cenário geográfico compatível, ou especificar e homologar um novo provedor de coleta (com sua própria calibração determinística, identificado no save/replay). Nenhum dos dois deve ser simulado com comida gratuita; o teste unitário de contabilidade não substitui essa homologação ponta a ponta. A versão V17 segue em draft.
