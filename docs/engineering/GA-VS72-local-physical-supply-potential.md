# GA-VS72 — Potencial físico de provisão local por comprador e mercadoria

**Baseline:** VS71, PR #178, head homologado 27ff6bf7835d6f40561fb287cbb2ebcfa50f414a, **37/37 workflows completed/success**. VS72 está empilhada sobre VS71, em Draft, sem merge na main.

## Decisão e semântica

A VS71 apresenta duas hipóteses de poder de compra abstrato e a cobertura de frete comercial planejado. Nenhum desses indicadores representa consumo real. A VS72 adiciona o terceiro eixo indispensável antes de avançar para atendimento econômico: produção física do próprio povoamento no mesmo tile e para a mesma mercadoria.

Para cada comprador/mercadoria com demanda atual positiva:

- **Produção local física por ciclo:** soma somente fluxos de oferta do mesmo tile e **mesmo CommodityId**. Não agrega quantidades heterogêneas de alimentos, madeira e pedra.
- **Teto hipotético de uso local:** min(produção local, demanda física local). Trata-se de uma possibilidade de alocação, não da alocação realizada.
- **Fração máxima de cobertura local:** teto hipotético / demanda física, uma razão adimensional exata.
- **Frete planejado de VS71, baseline e regional:** duas colunas independentes de contexto; não se adicionam ao indicador de provisão local.

**Crítico:** uma unidade de produção pode ser pretendida simultaneamente para provisão local e para remessa planejada a outro tile. Somar o teto local ao frete sem uma arbitragem simultânea sobre a mesma oferta geraria dupla contagem. A VS72 deliberadamente não informa uma porcentagem de consumo total ou de bem-estar e não calcula demanda atendida somando essas parcelas. Também não presume autoconsumo, estoque, transporte gratuito, remuneração ou produção realizada além do fluxo físico observado.

A hipótese regional continua calibrada por produção normalizada por residente conforme VS70, conservando a parcela H protegida por povoamento. Residentes e trabalhadores são a mesma entidade lógica. Nenhuma cidade formal é criada; o identificador segue o tile tático físico.

## Contrato técnico

CanonicalPrimitiveRegionalAccessDistribution.DiagnoseLocalSupplyPotential() produz um recibo CanonicalPrimitiveLocalSupplyPotentialDiagnostics, que inclui:

- Snapshot.Buyers: todos os compradores habitados e todas as mercadorias demandadas;
- PhysicalLocalProductionRaw, MaximumHypotheticalLocalUseRaw, MaximumLocalCoverage, BaselinePlannedFreightRaw e RegionalPlannedFreightRaw;
- média adimensional de cobertura máxima local entre pares comprador/mercadoria e contagens de ausência de produção local e potencial integral;
- RegionalFreightAccess: recibo original VS71, sem recalcular frete como consumo.

A construção não aceita um novo inventário de povoamentos fornecido pelo chamador: reutiliza a descoberta física interna à mesma VS71. VerifyIndependent() valida primeiro os recibos VS71–65, depois reconstrói todas as linhas físicas e agregados com racionais exatas, confronta identidades, demanda e produção. RequireCurrent(session) rejeita outra sessão, save restaurado, mudança da rede/estado físico ou recibos obsoletos.

CanAuthorizeRealSettlement=false, IsCommitted=false e IsActualConsumption=false. Nenhum pagamento, renda futura, crédito, obrigação financeira, estoque, roteamento alternativo ou SETTLE foi autorizado.

## QA e performance

A suíte VS72 testa: (1) reconciliação integral de produção local e demanda, por produto e comprador; (2) frete inacessível em termos de preço sem redução artificial da produção local potencial; (3) estradas e compradores independentes sem importar frete para o tile local; (4) determinismo sob reordenação das ofertas; (5) invalidação após restore e mudança de rede.

Benchmark --vs72-local-supply: repete 8/32/128 consultas sobre um cenário físico pequeno e fixo, aquecimento e três amostras. Mede diagnóstico, auditoria e suporte de VS71, não 128 cidades nem performance de mapa massivo.

Gate GitHub Actions no Linux e no Windows: regressões, benchmark e compilação Godot Mono. Só declarar a VS72 homologada quando a matriz completa de workflows do SHA final estiver em completed/success. Até lá a PR permanece em Draft.

## Próxima dependência conceitual

Antes do consumo efetivo, será necessária uma única seleção simultânea de uso da oferta física por origem/mercadoria/ciclo, evitando que o mesmo recurso seja alocado localmente e exportado, e respeitando orçamento H, direitos institucionais, rede, concorrência por corredores e limites físicos. A VS72 não implementa essa seleção.
