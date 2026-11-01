# GA-VS67 — Fatorização segura de seleção de fretes por componentes independentes

**Baseline:** VS66 PR #173 (`8a99f2509e3feee4211734b287993af5cb754480`), homologada 32/32 workflows. **Não há merge na main**.

## Motivação

VS66 garante ótimo reprodutível e auditoria independente num domínio explícito, com no máximo oito candidatos e 4096 configurações. Ampliar ingenuamente o produto cartesiano exigiria tempo exponencial; limitar indiscriminadamente a quantidade de produtos penalizaria povoamentos distantes que nem compartilham compradores, demanda, produção ou infraestrutura.

A VS67 não elimina a complexidade de blocos correlacionados: **apenas separa componentes demonstravelmente independentes**. Para cada componente, executa exatamente o seletor/auditor VS66. A concatenação é submetida à prévia conjunta VS65 para evitar resultados somados fictícios.

## Prova de desacoplamento

A partir de candidatos VS64 autenticados por *identidade de objeto*, o plano reconstrói o serviço VS63 correspondente e utiliza **somente** sua rota física e sua infraestrutura instalada. Dois candidatos são conectados se compartilharem, direta ou transitivamente:

1. **Comprador/residência**: mesmo `Buyer`, qualquer mercadoria. Assim um único orçamento/H não é contado duas vezes.
2. **Produção física**: mesma origem, mercadoria e atividade; múltiplos operadores não podem comprometer duas vezes o mesmo fluxo.
3. **Serviço institucional**: mesmo `ServiceId`; não são inventadas transportadoras.
4. **Ativo construído**: interseção de `AssetPath`, incluindo o operador autorizado.
5. **Corredor físico saturável**: interseção de `ExperimentalSharedFreightCorridorKey` (aresta Goldberg + tecnologia + versão). A saturação suave tem custo não linear; separá-la indevidamente duplicaria capacidade e subestimaria o custo.

A componente conexa obtida por *union-find* é conservadora. Fontes diferentes que atendem o mesmo comprador continuam acopladas. Duas mercadorias do mesmo consumidor **jamais** são independentes apenas por serem produtos diferentes. Uma via operando com uma tecnologia distinta não é conectada apenas por coincidir em geometria, exceto se compartilhar ativo ou direito institucional efetivo.

## Seleção, limites e prova cruzada

Nova API: `CanonicalPrimitiveFreightPortfolio.SelectIndependentFreightComponents(...)`.

- Até **12 candidatos autênticos** no portfólio do mundo atual, versus oito na VS66.
- Cada componente mantém os limites VS66: **oito candidatos** e **4096 combinações**, com quanta e política explicitamente recebidos. Um componente maior ou que exceda o domínio finito **falha fechado**, sem produzir ótimo parcial.
- Quantidades e otimização dentro de cada componente são escolhidas pelo algoritmo determinístico VS66, que executa sua própria auditoria independente em índices de radix misto.
- O número de configurações globais possíveis é o **produto** das contagens de componentes (`BigInteger`); a quantidade efetivamente avaliada é a **soma**. Essa economia só é legítima sob a prova acima. Não se presume economia de processamento se toda a rede estiver conectada.
- As soluções locais são reunidas na prévia VS65 com orçamento H e congestionamento de **todos** os compradores e corredores; os encargos agregados são comparados por racionais exatas com a soma dos custos locais. O mesmo é feito com a soma das frações de demanda satisfeitas.
- `VerifyIndependent` reconstrói a prova com auditoria par-a-par de **todas** as fronteiras entre componentes e checagem de conectividade por BFS, reexecuta o auditor de cada solução VS66, recompõe os lotes e recalcula o custo/score globais. Alteração na sessão, rede ou save restaura invalida o resultado.

A otimização por componentes é exata **apenas no domínio produto dos componentes explicitamente selecionados**, para o objetivo laboratorial VS66 (somar frações atendidas, minimizar gasto, desempate canônico). Não é equilíbrio econômico, substituibilidade entre produtos ou um algoritmo de clearing/SETTLE real.

## Evidência e limites da medição

- Regressão real: dois compradores em tiles diferentes, dois ativos rodoviários independentes, dois direitos institucionais e duas mercadorias. Cada subproblema tem nove estados (quanta 0..8); o produto tem 81, mas a seleção pode resolver 9+9 estados sob limite **10 por componente**. O VS66 monolítico falha corretamente sob limite 10.
- Regressão de acoplamento: três mercadorias do mesmo comprador e ativo único permanecem num único bloco, com resultado idêntico ao VS66 original.
- Diagnóstico sintético de grafo: 12 candidatos independentes, 9¹² configurações hipotéticas, e conexões forçadas por mesmo comprador ou mesmo ativo. Esses dados **não são aceitos** como direitos institucionais nem como comércio verdadeiro.
- Falsificação de candidato, repetição, ausência de preços e limites insuficientes falham fechados; alterações de rede e sessão restaurada invalidam auditoria.
- Benchmark `--vs67-components`: 8/32/128 **consultas sobre um caso conectado fixo**. Confirma overhead e compatibilidade da nova API; **não** demonstra escalabilidade de cidades nem speedup no caso independente. As propriedades de decomposição são testadas separadamente.

GitHub Actions gate VS67 valida Linux e Windows, regressão direcionada e suíte integral, benchmarks Release, builds Godot Mono Release/Debug. Somente **todos** os workflows verdes no head final autorizam homologação técnica.

**Governança:** `CanAuthorizeRealSettlement=false`, `IsCommitted=false`. H permanece consumo monetário residual invisível, não depósito, banco ou empréstimo. Sem estoque fictício, contratos de transporte grátis, venda garantida por componente, mutação de save, fundação formal de cidade ou antecipação de liquidação.
