# GA-VS49 — Checkpoint de liquidação recuperável, laboratório isolado

**Data:** 31/10/2026. **Base empilhada:** GA-VS48 PR #154, commit `cf6f7ca3035a3a89e3fea7d22e4f535b03d3e768`, 14/14 workflows aprovados. **Status:** PR draft, arquivos exclusivamente de laboratório, sem promoção à `main`.

## Requisito de negócio e governança

A GA-ADR-ECON-005 estabelece `SNAPSHOT → EXPLORE → AUDIT independente → SELECT/AUTHORIZE → SETTLE`. A VS48 comprovou SETTLE atômico e idempotente **apenas na memória de um processo**. A VS49 adiciona um checkpoint em arquivo próprio para comprovar que **fechar e reabrir** não autoriza cobrar a mesma operação duas vezes.

**O poder de compra da população continua abstrato no desenho canônico.** Os saldos e as carteiras deste laboratório são representação operacional numérica de teste para conservação financeira, e NÃO instituem saldos monetários persistentes por cidadão no jogo.

## Arquitetura: arquivo próprio, sem migração do save

`ExperimentalRecoverableCashCheckpointSandbox` exige caminho terminado em `.ga-vs49-lab.json`. Não escreve `CanonicalPhysicalTacticalSession`, `PhysicalFoodCashMarketState`, save V17, world hash, UI ou journal de partidas. O diretório de laboratório é criado explicitamente pelo chamador. Um arquivo existente não é sobrescrito por `Create`.

O checkpoint tem **schema 49**, turno, hash do estado do mundo **fornecido por produtor confiável**, revisão da rede, digest de snapshot, revisão do registro (0=aberto, 1=liquidado), contas tipadas, lançamentos e comprovante completo. A serialização é JSON UTF-8 determinística, com ordenação canônica das contas e checksum SHA-256 do conteúdo. **O checksum detecta acidentes, não autentica quem gravou o arquivo**: a autenticação continua externa.

## Protocolo de gravação e reabertura

1. `Create` prepara checkpoint aberto com saldos de origem, sem lançar qualquer compra.
2. `Commit` relê e valida o checkpoint, exige turno/hash/revisão atuais e **autorização institucional revalidada** via VS47/VS46 e `IExperimentalCashSettlementAuthority`; exige CAS `expectedStateRevision=0` para publicar o primeiro lote.
3. A VS48 é executada em **objeto descartável**, com o mapa de abertura original explicitamente fornecido. Debita mercadorias e frete em uma cópia, protege H, credita produtores/operadores e verifica `Σ abertura = Σ fechamento`.
4. Somente depois de o lote completo ter passado, a VS49 prepara arquivo temporário **no mesmo diretório**, com `FileOptions.WriteThrough` e `Flush(flushToDisk:true)`, e tenta `File.Replace` sobre o checkpoint aberto existente. Uma falha antes da substituição não altera o arquivo comprometido. Uma queda após a troca é recuperável pela leitura do checkpoint completo.
5. `Open` exige as mesmas ofertas, preços, massas, orçamentos, contas de abertura e **decisão original confiável**. Reexecuta num sandbox descartável a autorização, a auditoria independente e o settlement esperado, e compara o checkpoint persistido **por conteúdo canônico completo**, não apenas o hash. Se saldos, journal, comprovante, schema ou provenance divergirem, a recuperação **falha fechado**.
6. Quando já liquidado, `Commit` sobre a **mesma decisão positiva e autenticada** retorna o **comprovante original** (`WasAlreadyCommitted=true`) e não realiza nenhuma operação de escrita. Outra decisão, ator, candidato ou rede não pode cobrar o mesmo snapshot.
7. Temp files `*.stage` remanescentes de falhas são ignorados como não confirmados. Só o arquivo no caminho oficial do laboratório pode ser reconhecido; arquivo em estágio não é tratado como novo estado.

## Limitações de atomicidade e segurança

- A serialização é protegida por **lock estático por caminho dentro do mesmo processo**. **NÃO** existe garantia de exclusão entre processos ou máquinas, lock de SO, journal transacional de banco, autenticação de identidades, anti-replay de save fora do jogo nem sincronização multi-site.
- `File.Replace` em mesmo volume e `Flush(true)` melhoram a publicação de arquivo único, porém **não provam durabilidade após perda súbita de energia ou crash do dispositivo**. Fsync do diretório, comportamento de filesystem/antivírus/armazenamento em rede, backup, snapshots e recuperação de falhas de hardware exigirão um storage adapter próprio.
- O checkpoint validado depende do **snapshot canônico confiável fornecido externamente**. Um checksum sozinho pode ser recalculado por um invasor, e o arquivo não certifica sua própria autorização. O laboratório reconstrói contas e comprovantes com uma permissão externa; integração real precisa de emissor de estado, autenticação e provenance duráveis.
- `Open` não restaura um objeto de jogo ou avança os turnos; reconstrói uma visão do settlement a partir de dados originais e verifica se o arquivo corresponde ao resultado. Estado autoritativo e migração do save ficam para fase posterior.
- Um lote por snapshot. Nunca usar receita do mesmo ciclo para cobrir gasto antecipado, nem confundir produtores, transportadores, governo e casas.
- **Não** habilita salários monetários privados, bancos, crédito, dívida, compromissos futuros, transbordo nem micromanagement.

## Gates QA

- Criar checkpoint aberto, salvar, descartar objeto, reconstruir **outra instância de snapshot/seleção**, reabrir, validar compromisso e repetir a decisão sem segundo débito.
- Reiniciar antes da primeira compra permite liquidar normalmente, apenas uma vez.
- H protegido e saldos de operadores/produtores preexistentes preservados, dinheiro total exato em ambos os lados.
- IDs duplicados/conflitantes, ator divergente, autorização negada, hash de mundo/revisão/turno obsoletos e CAS incorreto não modificam bytes.
- Arquivo truncado, schema/proveniência alterados, ledger falsificado ou abertura financeira diferente falham fechado; nenhum checkpoint `*.stage` remanescente é aceito como liquidado.
- Concorrem 16 tentativas no mesmo arquivo, distribuídas entre duas instâncias do sandbox no mesmo processo: 1 gravação e 15 replays, sem double charge.
- Rejeitar arquivos de save canônicos por extensão e preservar os bytes de um save existente.
- xUnit direcionado VS40–VS49 e suíte integral Windows, Godot Mono Debug/Release, regressão hash e benchmark 8/32/128.
- Benchmark mede **commit+open+replay** (3 amostras após warmup; no timing threshold, sistema de arquivos temporário do CI, não desempenho de produção).

Comando de benchmark:

```powershell
dotnet run --project GlobalArena.Benchmarks/GlobalArena.Benchmarks.csproj -c Release -- --vs49-recoverable-checkpoint
```

## Roadmap pós-VS49

Somente depois de verificarmos a recuperação do sandbox poderemos estudar um storage adapter com bloqueio entre processos, versionamento de save, proveniência canônica das contas e identificação jurídica de operadores/Estado, autorização assinada e migração opt-in. **A regra do poder de compra abstrato permanece inalterada**: o orçamento numérico de cada ciclo é derivado por simulação e não exige carteira privada persistente.
