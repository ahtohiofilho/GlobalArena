# GA-ECON-REBASE-08 — Montagem física canônica de madeira e pedra

**Base homologada:** REBASE-07 / PR #255 / HEAD `45ef9e04de14c3fbca1cf9c427360c1aac4d4810`.
**Status:** incremento opt-in, PR Draft empilhada. `N` permanece desativado.

## Decisão de produto: o que já podemos chamar de executado

O motor físico canônico acumula madeira (commodity 3) e pedra (commodity 2). A REBASE-07 autorizou baixas individuais desses estoques, mas não formou nenhuma instalação ou bem. Não há receita produtiva acordada que diga quantos insumos rendem determinada mercadoria, quantos trabalhadores/turnos uma construção exige, ou qual produtividade uma obra concederia. Transformar saldos diretamente em fábrica, cidade ou mercadoria comercial seria inventar uma regra.

O REBASE-08 implementa **montagem física de um conjunto não comissionado** (`UncommissionedMaterialFrame`). É um objeto contábil físico produzido por uma ação explícita, que incorpora quantidades reais de madeira e pedra. Ele é reconstruído pelo journal e não gera renda, preço, coeficiente de produtividade, atividade automática ou mercadoria negociável. **Não é uma instalação operacional.**

## Ação executada, atomicidade e conservação

`CanonicalPhysicalTacticalSession.AssemblePrimitiveMaterialFrame(tile, kind, woodRaw, stoneRaw)` exige:
- um tile físico válido e habitado por trabalhadores;
- o único tipo de montagem suportado neste gate;
- **duas quantidades positivas declaradas** de madeira e pedra; não há receita fixa oculta ou transformação `x -> y` fictícia;
- estoque local efetivamente suficiente para cada material, sem teletransporte ou estoque invisível.

Um único `assemble_primitive` debita madeira e pedra simultaneamente no estado físico. A contabilidade FIFO é debitada **sobre clone**, com verificação integral de conservação antes de publicar o commit de `WorldState`. Se um dos insumos faltar, ambos permanecem intactos e nenhum evento é gerado. A prova `CanonicalExecutedPrimitiveAssembly` guarda: ordinal, tile, turno, tipo de montagem, quantidades incorporadas, hash anterior/posterior e atribuições FIFO de cada entrada. A montagem continua identificável ao longo de ciclos posteriores pelo histórico de ações.

A **identidade individual** do lote físico continua não autenticada, como já declarado nas REBASE-05/06. A atribuição de vintage é contabilidade de bens fungíveis, não prova físico-química de origem.

## Versão, hash e replay

Novos campos opcionais (ignorados se iguais a zero) em `CanonicalPhysicalAction`: `AssemblyKind`, `AssemblyWoodRaw` e `AssemblyStoneRaw`. Nenhum evento histórico muda sua representação JSON quando o recurso não é usado. O hash canônico autentica a mudança dos estoques físicos; o evento de montagem inclui a identidade/quantidade e é **reexecutado e comparado** com o journal no restore.

A versão do envelope evolui de forma estritamente condicional:

| Versão do envelope | Condição |
|---|---|
| 9–20 | Sem baixa material opt-in nem montagem: exatamente os saves históricos |
| 21 | Apenas eventos `material_use`, sem tesouro |
| 22 | `material_use` e tesouro dormente |
| 23 | Pelo menos uma montagem `assemble_primitive`, com ou sem `material_use`, sem tesouro |
| 24 | Montagem e tesouro dormente |

O `CanonicalPhysicalSavePayload.Version` interno **continua representando o ruleset** 9–17. O restore rejeita envelope discordante, ação não autorizada, quantidade adulterada, hash inválido e replay divergente. A lista de conjuntos executados é reconstituída do journal, não gravada como um patrimônio novo e não altera o estado de instalações do jogo.

## Fronteira de confiança e economia

Este incremento comprova a retirada física de dois insumos e o registro de sua incorporação em uma montagem material. **Não comprova**: tempo de trabalho construtivo, habilitação de uma instalação funcional, rendimento adicional, manufatura vendável, venda, preço, transporte entre tiles, posse comercial ou produto final economicamente valorado. Um comando futuro de comissionamento exigirá pré-requisitos físicos, regra de trabalhadores/tempo, identidade funcional da instalação, novo estado/hash versionado e teste de replay. Até lá, `HasProducedEconomicFinalValue=false` e `CanAuthorizeRealPurchasingPower=false`.

A montagem não pode contar simultaneamente como consumo intermediário e como nova produção final em `N`. Nenhuma carteira, crédito, estoque sintético ou receita monetária foi criado.

## QA e próximos gates

`GaVs117ExecutedPrimitiveAssemblyTests` cobre dupla baixa atômica, recusa por insuficiência/uso inválido/tile vazio, ausência de fabricação e de N, journal e hash, replay v23, combinação com `material_use`, v24 com tesouro antes/depois da ação, saves v9/v21 sem alteração, persistência histórica após novos ciclos e rejeição de payload/adaptação de envelope adulterados.

Homologar no CI: seis testes reais de kernel/hash em Linux, Windows e macOS Intel x64; CI FAST; benchmark; workflows elegíveis success/skipped. macOS ARM64 nativo continua pendente.

**Próxima decisão de produto:** definir um **tipo de instalação específica**, seus insumos físicos, execução temporal por trabalhadores e efeito funcional, antes de promover uma montagem não comissionada a instalação produtiva. Sem esses parâmetros, manter a transformação apenas material e auditável.
