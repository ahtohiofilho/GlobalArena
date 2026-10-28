# GA — Revisão de integração ECON-005 (PRs #38–#47)
**Data de revisão:** 2026-10-28  
**Tipo:** checkpoint de consolidação; não é nova funcionalidade, ADR de itens, norma econômica, autorização de release nem liquidação.  
**Branch de prova:** `integration/econ-005-38-47-review-gate`.  
**Base imutável:** `main` em `c45626e96ada56e834a42ba750a487c25ccc9929`.

## Resultado estrutural da revisão
Há **dez PRs abertos** e duas linhas independentes que convergem somente em uma integração:  
- **#38**, sobre `main`: auditoria read-only da fase de congestionamento do kernel, já incluindo checagem do custo incremental de cada remessa.  
- **#39 → #40 → #41 → #42 → #43 → #44 → #45 → #46 → #47**, série empilhada sobre `main`: comparação de lotes, custos de densidade, reavaliação de mercados, identidade física, proveniência de serviços, preferência GA-PROP-001, preços endógenos, tarifas físicas, suportes alternativos e um caso de canto n=1.

Os diffs listados pelo GitHub são **disjuntos entre #38 e #39–#47**, inclusive os arquivos preexistentes que tocam, permitindo uma árvore de integração contendo ambos sem resolver conflitos de edição. A branch de prova é um *merge commit* com pais `#47` e `#38` e árvore que inclui os quatro arquivos modificados exclusivamente pelo #38. Ela **não modifica `main` nem altera a base de qualquer PR**.

## Contratos necessários antes de qualquer merge
| Gate | Contribuição | Limite que deve continuar explícito |
|---|---|---|
| #38 / A | Auditoria por remessa de custo incremental/volume | Auditor do alocador convexo existente; não de todo o mercado novo, nem auditor de settlement |
| #39 / B | Lotes finitos, exploração pura | Perfil convexo, sem efeito da densidade |
| #40 / C | `C_r(q)=a_rq+2b_r√q+c_rq³/(3K_r²)` e intervalo racional | Recurso físico `EconomicPhysicalResourceId`, custo real, não tarifa/comprovação geográfica |
| #41 / C2 | Dois mercados de referência realmente resolvidos | Cotações e curvas exógenas; não é GA-PROP-001 completo |
| #42 / D | Serviço físico/geográfico existente → incidência de custo | Binding de terminal declarado não prova ativo terminal construído nem autorização de operação |
| #43 / E1 | Utilidade GA-PROP-001 n>=1 e preço origem n=1 | `H` é consumo/numeraire residual, não saldo bancário |
| #44 / E2 | Preços de origem n>=2 em suporte interior | Newton local encontra raiz estacionária; não prova unicidade, ótimo nem fluxos nulos |
| #45 / E3 | Feedback hipotético: mercado → cargas → custo → tarifa | Política `C(L)/L` é cenário experimental e envolve `double` arredondado; não lançamento contábil |
| #46 / E4 | Comparações independentes de suportes completos | Variações de utilidade e receita são observações, não seleção automática |
| #47 / F0 | Comprador desconectado, q=0, p=0 e estoque não vendido | **Somente n=1**, frete congelado; não é equilíbrio geral multivariedade de canto |

## Restrições de integração observadas
1. **Separação funcional.** Nenhuma classe dos Gates B–F0 deve entrar silenciosamente na liquidação autoritativa. Fases devem permanecer `SNAPSHOT → EXPLORE → AUDIT → SELECT/AUTHORIZE → SETTLE`. O #38 é integração controlada da auditoria legada; não transforma o explorador dos demais PRs em motor de turno.
2. **Dinheiro e unidades.** Frações racionais e microgramas físicos podem fundamentar verificações. Os números `double` dos preços/utilidade/tarifas nos Gates E–F0 são referências hipotéticas e não podem alimentar estoque, tesouraria ou pagamentos sem contrato explícito e auditado de escala/arredondamento.
3. **Infraestrutura e permissões.** Os serviços devem ser validados pelo resolvedor da geografia do mundo. O ID de recurso não é geografia, e o registro de uma transferência em D1 **não certifica construção real do terminal**. Resolver esta autoridade antes de executar uma rota.
4. **Economia institucional.** O custo real não é receita de transporte; receita bruta de origem não é lucro. A tarifa por custo médio do #45 é uma hipótese identificada, não uma política canônica geral. Preços estacionários não demonstram otimalidade global nem participação de todos os agentes.
5. **Jogabilidade e itens.** O futuro catálogo deve permitir recursos e requisitos agregados, sem obrigar inventários de equipamentos intermediários, peças, ferramentas ou armaduras. A simples menção de um exemplo não canoniza item. O jogador não precisa movimentar manualmente cada remessa; a complexidade existe para melhorar consequências estratégicas.
6. **MVP financeiro.** Sem bancos, crédito, empréstimos, dívidas, juros, garantias, inadimplência ou autorização de caixa negativo.
7. **Stack/CI.** GitHub CI verde de cada PR em isolamento não prova automaticamente a **árvore conjunta**. Esta branch é evidência adicional de compilação, regressão, hash e benchmark das duas linhas em união no mesmo commit.

## Plano de merge/revisão após o gate conjunto
- Verificar no mesmo SHA da branch de integração os três workflows nos 3 sistemas: regressão kernel, hash de estado, benchmark de frete. Os links dos workflow runs do GitHub são a evidência, **não** a mera existência deste documento.
- Revisar/assinar o #38 separado e o #39 como duas raízes sobre `main`.
- Em eventual integração autorizada, aplicar #40, #41, #42, #43, #44, #45, #46 e #47 nessa ordem, atualizando bases das PRs empilhadas após merges de seus pais para não misturar commits.
- Exigir revisão humana dos limites D1, E2/E3 e F0 antes de acoplar qualquer resultado experimental às regras de execução.
- **Nenhuma ação de merge da `main` é efetuada neste gate.** O documento registra prontidão técnica, riscos e ações necessárias, sem substituição de testes ou decisão do mantenedor.

## Revisão incremental do Gate B — fail closed em lotes sem caminho

No checkpoint de consolidação foi identificado que o comparador `EconomicFiniteLotCostExplorer` de #39 aceitava, antes, caminho físico de origem ou destino vazio enquanto projetava um lote positivo. Embora o comparador não seja um roteador e não ateste posse, sua semântica é de **realocação de lote já transportado** e, por isso, exige ambos os caminhos não vazios. #39 recebeu uma guarda explícita e dois testes adversariais (um para rejeitar as três combinações de caminho vazio, outro para preservar o custo exato em um trajeto idêntico). A alteração foi propagada por merges não destrutivos de #40 a #47. Os diffs de PR foram verificados individualmente e seguem isolados.

Esta prova de integração retesta a união do head atualizado de #47 com #38, sem selecionar novo mercado, executar transporte ou mudar `main`. Não confundir ausência de travessia com criação de massa: operações intraponto, se necessárias, exigirão um contrato próprio com proveniência da mercadoria.
