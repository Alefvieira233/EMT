# Fixtures DSTV — golden tests CHAPA

**Status:** ✅ Implementado. Os 3 `.nc1` reais do fabricante sao o oraculo do golden
test `DstvWriterGoldenChapaTests` (byte-a-byte). O `DstvFileWriter` foi alinhado ao
formato real do fabricante (colunas de largura fixa, unico `EN` no fim, blocos
ST/AK/BO validados).

## Arquivos

- `CH02.nc1` — CHAPA 620×520×12,7 com 11 furos Ø21 + recorte de canto (raio -13)
- `CH03.nc1` — variante CHAPA (fase 104, qtd 72)
- `CH04.nc1` — variante CHAPA (fase 101, qtd 36)

## Como estao ligados

1. Os 3 `.nc1` ficam neste diretorio (binarios ASCII, CRLF — ver `.gitattributes`).
2. Sao embutidos como `EmbeddedResource` no `SteelBIM.Tests.csproj` (preserva os bytes
   exatos, sem risco de normalizacao de fim de linha na copia).
3. `DstvWriterGoldenChapaTests` monta um `DstvFile` equivalente a cada um
   (cabecalho + `ContornoAk` + `Holes`) e assere `DstvFileWriter.Write(f)` IGUAL
   byte-a-byte ao recurso embutido.

## Entrada do CH02 (referencia rapida)

- `PieceMark="CH02"`, `OrderNumber="0"`, `DrawingNumber="CH02"`, `Phase="109"`
- `SteelQuality="A36"`, `Quantity=18`, `ProfileName="CH12.7X520"`, `ProfileType=B`
- comprimento=620.00, largura=520.00, espessura=12.70, raio=0, peso=99.695, area=2.116
- `ContornoAk`:
  ```
  [(0,380.48,0),(238.41,0,0),(620,0,0),(620,349.90,0),
   (362.90,349.90,-13),(349.90,362.90,0),(349.90,520,0),
   (0,520,0),(0,380.48,0)]
  ```
- `Holes` (11 furos face v, todos Ø21):
  ```
  (74.90,481.20),(140.28,231.93),(144.90,481.20),(199.60,269.10),
  (214.90,481.20),(258.92,306.27),(284.90,481.20),(581.20,74.90),
  (581.20,144.90),(581.20,214.90),(581.20,284.90)
  ```

## Escopo

Apenas CHAPA (code B). Vigas (I/U/L) ficam pra um conjunto de `.nc1` proprio do
fabricante — ver `// TODO` em `DstvFileWriter.cs`.

⚠️ O extrator de contorno (geometria Revit → ContornoAk no `DstvHeaderBuilder`)
e' Revit-bound: implementar quando os goldens passarem, marcar como pendente de
validacao no Revit pelo Alef.
