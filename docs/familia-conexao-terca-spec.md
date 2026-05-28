# Especificação da Família "Conexão Estrutural — Terça"

**Versão:** 1.0 (publicada com v2.8.2)
**Status:** Convenção mandatória pra famílias usadas pelo comando "Inserir Conexão de Terça"
**Audiência:** projetistas que vão modelar famílias de conexão (chapas, cantoneiras, gussets, ressaltos) compatíveis com o algoritmo do plugin.

---

## 1. Por que esta spec existe

O comando "Inserir Conexão de Terça" (v2.8.2) usa um algoritmo **face-based** que:

1. Faz pick das terças + pick das vigas de apoio
2. Calcula o ponto de inserção projetando o endpoint da terça na curva da viga mais próxima
3. Extrai a **maior face planar** dos solids da terça (em perfis U/C/I = alma)
4. Insere a instância da conexão usando `NewFamilyInstance(face, point, direção, símbolo)` — orientação automática
5. Opcionalmente ajusta parâmetros de altura via raycast

Esse algoritmo **só funciona se a família segue a convenção descrita abaixo**. Famílias modeladas com convenção diferente podem:
- Sair invertidas
- Não respeitar `NewFamilyInstance(face, ...)`
- Ignorar o XYZ e ir pro GlobalPoint (caso WorkPlaneBased)
- Falhar em aplicar Modo Completo (parâmetros faltando)

Quando o algoritmo detecta divergência de posição, ele aplica `MoveElement` como guard defensivo — mas isso é fallback, não substituto pra convenção correta.

---

## 2. Escolha do template

| Template | Recomendado? | Quando usar |
|---|---|---|
| **Metric Structural Stiffener.rft** | ✅ Sim (padrão) | Chapas planas (rigidiz, gusset, ressalto) — comportamento face-based natural |
| **Metric Generic Model.rft** | ⚠️ Aceitável | Geometria complexa que não cabe em Stiffener (cantoneira com furação rica, conjuntos soldados) |
| **Generic Model face-based.rft** | ⚠️ Aceitável | Quando precisar de host explícito (uso avançado) |
| Outros (Beam, Column, Window…) | ❌ Não | Templates de família estrutural lineares ou hosted incompatíveis com `NewFamilyInstance(face, point, dir, symbol)` |

**Recomendação default:** `Metric Structural Stiffener.rft`.

---

## 3. Categoria

A família deve estar categorizada como:

- **Categoria preferida:** `Structural Connections` (`OST_StructuralConnections`)
- **Categoria aceita (fallback):** qualquer categoria cujo nome contenha "Conex" (PT-BR) ou "Connect" (EN-US)

> **Como o plugin filtra:** o comando busca `FamilySymbol` cuja `Category.Name.Contains("onex")`. Cobre PT-BR ("Conexões Estruturais") e EN-US ("Connections"). Se sua família ficar em categoria fora desse padrão, ela **não aparece no combo** da janela.

Pra configurar: Family Editor → **Properties** → **Family Category and Parameters** → escolha "Structural Connections".

---

## 4. Convenção de origem e eixos (CRÍTICO)

Esta é a parte mais importante da spec. Erros aqui causam inserção em posição/orientação errada.

### Origem

A **origem da família** (intersecção dos planos de referência de origem) deve estar:
- No **centro geométrico da chapa** (centróide), em XY
- Na **face que vai encostar na alma da terça** (a face hospedeira), em Z

Ou seja, quando o algoritmo insere `NewFamilyInstance(face_da_terça, ponto, direção, símbolo)`, a face da família que coincidir com a origem é a que vai grudar na alma.

### Eixos locais

| Eixo local | Direção desejada | Justificativa |
|---|---|---|
| **X local** (BasisX) | Apontando pra "frente" da terça (direção do eixo da terça) | O algoritmo passa `terça.GetTransform().BasisX` como direção de inserção. Se a chapa for modelada com X = frente, ela alinha naturalmente |
| **Y local** (BasisY) | Perpendicular à terça (no plano da alma) | Resulta do produto vetorial X × Z; consequência da convenção dos outros 2 |
| **Z local** (BasisZ) | Pra "fora" da terça (afastando da alma) | A face hospedeira tem normal = -Z local. NewFamilyInstance espera isso |

### Resumo visual

```
   Vista superior (XY):
   
        Y
        ↑
        |
   ─────┼─────→ X   (= direção do eixo da terça)
        |
        |
   
   Vista lateral (XZ):
   
              chapa (visualizada de canto)
                  |
   alma terça ────[FACE Z=0]───→  Z (pra fora)
                  |
        ↑
        X (direção terça)
```

> **Como verificar no Family Editor:** depois de modelar a chapa, abra "View 3D" → o **gizmo de eixos** no canto inferior esquerdo mostra os eixos locais. Confirme:
> - X aponta na direção que vai virar "direção da terça"
> - Z aponta saindo da face que vai grudar na alma
> - Y é perpendicular a esses

---

## 5. Geometria mínima

Uma chapa simples tem geometria suficiente pra funcionar. Detalhes mínimos:

1. **1 sólido principal** — `Extrusion` retangular paramétrica
2. **3 dimensões principais** controladas por parâmetros (ver §6)
3. **Origem da extrusão** = origem da família

A **face superior da extrusão** (Z+ local) deve ser a face de **maior área** entre todas as faces planares — porque o algoritmo busca a maior face planar do solid pra hospedar.

> **Por que a maior face importa:** o serviço executa
> ```csharp
> solids.SelectMany(s => s.Faces).OfType<PlanarFace>()
>       .OrderByDescending(f => f.Area).FirstOrDefault();
> ```
> Em uma chapa, isso é sempre a face superior ou inferior (a alma da chapa). O algoritmo aceita qualquer uma — mas é importante que **não haja outra face planar maior por acidente** (ex: se você modelou um ressalto enorme na lateral).

---

## 6. Parâmetros mandatórios

Tipo: **Length** (Comprimento). O plugin detecta automaticamente como mm via `SpecTypeId.Length`.

| Nome | Default sugerido | Range típico | Descrição |
|---|---|---|---|
| `Espessura` | 6 mm | 4-19 mm | Espessura da chapa |
| `Largura` | 100 mm | 60-200 mm | Largura paralela à direção da terça |
| `Altura` | 150 mm | 100-300 mm | Altura perpendicular à direção da terça (na vertical) |

**Visibilidade:** todos devem ser **parâmetros de Tipo** (não Instância), porque o usuário escolhe o tipo no combo da janela e os parâmetros aplicam ao símbolo.

**Como aparecem na janela do plugin:** o expander "Parâmetros da família" mostra TextBoxes editáveis pra cada um, com sufixo "mm". O usuário pode alterar antes de inserir.

---

## 7. Parâmetros opcionais — Modo Completo

Pra suportar o checkbox "Ajustar altura à viga (Modo Completo)" na janela:

| Nome | Tipo | Default | Descrição |
|---|---|---|---|
| `Altura_PlacaInf_a_Terca` | Length | 0 mm | Distância da chapa inferior até a face inferior da terça. O algoritmo calcula via raycast e seta esse valor |
| `Espesor_Viga_Principal` | Length | 0 mm | Espessura da viga de apoio. O algoritmo lê do tipo da viga (`h` retangular ou `tw` viga I) e seta |

> **Compatibilidade PT/ES:** o plugin faz lookup duplo — aceita `Altura_PlacaInf_a_Terca` (PT-BR) e `Altura_PlacaInf_a_Correa` (ES-LATAM). Família que usar qualquer um dos dois nomes funciona. Mesmo padrão pra `Espesor_Viga_Principal` (ES) e `Espessura_Viga_Principal` (PT-BR).

**Se a família NÃO tem esses parâmetros:** Modo Completo vira **no-op silencioso**. O comando funciona normalmente, sem ajuste de altura. Não há warning nem erro.

---

## 8. Parâmetros opcionais — Furação

Pra famílias que incluem furos paramétricos:

| Nome | Tipo | Descrição |
|---|---|---|
| `Diametro_Furo` | Length | Diâmetro dos furos (ex: 16 mm pra parafuso M16) |
| `Quantidade_Furos` | Integer | Número de furos |
| `Espacamento_Furos_X` | Length | Espaçamento entre furos na direção X local |
| `Espacamento_Furos_Y` | Length | Espaçamento entre furos na direção Y local |
| `Distancia_Borda` | Length | Distância do furo mais externo até a borda da chapa |

Esses parâmetros são opcionais e aparecem no expander dinamicamente se existirem.

---

## 9. Passo-a-passo no Family Editor

### Pré-requisito
- Revit 2025 aberto
- Modo Family Editor

### Procedimento (modelando uma chapa simples)

1. **Novo > Família** → escolha `Metric Structural Stiffener.rft`
2. **Properties > Family Category and Parameters** → categoria "Structural Connections"
3. **Vista padrão "Ref. Level"** → desenhe **Planos de Referência** na origem (se não existirem):
   - 1 vertical paralelo a X (`Center Front/Back`)
   - 1 vertical paralelo a Y (`Center Left/Right`)
4. **Cria os parâmetros de Tipo**:
   - Tab "Modify" → "Family Types" → "New parameter"
   - Adicione `Espessura`, `Largura`, `Altura` como Type / Length
5. **Modela a chapa** (Extrusion):
   - Tab "Create" → "Extrusion"
   - Vista de trabalho: "Front" ou "Ref. Level"
   - Desenhe um retângulo `Largura × Altura` centrado na origem
   - Set Extrusion `End` = `Espessura / 2`, `Start` = `-Espessura / 2`
   - Vincule as dimensões aos parâmetros (lock)
6. **Verifica os eixos**:
   - Vista 3D
   - Confirme: X aponta na direção que vai virar "direção da terça", Z aponta saindo da face hospedeira
7. **(Opcional) Adiciona parâmetros do Modo Completo**:
   - `Altura_PlacaInf_a_Terca` como Type / Length, default 0
   - `Espesor_Viga_Principal` como Type / Length, default 0
8. **(Opcional) Adiciona furação**: novo `Void Extrusion` cilíndrico, vinculado aos parâmetros de furação
9. **Family Types** → cria 2-3 tipos predefinidos:
   - Ex: `CHAPA_6x100x150` (Espessura=6, Largura=100, Altura=150)
   - Ex: `CHAPA_8x120x180`
10. **Save As** → `Conexão estrutural - terça.rfa` em `C:\Users\User\Documents\Famílias EMT\`
11. **Load into Project** → carrega no projeto de teste
12. **Testa o comando** → §10

---

## 10. Como validar

Antes de usar em projeto real, valide num projeto **simples e isolado**:

### Setup de teste
- Galpão simples: 2 vigas de apoio paralelas, 3 terças perpendiculares apoiadas nelas
- Vigas: perfil retangular (mais simples) ou W (testar viga tipo I)
- Terças: perfil U150x65x4.76 (caso típico EMT)

### Execução
1. Ribbon → SteelBIM Modelagem → Estrutura Metálica → **"Conexão Terça"**
2. PickObjects: seleciona as 3 terças → Enter
3. PickObjects: seleciona as 2 vigas → Enter
4. Janela: escolhe a família + tipo, marca "Extremidades", OK

### Checklist de validação visual

- [ ] **Posição XY:** conexão está no encontro terça-viga, não no meio do vão da terça
- [ ] **Posição Z:** conexão está sentada no topo da viga (não solta no ar nem cravada na viga)
- [ ] **Orientação:** chapa em pé, encostada na **alma** da terça (face plana grudada na alma)
- [ ] **Direção:** chapa orientada paralelamente à direção da terça
- [ ] **Sem duplicação:** em terças contínuas ou nós comuns, conexões não se sobrepõem
- [ ] **(Modo Completo, se ativo)** Altura da chapa ajustou ao topo da viga

### Se algo der errado

| Sintoma | Causa provável | Solução |
|---|---|---|
| Chapa deitada (no plano XY) | Eixos locais errados — Z não aponta "pra fora" da hospedeira | Reabra a família, gire 90° em torno de X ou Y, salve |
| Chapa rotacionada 180° | Mirrored flip não tratado | Trocar referência da BasisX, gerar tipo espelhado, OU rodar offset de rotação 180° na janela |
| Chapa entrando na viga em vez de sentada no topo | Origem da família não na face hospedeira | Recriar a origem coincidente com a face hospedeira da extrusão |
| Modo Completo não ajusta altura | Família não tem parâmetro `Altura_PlacaInf_a_Terca` ou `_a_Correa` | Adicionar parâmetro ou desativar Modo Completo |
| Conexão duplicada em nó comum | Tolerância XY 50mm insuficiente (terças muito próximas) | Aceitável; tolerância configurada pra cobrir casos típicos |
| Posição XY ligeiramente deslocada do esperado | Guard `MoveElement` corrigiu pra face | Comportamento esperado em famílias WorkPlaneBased — não é erro |

---

## 11. Variantes além da chapa simples

A mesma convenção (origem, eixos, categoria, parâmetros mandatórios) se aplica a:

- **Cantoneira (perfil L)**: maior face planar = uma das abas. Modele com a aba contra-a-alma como a face hospedeira; a aba contra-a-mesa fica perpendicular
- **Gusset (chapa trapezoidal)**: mesma chapa, geometria base trapezoidal em vez de retangular
- **Ressalto soldado**: chapa + nervura de reforço perpendicular; a maior face continua sendo a hospedeira
- **Conexões parafusadas**: chapa + voids cilíndricos pra furos; a maior face é a chapa, voids não afetam o algoritmo

Em todos os casos, **a face hospedeira é a maior face planar do conjunto**.

---

## 12. Localização e versionamento

- **Pasta canônica:** `C:\Users\User\Documents\Famílias EMT\Conexões\`
- **Naming convention:** `Conexão estrutural - <tipo>.rfa` (ex: `Conexão estrutural - terça.rfa`, `Conexão estrutural - cantoneira terça-viga.rfa`)
- **Versionamento manual:** quando alterar geometria, adicione sufixo de versão (`...rev2.rfa`) ou data
- **Backup:** mantenha cópia em `C:\Users\User\Downloads\FerramentaEMT\FerramentaEMT-archive\familias\` (fora do git por enquanto)

---

## 13. Histórico

| Data | Mudança |
|---|---|
| 2026-05-29 | v1.0 publicada junto com plugin v2.8.2 |

---

## 14. Referências

- Comando: `SteelBIM/Commands/CmdInserirConexaoTercas.cs`
- Serviço: `SteelBIM/Services/ConexaoTercasService.cs`
- Helpers de geometria: `SteelBIM/Utils/EngineerGeometry.cs`
- Filtros: `SteelBIM/Utils/StructuralBeamSelectionFilter.cs`, `StructuralFramingSelectionFilter.cs`
- Math puro testável: `SteelBIM/Services/ConexaoTercasGeometry.cs`
- Tests: `SteelBIM.Tests/Services/ConexaoTercasGeometryTests.cs`
