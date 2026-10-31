# GA-VS50 — Exclusão entre processos e recuperação em pontos de falha (laboratório)

**Data:** 31/10/2026. **Base empilhada:** GA-VS49 PR #155, head `2fd4c54a8349a1ce4d5c4377adad64cd21e8d4f2`, **15/15 workflows aprovados**. **Escopo:** checkpoint financeiro experimental somente, sem ativação da economia.

## Decisão

A VS49 validou checkpoints reabertos e idempotência entre objetos, porém sua sincronização era apenas um `lock` intra-processo. Na VS50, cada acesso ao caminho `.ga-vs49-lab.json` por **Create, Open, Commit e Inspect** também exige um bloqueio de arquivo cooperativo no SO. `ExperimentalCheckpointProcessGate.Acquire` abre um arquivo lateral `.gate` com `FileShare.None`, aguardando no máximo 10 segundos por padrão (configurável, sempre finito). O bloco fica aberto durante **toda a leitura, verificação financeira, preparação, publicação e resposta**.

O arquivo `.gate` **não é apagado quando a operação termina**. O bloqueio pertence ao descritor de arquivo e é liberado pelo SO ao fechar o processo; apagar seu nome criaria riscos de duas gerações de locks em inodes diferentes no Linux. A existência de `.gate` não significa que exista uma operação em curso, e um processo que não respeite esse protocolo ainda poderá adulterar o checkpoint. Trata-se de cooperação local, não autenticação nem lease distribuído.

## Pontos de falha e recuperação

O `IExperimentalCheckpointPublicationFault` é uma interface de **injeção exclusiva de laboratório**, passada explicitamente ao abrir/criar o checkpoint. Dois pontos observáveis são definidos durante `Commit`:

1. `StageFlushedBeforeReplace`: o arquivo temporário foi totalmente escrito e `Flush(flushToDisk:true)` terminou, mas `File.Replace` ainda não aconteceu. Uma interrupção neste instante **não confirma a transação**; o arquivo oficial permanece na revisão 0. Um estágio isolado nunca é aceito como comprovante de liquidação.
2. `FileReplacedBeforeReturn`: o arquivo oficial já foi substituído pelo checkpoint confirmado, mas o autor ainda não recebeu o resultado. Uma interrupção neste instante **não permite reexecutar os débitos**. A reabertura revalida o lote original usando VS46–VS48, e a mesma decisão retorna seu comprovante com `WasAlreadyCommitted=true`.

Uma falha simulada por exceção não equivale, sozinha, à prova de que o filesystem sobreviverá a uma perda abrupta de energia; a VS50 testa **os dois estados possíveis antes/depois da publicação de um arquivo único**, não garantias de `fsync` de diretório, hardware, backups ou atomicidade de vários arquivos.

## Invariantes e limites de confiança

- Nada é adquirido sem autorização explícita da política institucional externa `IExperimentalCashSettlementAuthority`; digests VS47/VS49 não são assinatura nem permitem debitar por si.
- A auditoria VS46 reconstitui mercadorias e fretes; a VS48 prepara lançamentos de soma zero, com proteção de H e CAS, e a VS49 persiste somente a operação integral.
- O estado de abertura do comprador continua vindo do orçamento numérico **experimental**. **Poder de compra abstrato da população continua sendo o desenho canônico**: não foram criadas carteiras bancárias ou caixa privado persistente por trabalhador.
- O sidecar `.gate` está sujeito às garantias efetivas de `FileShare.None` do .NET e do sistema operacional local. Há proteção entre processos **cooperativos que abrem exatamente o mesmo caminho**, mas não prova exclusão distribuída, bloqueios em sistemas de arquivos remotos, symlink/alias, hostilidades, anti-replay entre cópias independentes de save, ou semântica de durabilidade após falta de energia.
- As operações só usam diretório próprio de laboratório. Nenhuma mudança em `PhysicalFoodCashMarketState`, save V17, world hash, turnos, orçamento canônico, UI Godot ou `main`; PR segue draft stacked.

## Testes

- Exceção antes de `File.Replace`: bytes do checkpoint original permanecem idênticos, revisão 0, nenhum crédito lançado; uma execução posterior pode confirmar o mesmo lote.
- Exceção depois da substituição: novo arquivo se reabre como revisão 1, a mesma decisão retorna comprovante anterior, nenhum segundo débito ou escrita.
- File gate disputado falha de modo finito, é reabrível depois da liberação e o `.gate` remanescente não representa uma transação fantasma.
- Negação da autoridade após reabrir, H, abertura financeira original, checksum e todos os testes VS49 permanecem válidos.
- Benchmark `--vs50-interprocess-gate` inicia **um processo .NET filho real**: enquanto o processo pai segura o bloqueio, o filho deve sair sem obtê-lo; depois da liberação, deve adquirir. Mede também aquisição/release isolados em lotes de 8/32/128, sem thresholds de desempenho.
- Linux, Windows, Godot Mono Debug/Release, full xUnit Windows, hash multiplataforma, regressões VS40–VS49.

## Roadmap

A arquitetura de armazenamento persistente de produção exigirá identificação canônica de titulares (Estado, operadores, atividades e população), autorização confiável, coordenação entre processos/servidores, proteção contra cópia divergente de checkpoints, fsync de diretório conforme o filesystem, versionamento e política de backup/restore, teste de encerramento abrupto em processos filhos e integração explícita com world-state e save. Não confundir a robustez deste sandbox com liquidação habilitada em uma partida.

Benchmark de QA:

```powershell
dotnet run --project GlobalArena.Benchmarks/GlobalArena.Benchmarks.csproj -c Release -- --vs50-interprocess-gate
```
