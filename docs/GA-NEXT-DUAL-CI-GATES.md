# GA-Next — Dois gates de qualidade no CI

## Objetivo
Evitar que escassez de runners macOS impeça o trabalho incremental no kernel, sem transformar a ausência de evidência em aprovação.

### Gate D — Desenvolvimento (autoriza criar PR empilhada)
Condições cumulativas, avaliadas **sobre o SHA mais recente** da PR-base:
1. Regressões completas do kernel em Ubuntu e Windows: **success**.
2. Hash de estado determinístico nos sistemas em que os jobs já terminaram: **success**; a validação de hash no macOS também deve estar concluída com **success** quando presente na matriz.
3. Workflows de domínio tocados pela alteração (movimento, economia, kernel, Godot smoke, benchmark): **success**.
4. Nenhum job executado tem `failure`, `timed_out` ou `cancelled` sem triagem.
5. **Única exceção admissível:** job de *regressão completa do kernel* macOS ainda `queued` devido à indisponibilidade de runner, ou cancelado por infraestrutura e já reenfileirado. Evidência da fila deve ficar registrada.
6. O código não pode ser promovido a merge enquanto a exceção permanecer.

Uma falha efetiva de teste em macOS NÃO recebe exceção. No caso de cancelamento por falta de executor, não se registra teste como aprovado.

### Gate H — Homologação / Merge
Todos os checks exigidos para o SHA atual devem estar **success**, incluindo regressão completa do kernel no macOS. Nenhuma exceção de runner vale para homologação. Revisão técnica e políticas de proteção da branch continuam aplicáveis.

### Aplicação
- O Gate D permite começar **R171 sobre R170** mesmo com a regressão macOS de R170 na fila, desde que as demais condições estejam satisfeitas.
- R170, R171 e os demais incrementos permanecem em **Draft e sem merge** até o Gate H respectivo.
- Workflows macOS **não são removidos, pulados ou marcados não bloqueantes no merge**.
- Não mudar a fórmula de mercado, tolerância do solver ou a matriz histórica para encobrir congestionamento de runners.
- Se a regressão macOS finalmente falhar, suspender a promoção da cadeia que depende desse SHA e corrigir antes de homologar.
- A política é de processo/avanço de desenvolvimento; não altera automaticamente configurações de branch protection do GitHub.

## Registro da decisão — 2026-11-23
Base R170 PR #563, commit `dc3287370125ed2e712b8024e09660a1aede99ea`. CI de domínio, regressões kernel Windows/Ubuntu, hash macOS/Windows/Ubuntu e smoke test Godot aprovados; regressão kernel macOS reenfileirada e sem resultado. Portanto o Gate D é elegível, mas o Gate H permanece pendente.
