# GA-VS95 — contrato de arbitragem física local/remota (read-only)

**Issue:** #225. **Baseline homologada:** VS94 PR #224, SHA `34efdc8fa528d29605e2bc1bc5c62bd6684d5632`, 60/60 workflows. **Publicação:** PR Draft empilhada em VS94. `main` intocada.

## Objetivo

VS94 estabeleceu o limite fisicamente realizável derivado de trabalhadores ativos e de capacidades locais, certificado pelo witness VS93. A VS95 exige que a **mesma fonte física não seja alocada simultaneamente ao uso local e à exportação**. Um lote remoto não é comércio efetivamente liquidado; ele depende de oferta autenticada do serviço/rota/rede, tal como já demonstrado pela VS81.

## Modelo físico

`CanonicalPrimitivePhysicalDispositionTrial.FromCertifiedPlans(session, sites, physical94, currentFreight, localPolicies, remoteLots, congestion)` recebe:
- Uma sessão/ciclo ativo, descoberta de sites do mundo vivo e lote VS94 referente aos mesmos produtos físicos.
- Uma política macroscópica `LocalUsePermille` (0..1000) para cada atividade primitiva: agricultura, silvicultura, mineração. Não há microgerenciamento de pessoas ou criação de trabalhadores.
- De zero a 12 lotes de transporte com ofertas de **serviços realmente autorizados** na VS81; valida-se direito, rota, corredor e custo de frete no preflight existente, sem promessa de capacidade rígida de veículos.
- Demanda basal apenas como pressão. Não é teto de consumo, nem receita, nem preço pago.

Para cada fonte/mercadoria/atividade, calcula-se com inteiros e `BigInteger`:

```text
Feasible = VS94.ExperimentallyFeasibleRaw
LocalTrial = floor(Feasible * LocalUsePermille / 1000)
RemoteTrial = SUM(lotes autorizados com a MESMA origem/mercadoria/atividade)
0 <= LocalTrial + RemoteTrial <= Feasible
TotalNotExtracted = PotentialRaw - (LocalTrial + RemoteTrial)
InfeasibleNotExtracted = PotentialRaw - Feasible
RemainingFeasibleNotExtracted = Feasible - (LocalTrial + RemoteTrial)
PotentialRaw = LocalTrial + RemoteTrial + TotalNotExtracted
TotalNotExtracted = InfeasibleNotExtracted + RemainingFeasibleNotExtracted
```

Há prova adicional de conservação em agregados por produto e no par origem–destino, preservando a identidade das mercadorias. O planejamento local é co-localizado no produtor; o envio remoto verifica origem e destino por serviço, comprador habitado e a pressão de demanda atual. Frete é **custo de um cenário** (não pagamento feito). Não há saldo automático de produto não vendido: toda quantidade que não tiver destino fica **não extraída**. Nada é levado para turnos futuros.

O preflight é recusado quando:
- Simulações locais e remotas ultrapassam o mesmo limite VS94.
- Serviços de transporte são falsos, duplicados, de outra sessão, de outra revisão de rede ou com lotes nulos.
- As políticas locais são inválidas/duplicadas ou a matriz de sites e fluxos não coincide com a prova VS94 ou a referência VS81.
- A sessão, turno, mundo ou infraestrutura mudam depois da captura; `VerifyIndependent` reconstrói todos os saldos e o audit de frete.

## Sem autorização financeira ou alteração do motor

A VS95 **não realiza extração nem entrega reais**, não distribui dinheiro, não executa compras, não calcula preços monetários de madeira/pedra, não converte índices VS78 em receita, não implementa uma carteira B=40, não estabelece crédito, estoques involuntários ou `SETTLE`. O witness de alimento realmente comprado permanece separado na saída do VS94, apenas para comparação de proveniência.

A decisão canônica `poder de compra abstrato` permanece congelada para a implementação apropriada. O usuário não deve selecionar origem por origem; o preflight aplica políticas macroscópicas e lote de frete autenticado em lote.

## QA e rollout

- `GaVs95PhysicalDispositionTrialTests`: conservação local+remoto+não extraído por origem e produto, dupla alocação adversarial, taxas zero/1000, serviço falsificado/duplicado, permutação e replay, sessão estrangeira, rede/turno obsoletos, saves inalterados.
- `GaVs95PhysicalDispositionBenchmarkTests`: 8/32/128 repetições após fixture real aquecida; confirma os mesmos saldos e não monitora falsas metas de performance, porque runners variam de velocidade.
- Workflow Linux: regressões VS40..95, benchmark, Godot Mono Release. Windows: suíte completa, benchmark, Godot Mono Debug. Confere checkout exato do SHA com falha explícita se sintético. O teste macOS do kernel preexistente continua separado.
- **61 workflows esperados**; homologação exige 61/61 completed/success no mesmo HEAD, sem confundir fila/status com testes executados. PR Draft, sem merge em main.

## Próximo marco de produto

Com a arbitragem física demonstrada, o próximo passo é definir a política de absorção e distribuição simultânea e o fechamento econômico abstrato, separando comandos do usuário (alocar trabalhadores, mover unidades) das escolhas automáticas do mercado. Jamais tratar esta prévia como economia executada.
