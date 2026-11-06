# GA-CANON-DIRECTIVES-01 — Contrato de produto e bloqueio de regressões conceituais

**Registro:** 06/11/2026  
**Hierarquia:** decisões posteriores de produto prevalecem sobre mecanismos históricos, sem reinterpretar saves antigos.  
**Base de implementação:** GA-NUTR-INTEGRATION-01 / PR #266.  
**Política de release:** este incremento nao altera partidas, cadencia, mercado, UI, saves ou producao; distingue formalmente ALVO de COMPATIBILIDADE.

## 1. Fonte de verdade para os proximos incrementos

A discussao pode corrigir ou detalhar uma politica, mas nenhum valor provisoriamente implementado em legacy pode ser silenciosamente recanonizado. Alterar estas diretrizes exige:
1. alteracao explicita deste contrato com justificativa e decisao do usuario;
2. incrementos versionados e testes que demonstrem a nova politica no mundo real;
3. manutencao de suporte deterministico aos formatos historicos;
4. homologacao multipla de CI e QA visual quando pertinente.

O contrato **nao concede** autorizacao automatica para ativar regras congeladas. Distinguir decisao de PRINCIPIO, desenho de FORMULA e autorizacao de PROMOCAO.

## 2. Cadastro das diretrizes vinculantes

### GA-DIR-ECON-ABSTRACT — ALVO ECONOMICO, nao economia V12

- Populacao P = pessoas reais. As mesmas pessoas residem, consomem e podem produzir; nao criar consumidores ou trabalhadores ficticios.
- N = poder de compra **abstrato** relativo, proveniente de evidencias reais e defasadas. Na base da escala relativa o valor neutro e 1; o metodo definitivo de derivacao requer autenticacao. NAO criar carteira, saldo ou credito de consumidores.
- **Contrato aprovado para pressao de mercado:** `D=P*N`, `x=S/D`, `p=f(x)`, `R_ref=p*D`.
- D **nao e quota fisica** de alimento, venda, producao ou consumo; S pode exceder D. A escolha de f e a autenticacao de N ainda exigem calibracao. O valor R_ref nao e uma entrada de caixa certificada.
- Produzir primeiro; calcular N para o ciclo seguinte, sem retroalimentar receita nominal com preco e evitar dupla contabilizacao de produtividade.
- Receitas do Estado dependem de titularidade, direitos e origem fisica verificada; nao surgem por debito em caixa inexistente da populacao.
- Exemplos antigos de orcamento B=40, demanda fixa de alimentos/operario e saldos de microcreditos sao VERSOES HISTORICAS/EXPERIMENTOS e nao definicoes do alvo.
- **Implementado:** `GlobalArena.Economy/PopulationPriceKernel` isolado e regras de proveniencia/benchmark. **Nao promovido ao runtime do Godot:** este ainda opera `LocalCashSettlement` com `FoodCash`.

### GA-DIR-OFFER-ABSORPTION — DESTINO FISICO E RETENCAO

- Toda quantidade efetivamente ofertada deve ter destino economico; ganhos marginais decrescem conforme aumenta a oferta.
- Reter material em estoque so por politica deliberada/necessidade material; nao confundir estoque retido com mercadoria oferecida e nao absorvida.
- Transporte, custos e acesso afetam o mercado; nao criar teto de consumo para corrigir curva de precos.
- Coeficientes e mecanismo final de execucao ainda nao homologados; nao criar `min(oferta,D)` como resolucao.

### GA-DIR-CADENCE-MOBILITY — DIRECAO POSTERIOR, FORMULA AINDA NAO PROMOVIDA

- A proposta antiga de `0–30:1/1, 31–60:1/2, 61–90:1/3` foi **posteriormente revista**. Ela permanece `AdaptiveThirty` SOMENTE por compatibilidade/homologacao da politica existente e **nao** deve ser apresentada como a diretriz final do projeto.
- O desenho posterior (discutido em 31/10) vincula a cadencia ECONOMICA a mobilidade efetiva/desenvolvimento tecnologico, e nao a faixas cronologicas de 30 turnos. Exemplo de engenharia da discussao: sem cavalaria, unidade a pe 1 tile por turno e um ciclo/turno; depois da mobilidade com cavalo, cavaleiro 1 tile/turno, unidade a pe 0,5 tile/turno e um ciclo a cada dois turnos.
- Essa nova politica foi **congelada para analise**, sem formula universal, limiares ou adaptacao de saves aprovados. `GA-DIR-CADENCE-MOBILITY` e o ALVO DE DESENHO que substitui a prioridade do modelo cronologico, mas nao autoriza ativa-lo silenciosamente.
- Implementacao futura requer politica versionada de mobilidade, regra de arredondamento, cronograma global de ciclos, passagem tecnologica e replays; tempos de chegada e gasto alimentar devem continuar sincronizados.

### GA-DIR-MOVEMENT-SCALAR — CONTRATO DE PROGRESSO FISICO FUTURO

- Pontos inteiros de movimento por turno; cada aresta tem custo fisico (distancia, declividade, terreno, infraestrutura/modais quando existirem). Sem potencias de dois obrigatorias.
- Progresso deve atravessar varias arestas no mesmo turno quando houver pontos; nao acumular pontos infinitamente em repouso. Chegada e prontidao/efetividade de combate sao dominios separados.
- `ScalarMovementPointForecast` e preflight experimental. `PrimitiveFixedLegMovementTimeline` com dois turnos por aresta continua regra HISTORICA V13–V30, nao a regra futura.
- Um comando unico deve cuidar da rota e da logistica sem exigir micromanagement.

### GA-DIR-NUTR-SIX — ALIMENTO REAL, NAO INTERPRETACAO DE MERCADORIAS

- Seis identidades alimentares reais, com consumos fisicos atestados, escalas comuns explicitamente calibradas e populacao real. A lista de IDs NAO pode incluir por acidente `CommodityId(2)` (pedra) e `CommodityId(3)` (madeira) da economia fisica atual.
- Nunca fracionar o `CommodityId(1)` legado em seis tipos ficticios. Ausencia de ledger nao certifica seis zeros.
- `FoodDemandRawPerWorker=500_000` e `% atendido` no HUD nao sao qualidade nutricional. Nao ativar A(f), Q, ganho produtivo, ou bonus de diversidade antes de evidencia e calibracao; qualquer efeito so no ciclo posterior, apos aprovacao.
- `GA-NUTR-ARCH-01` e `GA-NUTR-INTEGRATION-01` ja aprovados tecnicamente sao CANDIDATOS/PROJECOES e nao avaliam qualidade real.

### GA-DIR-PHYSICAL-CIV — CONTINUIDADE

- Neolitico: trabalho com pessoas reais, rudimentar, sem obrigacao de fundar cidade ou edificar estrutura; facilidades primitivas posteriores exigem materiais e trabalhadores reais.
- Terreno esferico e tatico; biomas discretos classificam possibilidades, fisicalidade continua calcula custos/produtividade; nao teletransportar recursos.
- No inicio, os itens essenciais sao alimento, madeira e pedra; cobre, estanho, ferro e outras manchas reais entram por milestones futuros, sem criacao indiscriminada de tipos para acelerar numeracao.
- Caminhos, estrada, rodovia, ferrovias e rotas maritimas devem evoluir com direitos, custos e infraestrutura reais. Evitar microgerenciamento.
- Manter hash, journal, save e replay deterministas; nenhum incremento experimental promove automaticamente uma feature.

## 3. Gate executavel

`GaCanonicalProductDirectionAudit.Inspect(session)` gera um diagnostico somente de leitura para as cinco politicas atualmente sob risco direto de drift:
- `GA-DIR-CADENCE-MOBILITY`: modo atual fixo 1/6 ou `AdaptiveThirty`, ambos HISTORICOS;
- `GA-DIR-MOVEMENT-SCALAR`: `TimedAdjacent` e `LegacyInstant`, ambos HISTORICOS;
- `GA-DIR-ECON-ABSTRACT`: `LocalCashSettlement` e `LegacyDirectConsumption` nao cumprem o alvo;
- `GA-DIR-OFFER-ABSORPTION`: necessidade de execucao de destino sem quota;
- `GA-DIR-NUTR-SIX`: ledger real por alimento ausente; qualidade indisponivel.

`RequireTargetCanonical()` FALHA quando qualquer diretriz nao pode autorizar promocao. Nao chama essa guarda no inicio de uma partida V9–V30 para impedir jogos/replays existentes; novos features que alegarem estar no alvo podem consultá-la **antes** de autorizar essa alegacao.

`GaCanonDirectives01PolicyAuditTests` verifica deteccao de modos ativos, nao promocao silenciosa, contrato economico matematico independente dos wallets, inalteracao de hash/save e replay deterministico.

## 4. Sequenciamento de produto

1. Confirmar e preservar este contrato de diretrizes como requisito transversal nos PRs posteriores.
2. Migrar por regras NOVAS/versionadas a economia real do Godot de `LocalCashSettlement` para `PopulationPriceKernel` + evidencias de N, sem excluir save/replay legado e sem criar limite artificial de volume.
3. Definir e implementar o ledger fisico de seis alimentos (identidades e consumo executado), transporte local e de expedicao; somente entao produzir nutricão real.
4. Implementar a movimentacao escalar e, em MARCO SEPARADO, homologar a formula de cadencia por mobilidade com testes cronologicos e salvamento versionado.
5. Depois de calibracao e observacao, introduzir efeitos nutricionais defasados, receitas e direitos institucionais autorizados.

**Proibicao operacional:** nao chamar o modo corrente de `AdaptiveThirty` de politica final do projeto; nao chamar microcreditos do modo V12 de poder de compra abstrato definitivo; nao inferir nutritividade de `% atendido`.

## 5. Historico e referencias

- Discussao de 29–30/10 e `GA-VS11-adaptive-economic-cadence.md`: politica inicial implementada e preservada para saves.
- Discussao de 31/10 e `GA-VS35-scalar-movement-point-forecast.md`: revisao da cadencia e futuro movimento.
- Revisoes de 01/11, 02/11 e 04/11: consumo sem teto numerico, poder de compra abstrato, formula `D=P*N`.
- `GA-ECON-REBASE-01-clean-population-core.md` e `GA-ECON-REBASE-02-lagged-real-purchasing-power.md`: formulas aprovadas e limites de implementacao.
- `GA-NUTR-ARCH-01-candidate-nutrition-architecture.md` e `GA-NUTR-INTEGRATION-01-executed-consumption-evidence.md`: fronteira nutricional.
