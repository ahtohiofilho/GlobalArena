# GA-NUTR-INTEGRATION-01 - Ponte de consumo fisico executado para nutricao

Data: 2026-11-06
Base: GA-NUTR-ARCH-01, PR #265, SHA 60950c8a2cd0226db7766a8f0e812f62e7923026.
Status: diagnostico canonico somente de leitura; sem avaliacao nutricional real nem efeito produtivo.

## 1. Objetivo

Integrar o dominio nutricional ao consumo fisico real de um ciclo economico encerrado. Reutilizar as duas evidencias ja homologadas:
- CanonicalExecutedPhysicalCycleProvenance: usos finais autenticados pelo evento de fechamento, com tile, commodity, canal e quantidade realmente consumida.
- CanonicalPhysicalLotCycleWitness: comprovacao independente da conservacao contabil e das atribuicoes FIFO do consumo. Origem FIFO nao significa identidade atomica comprovada dos lotes.

Nao empregar demanda economica, estoque disponivel, oferta, preco, dinheiro ou compras nao consumidas como substitutos de ingestao.

## 2. Implementacao

CanonicalNutritionExecutedConsumptionProjection.FromCurrentExecutedCycle(session) extrai exclusivamente o ultimo ciclo efetivamente fechado do estado canonico ainda atual.

Reconciliacoes obrigatorias:
1. A integridade do evento e hash do fechamento e verificada pelos recibos originais; recibos obsoletos ou de outra sessao sao rejeitados.
2. Consumo real total por commodity, tile, canal e ID de jornada equivale exatamente ao total atestado nos lotes, com acumulacao BigInteger sem overflow.
3. Soma local + expedicao equivale exatamente a PhysicalEconomicCycle.FoodConsumedRaw.
4. TotalRealPopulation vem do numero de pessoas reais no estado, nao do indice de demanda. Nao se presume distribuicao de consumo entre individuos.
5. A projeçao nao altera producao, estoque, eventos, mercado, poder de compra abstrato, estado, save, hash ou HUD.

TryCaptureCurrent fornece falso antes do primeiro ciclo, em turno sem fechamento ou apos outro comando que invalide a evidencia. Nao reutiliza silenciosamente um recibo antigo. Inconsistencias de evidencias no metodo estrito sao erros, nao zeros substitutos.

## 3. Limite de conhecimento

O atual CanonicalExecutedPhysicalCycleProvenance valida consumo alimentar apenas com CommodityId=1, categoria agregada. Esse codigo NAO atesta os seis alimentos canonicamente propostos. Nao ha coeficientes de conversao fisiologica aprovados nem atribuicao por habitante.

Por isso a projecao registra sempre:
- CanComputeNutritionalQuality = false
- NutritionalQuality = null
- HasPerPersonFoodVarietyConsumption = false
- HasCalibratedNutritionalConversions = false
- CanAuthorizeProductivityEffect = false
- CanAuthorizeRealPurchasingPower = false

O status distingue ausência de alimento consumido, unica mercadoria agregada e contabilidade incompleta por variedades. Essa evidência permite auditar consumo sem fabricar qualidade nutricional. O antigo indicador de atendimento quantitativo (ex.: 88,7%) permanece uma referencia economica provisoria e nao deve aparecer como avaliacao nutricional.

## 4. Gates

GaNutrIntegration01ExecutedConsumptionTests:
- ausencia de dados antes de ciclo executado;
- conservacao exata do consumo local e evidencias FIFO;
- mercado com liquidacao em dinheiro sem converter demanda em ingestao;
- replay historico com igualdade de consumo, hash e save;
- nenhuma mutacao por leituras repetidas;
- rejeicao de recibos velhos apos turno e realocacao;
- jornada provida de comida (consumo de expedicao separado do consumo local);
- demonstracao de que hipoteses de seis alimentos nao transformam provas de consumo agregado em provas de diversidade.

## 5. Proximo incremento

GA-NUTR-VARIETY-LEDGER-01 deve implantar fonte fisica de consumo por tipo alimentar (six-category ledger) no ciclo e nas jornadas, com invariantes de estoque, historico, identidade, residencia/carga e tratamento de zeros certificados. Apenas quando essa base existir podera ser calibrada a conversao nutricional e oferecida uma avaliacao real. A aplicacao de Q sobre produtividade deve ser decisao separada para o ciclo seguinte, apos calibracao.

Nao presumir massa, calorias, qualidade ou equivalencia de alimentos sem parametros explicitamente aprovados. Sem bonus artificiais por era.
