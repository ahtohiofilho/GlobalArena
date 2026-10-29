# Global Arena — retomada por ROI: vertical slice antes de infraestrutura

**Decisão de planejamento:** 29/10/2026. Não abrir novas tarefas G3-J3r de
auditoria, PostgreSQL ou HA antes de demonstrar uma fatia de gameplay
funcional e integrar a interface Godot no repositório controlado.

## Diagnóstico baseado no repositório

- `main` contém a solução C# (`GlobalArena.slnx`), mas
  `GlobalArena.Console/Program.cs` ainda imprimia somente
  `Hello, World!`. Não há `Godot/project.godot` nem
  `GlobalArena.Godot/project.godot` nas localizações verificadas
  da `main`. A interface Godot executada no Windows requer
  versionamento/importação explícitos; não alegar que está integrada.
- PRs #52–#58 desenvolveram dois pontos econômicos,
  reservas, fundação por trabalhadores, produção primitiva e
  produtividade geográfica. Eles existem como **cadeia de PRs aberta**,
  não como funcionalidades presentes em `main`.
- Há **uma ruptura de ancestralidade** relevante: o PR #65
  (`G3-I3`) baseia-se na branch
  `feature/econ-005-runtime-facility-identity-hash`, **não na
  branch G3-I2 (#64)**. Em consequência, não presumir que recursos
  de G3-C/D estão todos presentes no HEAD de G3-J3d/q. Esse
  desvio precisa de uma integração explícita de diferenças, não
  de merge cego das branches encadeadas.
- PRs #75–#87 aprofundaram recuperação HA, PostgreSQL,
  assinatura operacional e auditoria forense; todos permanecem
  em draft/open. Preservar o trabalho, **não continuar a cauda** agora.

## GA-VS01: primeiro recorte funcional verificável

**Base:** `feature/ga-g3d-geographic-primitive-production-20261028`
(PR #58). É uma ramificação funcional, não um prolongamento dos
PRs operacionais #75–#87.

A branch GA-VS01 converte a aplicação Console de placeholder em um
**harness interativo de ações neolíticas** e faz a integração real
entre os modelos G3-C e G3-D:

- Gerar deterministicamente o planeta por seed (`Goldberg(2,0)`
  **somente para o teste rápido**, não como tamanho final do jogo).
- Criar primeira população de **oito trabalhadores que são
  simultaneamente produtores e consumidores**, com agricultura,
  coleta de madeira e pedra.
- Consultar territórios terrestres adjacentes que não pertencem
  a ninguém e não estão ocupados.
- Emitir ordem `fundar` e acionar
  `EconomicNeolithicSiteFoundingResolver.ProposeBasic` para
  **alterar o WorldState territorial**, com transferência
  conservativa dos trabalhadores. Fundação primitiva não cobra
  estoque de madeira/pedra.
- Gerar prévias de oferta geográfica por trabalhador e
  demanda alimentar para todos os trabalhadores via
  `EconomicGeographicPrimitiveWorkerEconomyResolver`.
- Testes verificam conservação, rejeição de fundação inválida,
  identidade de seed, oferta/demanda por ponto e que prévias
  não mutam o `WorldState`.

### Execução

```powershell
dotnet run --project GlobalArena.Console -- demo 0
dotnet run --project GlobalArena.Console -- play 0
```

Comandos no modo `play`:

```text
status
vizinhos 1
fundar 1 <id-do-tile-livre> 3
prever
proximo
sair
```

`demo` demonstra automaticamente fundação e prévia de produção
antes/depois, sem intervenção humana. O gate
`ga-vs01-neolithic-interactive.yml` compila e executa **a
aplicação real**, além dos testes. Isso é um recorte de inspeção
funcional, **não** um jogo comercial/loop completo: `proximo`
ainda só contabiliza a prévia de produção, não liquida mercado,
não incrementa turno canônico, não mantém estoques nem grava saves.

## Priorização após GA-VS01

| Ordem | Entrega | Critério de aceitação |
| --- | --- | --- |
| 1 | **Conectar Godot ao repositório e à simulação** | Cena estratégica do globo com seleção de tile, vizinhos, fundação e mudança visível da ocupação, testada no Windows |
| 2 | **Turno econômico jogável e fechado** | Jogador encerra turno; produção física, consumo, mercado, estoques e novos pontos se refletem num estado persistente e recarregável |
| 3 | **Movimento tático, fronteiras e combate mínimo** | Comando em tile selecionado, resultado determinístico observável e território conquistável/defensável |
| 4 | **Loop offline coerente** | Nova partida → jogar vários turnos → salvar → fechar → reabrir → continuar sem divergência |
| 5 | **QA e lançamento do MVP** | Testes Godot/Windows, UI legível, observabilidade de bugs, performance e embalagem |

Fora do caminho crítico offline: bancos, crédito, contratos
financeiros, integração PostgreSQL real, failover de cluster,
Object Lock, custódia WORM, atestação externa e plataformas
servidoras. Retomar essas frentes **somente** quando houver um
requisito de gameplay online/produção concreto ou ameaça
mensurável que justifique custo.

### Regra de decisão de ROI

Antes de criar cada próximo PR, responder:

1. O jogador verá/fará algo novo na interface?
2. O estado da partida mudará de forma verificável?
3. O resultado aproxima uma execução completa no Windows?
4. A mudança pode ser testada e revertida sem afetar a `main`?

Se não atender pelo menos os itens 2–3 no agregado do milestone,
a tarefa de infraestrutura só entra se desbloquear um impeditivo
reproduzido.

**Operação de git:** não alterar `main`, não mesclar os PRs
antigos indiscriminadamente. Reconciliar genealogia da cadeia G3
antes de integrar v9 com G3-C/D. Preservar o desenho v9 de
save/replay que for necessário, mas **separar** os mecanismos
opcionais de HA e auditoria externa.
