# Fixtures DSTV — golden tests CHAPA

**Status:** Aguardando arquivos `.nc1` reais do fabricante.

## Arquivos esperados

- `CH02.nc1` — CHAPA 620×520×12,7 com 11 furos Ø21 + recorte de canto
- `CH03.nc1` — TBD (variante CHAPA)
- `CH04.nc1` — TBD (variante CHAPA)

## Como usar quando os arquivos chegarem

1. Salvar os 3 `.nc1` neste diretorio (sao binarios ASCII, line ending CRLF).
2. Marcar cada um como `Content` no `SteelBIM.Tests.csproj` com `CopyToOutputDirectory=PreserveNewest`.
3. Implementar `DstvWriterGoldenChapaTests` em `SteelBIM.Tests/Services/CncExport/`:
   - Montar um `DstvFile` equivalente ao CH02 (cabecalho + ContornoAk + Holes)
   - Assertar `DstvFileWriter.Write(f)` IGUAL byte-a-byte ao conteudo de `CH02.nc1`
4. Ajustar `DstvFileWriter` ate o golden passar (entrada do CH02 esta especificada
   no prompt da Etapa D do plano Onda 3).

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
