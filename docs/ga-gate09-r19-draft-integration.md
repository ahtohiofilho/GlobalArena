# Gate 09 R19 — consolidação experimental da conversão IEEE-754

> **Estado: PR Draft — laboratório isolado; não autorizado para produção ou merge.**

## Evidência de homologação local

Validação Windows em 2026-11-12, pacote R19/FIX1:

- Manifesto SHA-256: **16/16** arquivos conferidos.
- Contratos Python: **3/3** testes aprovados.
- Três projetos .NET 10 Release compilados com **0 erros e 0 avisos**.
- Resultados comerciais F-072: **23/23** JSON idênticos aos vetores de referência.
- Testemunhos IEEE-754: **270/270** razões exatamente equivalentes em bits.
- Entradas do verificador: C# corrigido aprovado previamente no R18/REV4.
- Execução R19: **PASSED_R19_BINARY_PARITY**, com benchmark observacional.

## Risco de desempenho medido

Microbenchmark .NET 10, Windows x64, 270 razões × 1.500 iterações, 7 rodadas:
- Conversão exata, mediana: **81,3663 ms** por 405.000 conversões.
- Conversão decimal legada, mediana: **23,1892 ms**.
- Overhead observado: **3,5088×** (exata/legada).
- As rotas **não são numericamente equivalentes**; o dado mede custo, não fundamenta substituir a exata.
- Não é benchmark do clearing econômico completo, portanto não estabelece impacto na simulação.

## Fronteiras e critérios de aceite

O laboratório independente deve viver em `experiments/gate09/r19/` e o workflow em `.github/workflows/ga-gate09-r19-parity.yml`, preservando os 16 arquivos sob manifesto imutável. O fluxo de CI deve compilar e comparar os 23 cenários e 270 vetores nos runners Windows e Linux; o benchmark fica observacional.

- Não alterar `GlobalArena.Kernel`, `GlobalArena.Runtime`, `GlobalArena.Simulation`, tesouros, journal ou regras comerciais F-043/F-072.
- Não declarar F-059 resolvido, convergência global, nem equilíbrio endógeno validado.
- Não promover ao motor autoritativo automaticamente.
- Exigir verificação de CI e revisão dos custos numéricos antes de considerar um passo de integração.

**Nota de governança:** este documento abre o rastreamento do R19. A presença deste documento, isoladamente, não confirma que o código do laboratório já foi integrado ao branch. O conteúdo experimental completo será adicionado como um commit separado e submetido à CI antes da revisão.
