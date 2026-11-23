# GA-Next — Gates de desenvolvimento e homologação (Windows + Linux)

## Decisão de produto — 2026-11-23

**Windows e Linux são as únicas plataformas obrigatórias** para desenvolvimento e homologação do GA-Next neste estágio. macOS não integra as matrizes automáticas nem é pré-requisito de merge. Não estamos declarando o jogo incompatível com macOS; apenas não prometemos suporte ou certificação em cada revisão. O GitHub macOS mostrou filas que prejudicam significativamente a produtividade.

## Gate D — Desenvolvimento
No SHA atual, todos os workflows de domínio afetados e as regressões completas de kernel **Ubuntu e Windows** devem concluir com `success`, sem falhas reais. Hash determinístico obrigatório em **Ubuntu e Windows**. Trabalhos ainda em andamento não podem ser tratados como aprovados.

## Gate H — Homologação e merge
No SHA atual, **todos os checks obrigatórios Windows/Linux** devem concluir `success` e as regras de revisão/branch protection devem ser satisfeitas. macOS não é exigido. A PR permanece Draft até a homologação e decisão explícita de merge.

## macOS — auditoria voluntária
O workflow `.github/workflows/optional-macos-determinism.yml` é acionado manualmente por `workflow_dispatch` no GitHub Actions e executa hashes determinísticos e testes financeiros temporais no macOS. Seu resultado não bloqueia os gates Windows/Linux, mas uma divergência real identificada deve ser analisada e registrada como incompatibilidade não resolvida; nunca descrita como validação bem-sucedida do macOS. Os workflows históricos Windows/Linux conservam a regressão completa e os hashes.

## Observações operacionais
- Não alterar os solucionadores econômicos nem reduzir as validações Windows/Linux em função das filas macOS.
- A alteração de YAML não altera automaticamente as regras de branch protection. Verificar e remover dos requisitos obrigatórios qualquer **nome de check macOS** registrado nas configurações do repositório, caso exista.
- PR #564 permanece Draft e empilhada na R170; nenhuma promoção automática nem merge foi realizado.
- As PRs antigas preservam checks anteriores; a nova política vale para os commits e workflows que a incluem e, após integração, para os próximos.
