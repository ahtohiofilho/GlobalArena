# GA-VS86 — Sidecar de observações históricas de preços hipotéticos

**Issue:** #205. **Base:** VS85 PR #204 SHA \`1cfa52bf4f98273d6720fd7c2cc97e48969ce7f0\` (51/51 CI). **Decisão canônica:** GA-ADR-ECON-007 R2 #188.

## Decisão de arquitetura

O histórico de preços NÃO será confundido com o save canônico. O sistema de economia/food cash atual permanece intacto. A VS86 introduz um **arquivo JSON sidecar observacional** separado, sem comandos para ser usado como autoridade de produção ou liquidação, criado somente após validação de um par de ciclos físicos reais pela VS85.

Níveis de prova não equivalentes:

| Fato | Verificado pela VS86? | Escopo |
| --- | --- | --- |
| Checksum de integridade do JSON | Sim | SHA-256 sobre payload, **não** assinatura digital/autenticidade; adulterador pode recalcular SHA |
| Vínculo do registro ao mundo restaurado | Sim | Mesmo hash do estado final, registro físico de ciclos, ticks e hashes desde origem |
| Ciclo econômico anterior e exatamente próximo ciclo | Sim | \`State.Cycles\`, contagem de movimentos, journal, cadeia \`BeforeHashHex/AfterHashHex\` e ausência de reatribuição/revisão de rede |
| Preços foram efetivamente pagos no mundo | **Não** | Índices são apenas cotações hipotéticas da VS78/VS82, formadas na sessão anterior da VS84 |
| Cotação original é matematicamente válida | Parcialmente | \`p\` racional [0,1], peso local positivo, ganho permitido, resposta limitada consistente \`Δu=trunc((2p-1)*maxStep)\` |
| Cotação original pode ser reconstruída do save canônico | **Não** | O save não guarda todas as hipóteses de política e cenário, e o sidecar não autentica economicamente esses números |
| Import pode ajustar produção ou executar \`SETTLE\` | **Não** | Apenas \`Inspect\` retorna um relatório sem métodos de operação |

## API e formato

- \`CanonicalEconomicPriceHistorySidecar.Export(session, completedProofVS85)\`: exige \`completedProof.VerifyIndependent()\` na sessão corrente, com ciclo anterior VS85 comprovado, sinais/políticas capturados. O sidecar não injeta dados no \`WorldState\`, no \`Game\` ou no \`SaveFormat\`.
- \`CanonicalEconomicPriceHistorySidecar.Inspect(canonicalSession, sidecarJson)\`: aceita a mesma sessão ou a sessão **restaurada do save canônico do ciclo alvo**, mas somente se cronologia bater. Inspeciona de forma independente o diário físico completo, source/target action-count e hashes, origem e destino do \`State.Cycles\`, revisões de trabalhadores/rede no destino, matemática de taxas, ordenação/identidade de atividades primitivas, integridade e limite de tamanho.
- \`CanonicalEconomicPriceHistoryInspection\`: campos apenas de exibição, indicadores explícitos que cotação não é compra real, checksum não é assinatura e import não permite execução de trabalho/transferências.
- \`CanonicalEconomicPriceHistoryEnvelope(Version=1, PayloadJson, PayloadSha256Hex)\` com \`CanonicalEconomicPriceHistoryPayload\` versionado. O payload inclui: source/target economic cycle count, movement turn, journal index, worker/network revision, world hashes, maximum response step e 1..3 observações por atividade.
- Valor quantitativo de \`BigInteger\` persistido como texto decimal canônico não negativo para preservar exatidão entre plataformas; preço relativo \`numerator/denominator\` [0,1]. Duplicados, enum inválido, delta incompatível e dados fictícios geram erro. Ordem determinística por atividade.
- Tamanho máximo 128 KiB. JSON exige esquema e serialização canônica, sem extensões ignoradas silenciosamente.

## Limitações intencionais

- Recuperar um sidecar cujos valores de preço foram **alterados e cujo checksum foi recalculado** não permite provar a cotação original: mesmo com os hashes legítimos do jogo, o sidecar continua apenas um artefato de **observação não confiável para decisões produtivas**. O mecanismo não alega uma assinatura digital nem prova de preços executados. Uma etapa futura precisará escolher política de preço realmente executável e uma forma de registrar no save/diário a sua origem.
- Inspeção histórica somente no **save do turno alvo exato**; depois de novos turnos o registro não corresponde mais ao estado atual. Registro perpétuo/múltiplas observações depende de modelo específico, não é inferido.
- Eventos de reatribuição/rede entre ciclos são rejeitados na VS85 e na VS86. Sem simular histórico ausente, sem retroatividade.
- Sem \`B=40\`, bancos/crédito, saldos inventados, estoque involuntário, \`SETTLE\`, preço entregue convertido implicitamente de custo de frete nominal ou micromanagement individual.
- Não inferir lucro monetário de um índice relativo ou tratar demanda como teto físico de comércio. A logística autenticada permanece exclusivamente como prévia física VS81.

## QA

Sete testes para roundtrip de save, inspeção de sidecar externo, prova de ciclos e hashes, replay determinístico, corrupção de checksum, modificações com checksum recalculado mas inconsistências matemáticas, origem/ação/ciclo adulterados, estado já avançado, JSON inválido e limite de tamanho.

Benchmark 8,32,128: construir dois ciclos físicos de fato, exportar sidecar comprovado VS85, restaurar save canônico uma vez e medir inspeção read-only sem mutar o mundo. Gate Linux VS40–86, Windows full suite e Godot Mono Release/Debug; 52 workflows esperados. Homologação somente no HEAD final 100% \`completed/success\` de uma PR Draft empilhada sobre VS85, sem merge na \`main\`.

## Promoção futura

Para que feedback macro seja promovido ao simulador real, exigimos ainda ADR que feche o que configura **preço efetivamente observado pelo motor**, registro temporal confiável no save/diário, escolha explícita de política de produção, desacoplamento de mecanismo legado de estoque/caixa e testes de estabilidade em muitos ciclos. A VS86 prepara inspeção histórica, não efetua essa promoção.
