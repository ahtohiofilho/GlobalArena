# Global Arena — ADR-053: cobertura espacial demandante no F-072

**Data:** 15/11/2026. **Base:** ADR-052 PR #341. **Status:** implementação experimental, não aprovada para C#/Godot. PR Draft empilhado, sem merge.

## Objetivo

Ampliar progressivamente a quantidade de nós com demanda de trabalhadores PARTICIPANDO do equilíbrio F-072. Não confundir trabalhadores, nós produtores, nós demandantes, grupos agregados e mercados por par (nó,item).

O trabalhador produtor gera oferta e demanda local. Todos os trabalhadores demandam inclusive em construção, viagem, atividade militar ou ócio. Em trânsito, cada tile visitado recebe demanda proporcional aos turnos de permanência, sem consumo adicional. Nenhum mercado sintético R13 é usado; R13 fornece somente arestas, tarifas, capacidades e propriedade.

## Matriz controlada

| Perfil | Nós Goldberg | Trabalhadores | Nós produtores | Nós com demanda | Mercados (nó,item) | Grupos agregados | Gate |
|---|---:|---:|---:|---:|---:|---:|---|
| Referência ADR-052 | 5.762 | **2.048** | 12 | **48** | 96 | 24 | F072 obrigatório |
| **Candidato ADR-053** | 5.762 | **2.048** | 24 | **96** | 192 | 48 | F072 exploratório |
| Degrau espacial | 5.762 | 4.096 | 48 | **192** | 384 | 96 | Ledger, NÃO F072 |
| Extremo ADR-052 | 5.762 | 8.192 | 512 | **2.048** | 4.096 | 1.024 | Ledger, NÃO F072 |

O primeiro par mantém a mesma população e geografia e dobra o número de nós demandantes. A posição e atividade dos indivíduos por aglomerado também se alteram; o experimento NÃO constitui uma prova causal pura sobre o custo por mercado. Os dois patamares espaciais apenas verificam a integridade de milhares de trabalhadores e mercados, sem declarar equilíbrio não computado.

## Código executável

O novo módulo de ADR-053 reutiliza, sem alterar:
- O gerador worker-coupled do ADR-052 (itinerários reais em seis turnos, oferta agregada por nó/mercadoria/vendedor, mercados por nó/mercadoria);
- O precondicionador em sub-blocos SPD do ADR-051, com equação global MCP/VI original;
- A certificação original e INDEPENDENTE ADR-042/F072 (ambos os gaps global e pior grupo <=1e-4, rotas R13/R14, vendas integrais, preços e F084F).

A população demanda sem multiplicação ao viajar. Todos os nós com produção pertencem ao conjunto de nós demandantes P subconjunto de D. A semente inicial tem Q positivo com produção real, mas NÃO é um equilíbrio comercial certificado.

### Orçamento de pesquisa predeclarado para 96 nós demandantes

| Unidade de trabalho | Teto |
|---|---:|
| H completas com rejeições | 760 |
| Scans de menor caminho | 1.900 |
| Certificados ADR-042 | 6 |
| Colunas ativas | 750 |
| Gerações globais | 5 |
| Passos Newton aceitos | 145 |
| Backtracking | 10 |
| Produtos Krylov Jv/Jtv | 34.000 |
| Iterações CG por direção | 145 |
| Produtos triangulares de blocos SPD | 145.000 |

A referência de 48 nós usa o orçamento original do ADR-052, sem modificação. Esses tetos são computacionais, NÃO valores temporais de SLA para o jogo. Os tempos observados são apenas de Python em GitHub Actions.

## Gate de aceite

**Obrigatório:** equilíbrio F-072 independente do cenário de referência de 48 nós, conservação individual e agregada do consumo, produção íntegra, P subconjunto D, itinerários adjacentes, mercados positivos e contabilidade de 96/192/2.048 nós demandantes.

**Exploratório neste primeiro gate:** resolução F072 para 96 nós demandantes. Se não houver certificado original, o runner registra NO_GO_EXPLORATORY_96_DEMAND_NODE_F072_WITH_VALID_LEDGER, sem retorno de alocação final. A CI de integridade poderá ficar verde enquanto o cenário comercial é NO-GO: esse resultado NÃO valida o equilíbrio de 96 nós.

Se o candidato 96 nós receber PASS em Windows e Ubuntu, será promovido a regressão obrigatória em commit separado, com testes negativos. Jamais apresentar o ledger de 192 ou 2.048 nós como certificado econômico.

## Próximos investimentos

Após este gate, aumentar gradualmente o número de nós demandantes que chegam ao equilíbrio, avaliar custo e memória por mercado/rota, e medir CPU real do C# com reserva de processamento para interface, guerra e demais camadas. O atual solver ainda não implementa execução cooperativa nem suspensão de cálculo. A fração de turnos gasta em arestas e a futura autonomia de tesouros locais também precisam de especificação.

F059 segue não ratificado. Nenhum arquivo original R13/R20–R52, regras F072/F084F, C#/Godot ou main é modificado. Não alterar congestionamento quartico sem decisão explícita.
