# GA-ECON-REBASE-04 — Vínculo com ciclo econômico canônico concluído

**Base empilhada:** REBASE-03 / PR #251, HEAD `2ea919cad2def4ab053981f124807f742f3b463f`.
**Estado:** captura real do fechamento do ciclo e checagens agregadas; **sem autenticação completa de produção final por mercadoria**.

## Constatação na implementação atual

`CanonicalPhysicalTacticalSession.AdvanceMovementTurn()` produz e consome bens de verdade, vinculando cada tick a um hash do `WorldState`. O ciclo fechado registra `ProducedRaw`, `FoodConsumedRaw`, `FoodUnmetRaw` e o turno de fechamento, mas não disponibiliza no journal recibos por origem, commodity, lote e destino. `PhysicalTacticalEconomyRuntimeState.Stocks` é um estoque agregado que pode atravessar ciclos; consumo do ciclo não prova origem de produção no mesmo ciclo. VS82/VS95 continuam simulações de alocação e não comprovantes executados.

O REBASE-04 não deduz o destino final de dados inexistentes. Em vez disso, cria uma fronteira auditável entre o ciclo **genuinamente executado** e as alegações físicas que ainda não possuem autenticação suficiente.

## Novo contrato

`CanonicalCompletedCycleEvidenceBridge.Capture(session)` exige:
- último evento `tick` sem payload de realocação, associado ao hash e revisão correntes;
- último ciclo econômico fechado exatamente no turno atual;
- população física positiva (`State.TotalWorkers`, incluindo viajantes); ciclo ordinal baseado em `State.Cycles.Count`, **não** no número do turno;
- registro imutável do hash de fechamento, produção bruta, consumo e necessidade alimentar não atendida.

A evidência é **restrita à mesma sessão**, ao mesmo hash, ao mesmo número de eventos/ciclos, à mesma revisão da força de trabalho e à mesma revisão da infraestrutura de transporte. `VerifyIndependent` relê o estado canônico.

`ReviewCandidate` exige que o pré-validador do REBASE-03 pertença ao **mesmo ciclo ordinal**, tenha a mesma população e que **a soma bruta de todos os destinos, inclusive retenção e insumo**, corresponda exatamente à produção real agregada daquele ciclo. Essa igualdade é apenas **necessária**, jamais suficiente: não comprova mercadoria, produtor, data de extração, destino final nem separa uso de estoques anteriores.

`ReviewExecutedLocalFoodUse` recebe o testemunho canônico de compras locais de alimento já executadas (VS87), verifica hash, ciclo, sessão e quantidade entregue, e distingue a parcela documentada das outras formas de consumo. A quantia paga e o preço nominal **não são usados** para calcular N, nem o recibo demonstra que o alimento foi recém-produzido. O modo legado sem recibo não gera recibo sintético.

A API expõe flags negativas permanentes para `CanAuthorizePurchasingPower`, autenticação por origem, autenticação de destino final, execução econômica e persistência. Não oferece conversão automática de pré-validação em `RealCompletedCycleObservation`.

## Dependência e compatibilidade

O projeto `GlobalArena.Simulation` passa a referenciar `GlobalArena.Economy` (biblioteca independente) exclusivamente para ler o contrato do pré-validador. Sem mudanças em `AdvanceMovementTurn`, `WorldState`, serializer, Godot, valores de produção, preço, `D=P*N`, saldo ou imposto.

## Critérios de liberação da etapa posterior

Para autorizar `N` no runtime, falta registrar e validar a proveniência canônica por origem, mercadoria, destino, transporte e eventual estoque de período anterior; atestar preços-base congelados; definir a política de novos produtos/rebase e de ciclos com produção final zero; versionar a migração e garantir replay/save determinístico.

É proibido substituir essas provas pelo cotejamento de um único agregado `ProducedRaw`.

## Qualidade

`GaVs113CanonicalCompletedCycleEvidenceBridgeTests` cobre ciclo não fechado, tick substituído por realocação, hash e total físico, igualdade agregada insuficiente para autorizar N, ciclos/populações/quantidades estranhos, invalidação após ação, isolamento entre sessões, replay de save e recibos alimentares reais sem inferir nova produção.

**Governança:** PR Draft empilhada sobre #251; ARM64 segue pendente; macOS Intel x64 é contingência de CI.
