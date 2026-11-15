# GA-Next — Primeira execução econômica C# da nova fundação (15/11/2026)

**Natureza:** corte vertical funcional e executável em console; NÃO solver F-072 homologado, NÃO `WorldState` autoritativo, NÃO integração Godot, NÃO lançamento. Novo código de produção adicionável; a economia M4 anterior continua intocada e NÃO governa esta fundação.

## Decisão operacional (encerrando a sequência de micro-ADRs)

A economia é definida pelas leis, não pelo requisito de resolver com precisão infinitesimal todos os mercados a cada turno. O propósito do produto é observar um mundo jogável e decidir empiricamente a escala necessária dos nós. A complexidade de combate e outras camadas precisa entrar no ciclo antes de novos investimentos extensos em micro-otimizações.

A introdução do congestionamento dependente do volume foi o principal acoplamento matemático: preço influencia fluxo, fluxo muda preço e o congestionamento muda atratividade das rotas. A nova abordagem conserva esse acoplamento, mas **não bloqueia a passagem do turno até obter o equilíbrio global de alta precisão**.

### Contratos consolidados

- **Todos os tiles têm as mesmas leis.** Não introduzir categorias de nós privilegiados, fronteiras artificiais de comércio, mercados agregados que substituam tiles nem limites de distância.
- **Trabalhador físico:** produz quando na atividade produtiva, demanda sempre no tile em que se encontra, inclusive em deslocamento, atividade militar ou ociosidade. A demanda de um trabalhador aparece **exatamente uma vez por turno**. Não criar comprador artificial. Produção é agregada somente por (tile, mercadoria, civilização) para economizar trabalho, sem substituir a identidade do trabalhador.
- **Preço atual por mercado:** `P=sqrt(D/Q)` para `Q>0`. Quando `Q=0`, mercado sem cotação; não inventar preço de piso.
- **Aresta de transporte:** `C_e=c_free*(1+(B_e/K_e)^4)` mais tarifa externa para civilização não titular. Sem teto duro de carga e sem regra diferente para um tile especial. É o custo generalizado da referência R13; não equivale a uma liquidação financeira completa.
- **Oferta física sempre alocada:** toda produção tem um destino; nenhuma quantidade desaparece na tentativa de encontrar melhores rotas. O volume de uma aresta soma todas as mercadorias que realmente a utilizam.
- **Reajuste adaptativo:** um produtor que observa grande vantagem relativa em outra alternativa pode deslocar parcela maior de seu comércio; próximo da melhor alternativa, reduz o passo. Não forçar erro econômico ou ruído para simular concorrência imperfeita.
- **Qualidade:** conservação física e integridade de entradas são rígidas. Precisão da alocação é monitorada em indicador de oportunidade perdida, com tolerância e orçamento de passes configuráveis. A fórmula concreta do passo e os valores do exemplo **NÃO são canônicos**, nem equivalem a erro percentual de preço ou certificado F-072.
- **Sem alegação de paridade:** finanças F-084F/F-059, impostos, infraestruturas possuídas de forma integral, construção, combate, curva agrícola por clima e certificação independentemente F-072 seguem fora deste corte.

## Entrega executável

`GlobalArena.Simulation/Foundation/PhysicalWorkerTradeCycle.cs` cria o primeiro ciclo C# isolado e determinístico da nova fundação:

1. Recebe arestas do grafo, trabalhadores físicos, mercadorias e demanda-base por trabalhador. Nenhuma diferenciação funcional de nós.
2. Recebe ordens de atividade e deslocamento de **uma aresta por turno** (para o protótipo; a regra completa de velocidade de movimento ainda não está integrada). Valida todo o lote antes de alterar o estado.
3. Constrói demanda nos tiles efetivamente ocupados e produção somente nas atividades produtivas. A oferta se agrega sem criar grupos com supply artificial.
4. Inicializa, com produção REAL, pequenas vendas para destinos alcançáveis na primeira ativação ou alteração de mercados; depois mantém o estado comercial anterior quando compatível.
5. Avalia os preços `sqrt(D/Q)` e os custos de arestas carregadas `c_free*(1+(B/K)^4)`, incluindo tarifa externa.
6. Em um número **limitado** de passes, calcula oportunidades e redistribui uma fração adaptativa da produção de cada grupo. Os totais são conservados dentro do erro de ponto flutuante monitorado, sem afirmar que já alcançaram equilíbrio global.
7. Publica snapshot de turno com produção, entrega, demanda, preço dos mercados cotados, cargas nas arestas, atividades e indicador do maior desvio relativo da oportunidade observada. Snapshot é consumível por camada futura de apresentação.

A implementação usa `double` apenas para o **protótipo de solução comercial**. Não implementar liquidação monetária em ponto flutuante. Futuro livro-caixa F-084F exige unidades inteiras/fixas e invariantes financeiros próprios.

### Como executar

```powershell
dotnet run --project GlobalArena.Console/GlobalArena.Console.csproj -- --foundation-slice
```

A opção executa três turnos de uma pequena rede conectada: agricultores, demanda em todos os tiles ocupados, trabalhador inicialmente em atividade militar, trabalhador viajando de um tile ao vizinho e retorno à produção. O console imprime variáveis de mercado, não apenas tempos de benchmark.

### Garantias e pontos pendentes

O cenário é deliberadamente pequeno para demonstrar o ciclo, **não** para fixar a escala final do planeta. Nenhum nó é diferente de outro por ser capital, mercado regional, centro industrial ou zona especial. Demandas não atendidas por ausência de produção real permanecem demandadas e **sem cotação**, não geram mercadoria fictícia.

O programa não reutiliza o circuito econômico legado M4; a separação impede confundir as fórmulas e os invariantes com o redesenho iniciado em novembro. O fluxo é determinístico na mesma plataforma e usa ordenação estável; paridade bit a bit entre arquiteturas e moedas ainda não foi homologada.

**Próximos incrementos com ROI direto de jogabilidade:**
- Ligar nós e arestas ao Goldberg real, trabalhadores físicos ao estado canônico e o tick econômico à política de turno;
- Mostrar ofertas, rotas e preços nos painéis do Godot sem tornar a UI autoritativa;
- Integrar deslocamentos e confrontos militares com o mesmo grafo e trabalhadores;
- Só depois calibrar tolerâncias pelo efeito em preços, receitas, rotas e decisões efetivas de jogo, não por casas decimais arbitrárias.

**Não abrir outra série de ADRs de microprofiling.** O solver Python independente F-072 continua oráculo de referência para cenários controlados, não requisito computacional de cada tick de jogo.

## Verificação

CI Windows e Ubuntu compila o projeto .NET 10, executa os testes `FoundationPhysicalTradeCycleTests`, realiza os três turnos do console e reexecuta a suíte C# existente. Nenhuma fonte econômica legada nem interface Godot foi modificada.
