# GA-VS107 — CI incremental rápido e homologação completa sob demanda

**Natureza:** incremento de infraestrutura, empilhado na VS106 homologada (PR #245).
Não altera o motor, os saves, o Godot, os GPP ou os marcos do roadmap.

## Problema observado

A VS106 precisou de **86 workflows / aproximadamente 2h22 de ponta a ponta**,
com mais de uma hora inicial de fila. Isso não é uma métrica de desempenho
do jogo: é latência de agendamento somada ao custo e à redundância do CI.
Os workflows históricos foram acumulados por VS e muitos eram disparados a
cada alteração, mesmo quando apenas outro subsistema havia mudado.

## Contrato de duas trilhas

### 1. Trilha FAST (obrigatória na rotina)

`.github/workflows/ga-ci-fast.yml` executa em toda PR:

- Audita por script os **109 workflows históricos**, suas condições de
  checkpoint, a preservação de `workflow_dispatch` e a ativação
  automática do E2E de UI.
- Detecta o escopo pelo diff exato `base.sha..head.sha`, em vez de
  inferir resultados por workflow previamente executado em outro commit.
- Se houver código C#/Godot: compila o cliente Godot em Linux e roda
  regressões selecionadas de presença, cadência, movimento e classes de
  testes alteradas. Se a UI mudar, também compila Debug no Windows.
- O `GA-QA-UI-E2E-001 Real Godot .NET Runtime` original **continua
  automaticamente habilitado em PRs que alteram o Godot**, inclusive
  com import, SceneTree, save/reopen e gameplay real.
- Alteração exclusivamente documental/de CI executa a auditoria,
  sem ocupar runners de compilação desnecessariamente.

FAST **não** homologa toda a economia, mundo, transporte e persistência.
É feedback técnico rápido, não substitui a conformidade integral.

### 2. Trilha CHECKPOINT (obrigatória antes de fechar blocos significativos)

- Adicionar a etiqueta exata **`ga-ci-full`** à PR.
- O evento `pull_request:labeled` habilita os jobs dos workflows
  históricos com o predicado
  `github.event_name != 'pull_request' || contains(github.event.pull_request.labels.*.name, 'ga-ci-full')`.
- Os filtros de arquivos originais permanecem vigentes. Os testes de
  cada workflow, builds Windows/Linux, benchmarks e artefatos não foram
  removidos ou reduzidos. Uma PR que alterar uma família de arquivos
  só dispara os históricos aplicáveis, como anteriormente.
- `workflow_dispatch` e eventos `push` já existentes continuam sem
  restrição de etiqueta. Como 110 workflows do stack ainda não existem
  na `main`, **não depender de workflow_dispatch manual enquanto
  estiverem apenas na branch empilhada**.
- Se o HEAD mudar com a etiqueta presente, os eventos synchronize
  voltam a executar a bateria relevante automaticamente. Para retornar
  à rotina FAST, remover a etiqueta após o checkpoint.
- A etiqueta deve existir no repositório (criação única em Settings >
  Labels, se necessário). Sua aplicação não precisa ser um rito a
  cada VS: só nos checkpoints; pode ser feita pelo conector GitHub.

### Leitura correta da homologação

Uma execução `completed/skipped` **NUNCA é um teste aprovado**.
Na trilha FAST, exigir o gate `GA-CI FAST Incremental Quality Gate`
e o E2E Godot se houver mudança de UI. Na trilha CHECKPOINT, conferir
os workflows reais disparados para aquele HEAD, seus jobs e as evidências
obrigatórias, sem contar SKIP como SUCCESS. O PowerShell
`tools/ci/GA-CI-monitorar.ps1` possui modos separados.

A PR continua Draft, sem merge implícito em `main`. Nenhuma redução da
cobertura integral, das invariantes de segurança, da fisicalidade ou dos
requisitos de UX foi autorizada.

## Riscos e mitigação

- **Gate rápido insuficiente:** compensar com checkpoint completo para
  migrações, mudanças estruturais, integração e homologação.
- **Dependência de etiqueta:** auditar automaticamente que todos os
  workflows históricos a reconhecem; registrar o modo antes de avaliar CI.
- **Estouro de runners em checkpoints:** manter execuções completas
  ocasionais em vez de acioná-las a cada pequeno commit; acompanhar
  latência de fila separadamente da execução.
- **Falha do próprio fast gate:** bloquear incremento e corrigir; não
  interpretar CI histórico pulado como alternativa verde.

## Critério de aceite VS107

1. Todos os workflows históricos identificados com jobs protegidos,
   sem remoção dos comandos e com seus eventos manuais preservados.
2. PR com mudanças exclusivas de CI: FAST audita e termina com sucesso,
   sem iniciar o conjunto pesado de jobs históricos.
3. PR com UI: FAST + Godot E2E original; Windows condicional.
4. Checkpoint explícito: etiqueta habilita históricos aplicáveis,
   verificados no HEAD. Esta execução não é requisito para cada commit
   operacional, mas é exigida na passagem de etapa significativa.

Referência de base: VS106 PR #245, HEAD
`4df839c8d22915c503a4706ba20798edf6cf5e63`.
