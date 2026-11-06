# GA-CI-FAST-02 — Homologacao progressiva (06/11/2026)

## Problema operacional

Os PRs comuns executavam 2.592+ testes de regressao completa em Linux, Windows e macOS Intel, benchmarks em mais tres runners e hash multiplataforma, mesmo quando o incremento so adicionava uma leitura nao mutante. O caminho critico do macOS passou de 20 minutos. A CI FAST ja existia, mas o seletor reconhecia somente arquivos de teste cujos nomes continham GaVs[digitos], omitindo novas classes GaNutr* e GaCanon* nos testes rapidos.

## Politica vinculante — imediatamente apos este incremento

**FAST POR PR:** obrigatorio e gratuito de promessas de homologacao integral:
1. `ga-ci-fast.yml` continua automatico em todo PR. Audita gates existentes, executa unittest do seletor e confere o escopo do diff entre HEAD e BASE.
2. Para codigo de kernel/simulacao/economia/testes ou alteracoes da propria CI FAST, compila o cliente Godot e executa xUnit selecionando baseline + todas as classes xUnit declaradas nos arquivos `GlobalArena.Tests/*.cs` alterados, sem restricao ao prefixo GaVs.
3. Para modificacoes da UI, o job Windows Debug e os gates E2E reais previamente existentes permanecem acionados segundo seu escopo.
4. Nao confundir FAST success com QA visual, compilacao multiplataforma, integridade global de hashes ou replay completo.

**CHECKPOINT INTERMEDIARIO — rotineiramente a cada 3–5 incrementos relacionados, ou antes em mudancas de risco alto:** adicionar a etiqueta `ga-ci-checkpoint` ao PR. Os tres workflows abaixo executam matrizes completas no HEAD exato:
- `cross-platform-kernel-regression.yml`: toda a regressao kernel xUnit + TRX em Ubuntu/Windows/macOS Intel.
- `cross-platform-state-hash.yml`: contrato de hash em Ubuntu/Windows/macOS Intel.
- `turn-locked-freight-market-benchmark.yml`: referencias em Ubuntu/Windows/macOS.
O checkpoint nao automaticamente executa os 109+ workflows historicos etiquetados `ga-ci-full`.

**HOMOLOGACAO RELEASE / ANTES DO MERGE:** aplicar `ga-ci-full`, que continua a habilitar os gates historicos opt-in e tambem habilita os tres workflows multiplataforma. Inspecionar os resultados do ultimo HEAD; um job ignorado/skipped nao representa execucao e nao conta como aprovado. Nao fazer merge automaticamente.

As etiquetas podem ser adicionadas na aba Labels do PR. A etiqueta `ga-ci-checkpoint` precisa existir no repositorio (criacao unica via Settings > Labels / aba Issues > Labels por responsavel autorizado) antes de ser aplicada. Sem essa etiqueta os tres workflows sao ignorados em PRs ordinarios; a execucao manual `workflow_dispatch` continua disponivel para contingencia e o push na main continua disparando o escopo original.

## Disparos / falhas que nao podemos disfarcar

- `on: pull_request` dos tres workflows permanece para reconhecer `labeled`, `opened`, `synchronize`, `reopened`. O job inteiro possui `if` seguro para `ga-ci-checkpoint` ou `ga-ci-full`, com excecao apenas de `push` e `workflow_dispatch`.
- Etiquetas persistentes resultam em repeticao completa no novo HEAD de um PR checkpoint, impedindo assumir que o HEAD anterior foi validado.
- Os testes Python garantem selecao de classes sem prefixo GaVs e recusam fonte com marcador de xUnit sem classe detectavel.
- `ga_ci_lanes.py audit` continua conferindo a guarda `ga-ci-full` de todos os workflows historicos e passa a auditar os tres novos guards multiplataforma.
- O filtro rapido xUnit seleciona classes por `FullyQualifiedName~NomeDaClasse`; um teste alterado com atributo [Fact] ou [Theory] nao pode passar despercebido por uma restricao ao padrao GaVs.
- Hashes/saves nao sao reescritos por esta CI: novos testes que tratam de regras canônicas devem continuar cobrindo imutabilidade e replay no FAST, e a matriz global e comprovada no checkpoint.

**Limitacao de enforcement:** este PR melhora a CI, mas NAO altera branch protection nem estabelece obrigatoriedade de merge checks no GitHub. Sem uma regra de branch protection/ruleset apropriada, o bloqueio de merge sem checkpoint depende de governanca operacional. A configuracao dessa protecao sera tarefa separada, exigindo autorizacoes de repositorio. Nao afirmar que checks skipped certificam producao.

## Prova no HEAD

- `python3 -m unittest discover -s tools/ci -p 'test_ga_ci_lanes.py' -v`
- `python3 tools/ci/ga_ci_lanes.py audit`
- FAST: confirmar quantidade e nomes dos testes xUnit selecionados pelo PR.
- Checkpoint e full: confirmar contagens dos jobs reais e TRX, incluindo sistemas operacionais, e SHA exato do PR.

## Sequenciamento

Marco GA-CI-FAST-02 precisa passar FAST, auditoria do seletor e uma vez o checkpoint integral (por se tratar de alteracao da propria infraestrutura CI). A partir do PR seguinte, FAST passa a ser o gate cotidiano; checkpoints completos ficam para grupos de 3–5 PRs, alteracoes persistentes no kernel de risco elevado e pre-merge.

Preservar o contrato de produto do GA-CANON-DIRECTIVES-01. A aceleracao do CI nao autoriza ativar numerais provisórios de economia, cadencia cronologica ou pseudo-alimentos.
