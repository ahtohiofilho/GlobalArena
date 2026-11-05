# GA-ECON-REBASE-07 — Uso físico canônico de madeira e pedra

**Base:** REBASE-06, PR #254 homologado, HEAD `9648136b6f084197a2dfbd028edc17902ea3e365`.
**Entrega:** operação explícita de baixa física de material em um tile habitado, com classificação de finalidade, prova de hash/journal e replay em saves versionados.

## Lacuna anterior

O ciclo legado produz madeira (commodity 3) e pedra (commodity 2) e os mantém no estoque, mas não executa seu uso final ou utilização como insumo. O REBASE-06 implementou conservação dos lotes fungíveis com atribuição contábil FIFO, sem evidência direta do uso desses materiais. Assim, não seria correto inventar que a simples produção ou a disponibilidade de estoque equivalessem a consumo final ou transformação industrial.

## Ação real opt-in: UsePhysicalMaterial

`CanonicalPhysicalTacticalSession.UsePhysicalMaterial(tile, commodity, kind, raw)` exige:

- tile físico válido com trabalhadores residentes;
- mercadoria 2 (pedra) ou 3 (madeira);
- quantidade bruta estritamente positiva e não maior que a quantidade realmente estocada no tile;
- classificação `FinalPhysicalUse` ou `IntermediateInputUse`, ambas explicitamente declaradas pelo comando do jogador/serviço, sem interpretação a partir do saldo.

No mesmo commit de `WorldState`, o estoque é **debitado de verdade**. A camada de lotes FIFO consome a mesma quantidade em custódia local, verifica conservação contra o próximo snapshot **antes** do commit e retém a atribuição por ciclo e origem como contabilidade fungível — não como identidade autenticada de cada átomo de matéria.

O resultado `CanonicalExecutedMaterialDisposition` guarda ordinal da ação, turno, tile, commodity, classificação, quantidade, hash anterior/posterior e parcelas FIFO. A lista histórica é consultável em `ExecutedMaterialDispositions` e reconstruída na restauração pelo replay da operação, sem novos saldos monetários.

## Sem criação fictícia de patrimônio

- `FinalPhysicalUse` é um **sumidouro físico registrado**. Por si só, não cria edifício, patrimônio, bem público, construção ou preço. Uma obra produtiva exigirá ação e estado próprios.
- `IntermediateInputUse` registra **consumo de insumo**, mas não inventa produção transformada nem prova receita.
- A classificação é uma intenção explícita e registrada de destinação no momento da ação; a operação prova o débito físico, **não** a execução de uma construção/atividade industrial externa.
- Não há transferência entre tiles, transbordo ou modais de transporte novos. Tais processos exigirão validação de serviço, direitos, custo físico e preservação de custódia.

## Formato do journal/save e compatibilidade

O evento `CanonicalPhysicalAction` foi estendido com campos opcionais de valor padrão zero: `MaterialCommodity`, `MaterialUseKind`, `MaterialRaw`. Todos usam `JsonIgnore(WhenWritingDefault)`; portanto, sem o recurso novo, a serialização dos eventos históricos permanece igual.

A existência de ação `material_use` ativa um novo envelope, enquanto o `CanonicalPhysicalSavePayload.Version` interno mantém o ruleset original:

| Envelope | Uso |
|---|---|
| 9–20 | Saves históricos sem ação material; bytes preservados |
| 21 | Journal com material, sem reserva de tesouro |
| 22 | Journal com material e tesouro dormente integrado |

No restore, a combinação envelope/ação/tesouro é validada antes de replay. O comando é reexecutado e cada `CanonicalPhysicalAction`, hash e estado final são comparados ao que foi salvo. Os materiais gastos reduzem os saldos canônicos ao longo da história, e o registro das parcelas FIFO é reconstruído deterministicamente. Não é permitida aplicação silenciosa dessa operação em saves de formatos antigos.

## Governança e fronteira de N

A efetiva retirada de madeira/pedra e sua classificação local agora têm testemunho canônico. Contudo, o jogo ainda não vincula os materiais gastos a obras produtivas, produto transformado, infraestrutura instalada ou vendas comerciais. A identificação individual dos lotes continua sendo convenção FIFO para bens fungíveis. **Isso não é uma prova suficiente de produção final econômica para atualizar N**.

O índice `N` permanece desativado. Não usar o preço hipotético, carteira, novo orçamento, transferência monetária, quantidade escondida nem consumo intermediário como produção final adicional.

## Testes e critérios de homologação

`GaVs116ExecutedMaterialDispositionTests` cobre débito físico, distinção de classificação, quantidades e hashes do journal, sequência de comandos, recusas sem mutação, replay v21, replay v22 com tesouro ativado antes/depois, uso através de ciclos, checksum refeito com evento adulterado e tile não habitado. Gate: compilação/testes reais Linux, Windows, macOS Intel; hashes, CI FAST e benchmark. ARM64 nativo segue pendente.

**Estado:** PR Draft empilhada sobre #254; sem merge em `main`.
