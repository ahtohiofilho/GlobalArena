# Global Arena — ADR-054: primeiro equilíbrio comercial em 192 nós demandantes

**Data:** 15/11/2026. **Base:** ADR-053, PR #342 Draft e seu commit homologado `a0a4006dcbd72508ce852d9dbfdee2f04182035c`.
**Escopo:** laboratório de escalabilidade Gate 09, sem integração em produção.
**Situação inicial:** o ADR-053 homologou o F-072 original independente em 48 e 96 nós demandantes no Ubuntu e Windows (28/28 testes em cada sistema); 192 e 2.048 nós eram **apenas** ledgers físicos.

## Hipótese e limite da inferência

Tentar certificar, pelo mesmo algoritmo e **sem relaxar as regras**, o equilíbrio comercial com **192 nós demandantes e 384 mercados (nó,item)**, alimentados por trabalhadores físicos. O cenário de 192 nós possui 4.096 trabalhadores, portanto NÃO é uma duplicação com população controlada do cenário de 96 nós (2.048 trabalhadores). Contagem de trabalhadores, distribuição espacial, produtores e mercados mudam simultaneamente; relatar tempos e trabalho observado, sem deduzir custo marginal puro por mercado.

| Indicador | Regressão 48 | Regressão 96 | Experimento 192 | Extremo espacial 2.048 |
|---|---:|---:|---:|---:|
| Nós Goldberg do mundo | 5.762 | 5.762 | 5.762 | 5.762 |
| Trabalhadores físicos | 2.048 | 2.048 | 4.096 | 8.192 |
| Nós produtores | 12 | 24 | 48 | 512 |
| Nós demandantes | 48 | 96 | 192 | 2.048 |
| Grupos produtores agregados | 24 | 48 | 96 | 1.024 |
| Mercados por (nó,item) | 96 | 192 | 384 | 4.096 |
| F-072 | **Certificado obrigatório** | **Certificado obrigatório** | **Tentativa experimental** | **Não executar** |

## Políticas congeladas

- Toda oferta e toda demanda vêm de trabalhadores físicos do ADR-052/053. Quem produz também demanda; construção, ócio, viagem e atividade militar demandam sem gerar oferta artificial. O trabalhador viajante reparte sua demanda entre tiles conforme a duração, sem consumo duplicado.
- Nós com produção são subconjunto dos nós com demanda. Mercado não equivale a trabalhador individual.
- Preservados grafo Goldberg R13/R14, tarifas por vendedor e titularidade, preços originais **P = sqrt(D/Q)**, venda integral, custos físicos e congestionamento **c_free * (1 + (B/K)^4)**, invariante F-084F, certificado independente ADR-042/F-072 e tolerância original global **e** pior grupo `<= 1e-4`.
- O solver numérico é o experimental global MCP SPD ADR-051, não um agendador econômico cooperativo C#/Godot. Não alterar R13/R20–R53, C#/Godot, `main`, preços, impostos, tarifas ou política financeira F-059.

## Budget de pesquisa pré-declarado para o primeiro F-072 com 192 nós

| Unidade algorítmica | Teto |
|---|---:|
| Avaliações H completas, inclusive rejeições | 1.100 |
| Scans de menor caminho | 3.100 |
| Certificadores ADR-042 originais | 7 |
| Colunas ativas | 1.500 |
| Gerações globais | 6 |
| Passos Newton | 185 |
| Passos backtracking | 12 |
| Produtos Krylov Jv/Jtv | 60.000 |
| Iterações CG por direção | 210 |
| Produtos triangulares dos blocos SPD | 250.000 |

São limites de trabalho do experimento, **não milissegundos nem um orçamento de CPU para turnos do jogo**. Limites de 48 e 96 nós continuam exatamente os do ADR-053. O timeout CI de 30 minutos por sistema é uma proteção operacional externa, não prova de desempenho em produção. Não expandir silenciosamente o budget depois de observar falha: documentar nova tentativa e novo orçamento em commit distinto.

## Critério de aceite e classificação

1. **Gate obrigatório:** os certificados F-072 originais de 48 e 96 nós devem continuar PASS; regressão ou ausência de tentativa falha CI.
2. **Contrato físico obrigatório:** 192 nós/4.096 trabalhadores/384 mercados e 2.048 nós/8.192 trabalhadores/4.096 mercados mantêm demanda física, conservação e integridade do grafo; não executar F-072 no extremo de 2.048 nós.
3. **Gate exploratório de 192:** apenas o certificado **original independente** com gaps global e pior grupo finitos e `<= 1e-4`, invariantes e congestionamento quártico intacto é PASS **experimental**. Se não convergir dentro dos tetos, registrar `NO_GO_EXPLORATORY_ADR054_192_F072_NOT_CERTIFIED` com gaps finais nulos, solver, tempos, trabalho e snapshots provisórios explicitamente não autoritativos; preservar regressões anteriores sem falsificar sinal verde de equilíbrio.
4. **Homologação bilateral:** PASS de 192 nós no Ubuntu e Windows, no mesmo HEAD e com evidências, é condição para discutir promoção a regressão obrigatória **em commit posterior**. Mesmo após promoção, a autorização de produção depende de outros gates.

O PR deve permanecer **Draft empilhado sobre o PR #342**, sem merge. Nenhuma homologação de 192 nós existe na abertura desta ADR. F-059 financeiro, interação com tesouro local e scheduler de tempo de CPU continuam pendentes.

## Reprodutibilidade

```powershell
python -m unittest discover -s experiments/gate09/adr054_market_192/tests -p 'test_*.py' -v
python -u experiments/gate09/adr054_market_192/ci/run_adr054.py --profile ci --output .
```

Gerar JSON e ZIP com status por estágio, budgets, gaps apenas quando certificados, contadores, tempo Python e hashes do ledger/fluxo. Diferenças temporais entre OS e tentativas não são prova de determinismo universal nem de desempenho de C#.

## Decisão pendente

**NO_GO_FOR_PRODUCTION_ADR054_PENDING_DUAL_OS_CERTIFICATION.** O resultado da primeira CI determinará se o laboratório 192 nós é candidato a promoção experimental ou requer diagnóstico de orçamento/escalabilidade. Não tocar no PR #342 nem na branch main.

---

## Resultado da primeira certificação e promoção a regressão obrigatória

Após a falha exclusivamente de pré-validação na primeira CI (o budget inicialmente declarado excedia os tetos herdados do ADR-047), os limites foram corrigidos para **60.000 Krylov** e **250.000 produtos SPD**, antes da primeira tentativa efetiva de solver. Nenhuma fórmula econômica ou tolerância F-072 foi alterada.

**[CI #43719293076](https://github.com/ahtohiofilho/GlobalArena/actions/runs/43719293076): SUCCESS**, 17/17 testes de contrato em **Windows e Ubuntu**, no mesmo HEAD `55bf8781877192bc0fc3e1da0f096ad827fad196`. Certificado **original independente ADR-042/F-072**:

| Indicador | Windows 48 | Windows 96 | **Windows 192** | Ubuntu 48 | Ubuntu 96 | **Ubuntu 192** |
|---|---:|---:|---:|---:|---:|---:|
| F-072 independente | PASS | PASS | **PASS** | PASS | PASS | **PASS** |
| Gap global F-072 | 5,1637e−12 | 2,1373e−14 | **2,2062e−14** | 5,1637e−12 | 2,1373e−14 | **2,2062e−14** |
| Gap pior grupo | 1,1755e−11 | 8,9589e−14 | **1,2920e−13** | 1,1755e−11 | 8,9589e−14 | **1,2920e−13** |
| H (incluindo rejeições) | 30 | 32 | **32** | 30 | 32 | **32** |
| Produtos Krylov | 344 | 445 | **389** | 344 | 445 | **389** |
| Scans de caminho | 60 | 120 | **240** | 60 | 120 | **240** |
| Tempo solver Python, segundos | 13,94 | 28,06 | **56,25** | 15,14 | 31,67 | **62,71** |

Artefatos ZIP/JSON: Windows ID **14163931387**; Ubuntu ID **14163449014**.
Gaps e contadores idênticos nesta fixture, sem alegar determinismo universal. O tempo aproximadamente dobrou de 96 para 192 nós, mas a população também dobrou e não há demonstração de custo assintótico nem benchmark em C#.

**Promoção subsequente:** como houve PASS original independente nas duas plataformas, a classificação de 192 nós passa a **regressão obrigatória** em commit posterior. A nova CI somente fica verde se **48, 96 e 192 nós** tiverem certificados F-072 originais. Um NO-GO em 192 gera status `NO_GO_MANDATORY_ADR054_192_F072_REGRESSION` com código diferente de zero e gaps finais nulos. Foram adicionados testes negativos de ausência de certificação, de execução e integridade do gate. É necessária reexecução da CI no HEAD da promoção; sucesso anterior não se transfere automaticamente.

**Decisão de governança:** `GO_ADR054_192_F072_CERTIFIED_LAB_ONLY` condicionado à regressão final; **NO_GO_FOR_PRODUCTION**, **NO_GO_FOR_CLAIMING_2048_NODE_F072_EQUILIBRIUM**. F-059 e scheduler C#/Godot permanecem pendentes. PR Draft sem merge, empilhado sobre PR #342.
