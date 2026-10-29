# GA-VS08 — Contornos táticos rentes à superfície, sem paralaxe

Data: 29/10/2026. Corrige a separação visual observada no
Godot 4.7.2.mono no Windows durante o teste manual do GA-VS07.
Os anéis de origem (ciano), destinos possíveis (verde-lima),
destino escolhido (laranja) e hover apareciam **pairando**
sobre os hexágonos físicos quando vistos pela câmera tática
inclinada a 45 graus.

## Causa confirmada no código

A malha plana do tático tem **Y=0**. Os anéis preenchidos
do `TacticalContextPreview.WorkerRings.cs` eram
posicionados em **Y=0,012** e linhas normais do mapa em
**Y=0,006**. O deslocamento projetado no plano da tela
ficava visível devido à inclinação da câmera, mesmo que
os vértices X/Z coincidissem com o hexágono correto.
Não era problema de ID de tile, topologia nem de seleção.

## Mudança estritamente de apresentação

Os quatro níveis agora compartilham constantes em
`TacticalContextPreview.cs`, com terreno em Y=0:

| Camada | Elevação anterior | Nova elevação Y |
|---|---:|---:|
| Hexágonos físicos preenchidos | 0 | 0 |
| Linhas finas da malha (inclusive extensão) | 0,006 | 0,00012 |
| Seleção de inspeção do tile | 0,00030 | 0,00025 |
| Fronteiras estratégicas discretas | 0,00035 | 0,00034 |
| Contornos largos de trabalho ciano/laranja/verde | 0,012 | 0,00050 |

A ordem dos offsets permanece explícita para evitar
**z-fighting** (cintilação de faces coplanares), mas
todos são menores que 0,001 unidade de cena.
A elevação aparente dos anéis caiu **24 vezes**.
São deslocamentos de renderização, **não altitude**,
custo, posição física canônica nem geometria topológica.

- Os mesmos polígonos `_polygons` definem superfície,
  seleção, contorno do trabalhador e teste de clique.
- Não reintroduz bordas fortes nos setores estratégicos:
  as fronteiras seguem discretas, iguais para todos os
  territórios, sem esmaecimento de vizinhos.
- Não altera G(3,0)→G(18,0), workers, produção,
  consumo, estoques, tempo, cache, hash ou save.
- A HUD do laboratório também deixa de afirmar
  equivocadamente que não executa turnos/não salva:
  confirma o ciclo econômico offline a cada seis
  movimentos e o save próprio **não canônico**.

## Verificação

A CI `ga-vs08-grounded-overlays.yml` roda Windows e
Linux, compilando o projeto **Godot C# original**, regredindo
os testes de trabalhadores e save e verificando, via
contrato estático, os valores/camada e o emprego de
todas as constantes compartilhadas. O script não é
uma evidência de renderização em GPU real.

### Gate manual obrigatório no Windows

1. Abrir a cópia VS08 do projeto Godot original,
   selecionar um território, abrir contexto tático
   e iniciar laboratório neolítico.
2. Comparar os anéis ciano/verde com a superfície:
   devem coincidir com as bordas reais dos hexágonos,
   sem linha brilhante deslocada acima/abaixo do tile.
3. Fazer zoom e pan, alternar yaw com **ROTATE ±15°**,
   ligar/desligar **TACTICAL TILE EDGES** e
   **TERRITORY LIMITS**. Verificar ausência de
   cintilação e de descolamento em todos os modos.
4. Selecionar vizinho: contorno laranja deve ocupar
   o destino correto. Fazer a transferência,
   avançar seis ticks e reabrir save, sem regressões
   de movimentos, estoques, consumo ou persistência.

**Não considerar a falha visual encerrada só por build/CI
passados**; o critério é a mesma GPU Windows/Intel UHD
que revelou o problema.

## Próximo ROI

Retornar à migração progressiva do estado econômico
físico para o `WorldState` canônico (com eventos,
hash e saves versionados). Não introduzir instalações
obrigatórias no Neolítico, coeficientes de investimento
sem validação, cidades ou novos serviços de HA.

**MKT-00** pode continuar em preparação leve;
**MKT-01** não é liberado só por este ajuste visual.
