# GA-VS51 — Recuperação após interrupção real de um processo de teste

Base: GA-VS50 PR #156, head `616566356464d705c7b8753fe7204644e509d66f`, 16/16 CI. Exclusivamente laboratório.

## Contrato

A VS50 simulou exceções antes e depois de `File.Replace`. A VS51 valida os mesmos pontos com um processo .NET filho independente, encerrado pelo processo controlador durante a operação. Em ambos os cenários, o controlador verifica a liberação do bloqueio do SO, reconstrói o snapshot financeiro e reabre o checkpoint existente. O recibo sempre é auditado novamente, nunca aceito como autorização por si só.

**Antes da publicação:** o checkpoint original permanece byte a byte igual à revisão 0. Arquivos `*.stage` abandonados não são pagamentos. Após reabrir, a operação é aplicada uma única vez.

**Depois da publicação:** o checkpoint está integralmente na revisão 1, e o reprocessamento da mesma decisão retorna o recibo já liquidado, sem nova gravação nem novo débito.

O teste confirma conservação monetária, reserva H e os valores finais da auditoria. Executa cenários nos runners Linux e Windows, com mediana de três amostras após aquecimento, sem SLA de desempenho.

## Limitações

Este teste confirma recuperação de um arquivo depois da saída abrupta **do processo**, em duas posições definidas. Não certifica falta de energia, interrupção durante a chamada ao próprio `File.Replace`, durabilidade do diretório, arquivos em rede, bloqueios entre máquinas ou vários arquivos atômicos. O sidecar `.gate` protege apenas processos cooperativos no mesmo host e caminho.

O checkpoint `.ga-vs49-lab.json` não é um save do jogo. A autoridade institucional é simulada e os dados monetários são contabilidade operacional temporária. O desenho canônico do **poder de compra abstrato** continua preservado; não há novas carteiras privadas, bancos, dívida ou crédito.

```powershell
dotnet run --project GlobalArena.Benchmarks/GlobalArena.Benchmarks.csproj -c Release -- --vs51-abrupt-recovery
```

A promoção ao jogo exigirá identidade econômica canônica, autenticação institucional, política de backup/restore e arquitetura de armazenamento persistente. PR draft stacked; `main` e os saves oficiais intactos.
