# GA-VS47 — SELECT/AUTHORIZE sobre propostas econômicas independentemente auditadas

**Data:** 31/10/2026. **Base empilhada:** GA-VS46 PR #152, head `c192c8f08859575e45d7478de75bf183edb729ab`, 12/12 workflows aprovados. **Status:** laboratório somente leitura, PR draft; `main` intacta.

## Objetivo e fronteira do contrato

Na GA-ADR-ECON-005, o pipeline é `SNAPSHOT → EXPLORE → AUDIT independente → SELECT/AUTHORIZE → SETTLE`. A VS46 implementou **AUDIT**, reproduzindo obrigações de compradores, produtores, operadores e corredores sem transferir um crédito real. A VS47 implementa **SELECT/AUTHORIZE informacional** sobre um conjunto finito de propostas previamente construídas: não executa `SETTLE`.

`ExperimentalCashAuthorizationSnapshot` identifica um **turno econômico**, um **hash de mundo fornecido por chamador confiável**, uma **revisão de infraestrutura** e todo o conjunto de ofertas, catálogo de massa, preços postados, saldos de caixa, reserva `H`, opções físicas e curva de congestionamento. O conteúdo recebe uma impressão digital SHA-256 canônica e ordenação determinística. Esta impressão é um **identificador de integridade, não uma assinatura nem autenticação da fonte**. Um emissor de estado real precisará validar e associar o hash de mundo/revisão de rede aos objetos canônicos V14/VS39 e ao conjunto de ofertas antes da ativação.

## Seleção explicitamente institucional

`ExperimentalAuditedCashCandidateAuthorization.Select` exige `ExperimentalAuditedSelectionPolicy(PolicyId, PolicyVersion, Rule)`, com nome e versão não vazios. Cada candidato deve ter identidade positiva única. O módulo **reaudita obrigatoriamente todos os candidatos** com a VS46 e **falha fechado se qualquer um apresentar** frete adulterado, volume físico impossível, caixa insuficiente, contas ou serviço não declarado. Seleção não toma o valor de `NetEconomicValue` alegado pelo candidato como pontuação confiável.

Duas políticas de laboratório, deliberadamente transparentes:

1. `MaxDeliveredRawVolumeThenLowestFreight` — maximiza quantidade agregada em unidades de fluxo em pontos fixos e, se empatar, minimiza frete financeiro auditado; último desempate por ID canônico do candidato. **Quantidades de bens diferentes não medem o mesmo valor econômico**: esta política é de demonstração institucional, não cálculo de bem-estar agregado. 
2. `LowestFreightAtEqualRawVolume` — aceita somente um lote de alternativas com **exatamente o mesmo volume agregado**; escolhe frete auditado mais baixo, depois ID. Volumes diferentes são rejeitados em vez de serem descartados silenciosamente.

As propostas são previamente geradas por `EXPLORE`. A melhor opção selecionada é a melhor **entre as alternativas fornecidas e integralmente auditadas**, não um ótimo global de mercado. Uma eventual seleção por bem-estar exigirá uma auditoria independente do **valor integrado da demanda e custo de reserva dos produtores**; a VS46 só certifica as obrigações financeiras e físicas, não esses valores.

## Autorização vinculada ao snapshot

`Authorize` exige `ExperimentalCashAuthorityDecision`: `ActorId`, `DecisionId`, decisão explicitamente **positiva**, turno, digests exatos de snapshot/proposta, candidato e ID/versão da política. Reconfere turno, hash de mundo e revisão da rede apresentados **na chamada de autorização**, recalcula digest do conjunto original e **executa novamente a auditoria VS46** sobre o candidato selecionado; mudanças na proposta após `Select` falham fechado.

O resultado `ExperimentalCashAuthorizationReceipt` é **recibo de laboratório, não credencial de débito**. `ActorId` e `DecisionId` são strings fornecidas externamente e **não autenticadas** pelo componente. O recibo é determinístico e pode ser reconstruído; a proteção contra reaproveitamento de decisões, assinaturas institucionais e permissão real de operadores financeiros pertencem ao futuro boundary de `SETTLE`. O hash do journal auditado vincula o recibo a snapshot/candidato e totais auditados, não garante prova criptográfica de execução.

## Invariantes

- Nenhum candidato inválido pode ser selecionado, mesmo quando houver um candidato válido no mesmo pacote.
- Não autorizar versão de política, candidato, digest, turno, mundo ou revisão de rede divergentes; nem decisão negativa ou agente sem identificação.
- Nenhum benefício econômico inventado por adulterar `NetEconomicValue`: a regra de ranking depende apenas de quantidades e preços recompostos na auditoria.
- Apenas lançamentos financeiros propostos pela VS46; **não** transferir fundos, criar contas permanentes, modificar preços, caixa V14, trabalhadores, bens, save, hash, turnos ou UI.
- Sem crédito, dívidas, bancos, receita futura, substituição arbitrária da reserva H ou transbordo implícito.
- Exige abastecimento de identidade/proveniência do mundo por uma autoridade externa confiável; não confiar na mera presença de um hash.
- Período de validade do recibo é o **snapshot atual fornecido ao gate**; um mecanismo real de assinatura, revogação ou idempotência só cabe no fechamento futuro e não está prometido por este PR.

## QA / observabilidade

Os testes específicos cobrem escolha de volume versus frete, empate determinístico, recusa de política de menor frete se volumes diferirem, rejeição de candidato fraudado, divergência de digests/turno/política/rede/mundo, reauditoria após mutação e compatibilidade de ordenação da entrada. CI Linux/Windows, xUnit integral Windows, Godot Mono Debug/Release, state hash multiplataforma.

Benchmark sintético:

```powershell
dotnet run --project GlobalArena.Benchmarks/GlobalArena.Benchmarks.csproj -c Release -- --vs47-audited-selection
```

Mede seleção de **8, 32 e 128 propostas previamente válidas**, com reauditoria individual, mais uma reauditoria final durante autorização. Registra mediana de 3 amostras após warmup e alocações, sem threshold de tempo ou extrapolação para uma economia completa.

## Próximo marco

A VS48, se aprovada, deverá estudar o `SETTLE` **atômico e idempotente** com contas de abertura autorizadas e provenance canônica, incluindo titularidade dos transportadores e conta pública, journal persistente contra replay, versionamento de save e garantia `CAS` no hash/revisão do mundo, sem usar recibos desta etapa como autorização automática de mutação.
