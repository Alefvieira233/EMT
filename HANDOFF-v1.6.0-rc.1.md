# Handoff v1.6.0-rc.1 — Apresentação 2026-04-28

**Status:** PRONTO. Build verde, testes 460/460, plugin instalado e validado.

---

## 1. O que mostrar na apresentação

### 🎯 Demo principal (10 minutos)

1. **Abrir Revit 2025** e mostrar o ribbon com 2 abas:
   - `Ferramenta EMT` — só fluxo PF
   - `Ferramentas ECC` — fluxo geral
2. **Fluxo PF — Bloco 2 Estacas** (totalmente novo do Victor):
   - `Ferramenta EMT > PF Armaduras > Aços Bloco 2 Estacas`
   - Selecionar bloco de fundação
   - Mostrar janela com 14 posições padronizadas (catalogue Tipo4)
   - Lançar armadura — comentários ficam padronizados (ex.: `"N1 - POS 1 - diam. 12.5 - C/15 - C=350 - U inferior"`)
3. **Fluxo PF — Estribos com RebarShape**:
   - `Ferramenta EMT > PF Armaduras > Estribos Pilar` (ou Viga)
   - Mostrar ComboBox carregando shapes do projeto Revit + preview visual
   - Modo "Automático" vs aplicar shape do projeto
4. **Fluxo metálico — Cortar Elementos**:
   - `Ferramentas ECC > Estrutura > Cortar Elementos` (Onda 3 PR-1)
   - Selecionar pisos + colunas/vigas → corte automático

### 🏆 Diferenciais para destacar

- **NBR 6118 real** — cálculo de ancoragem com eta1/eta2/eta3 de norma. Diferencia o plugin de qualquer concorrente.
- **Padronização de marcas** — `Comment` do Revit segue padrão consistente, alimenta schedules e CSV downstream sem virgulas perdidas.
- **460 testes automatizados** — qualidade industrial. Pegou bug de produção do Victor (culture-invariant em pt-BR).
- **Co-autoria** — mostrar o LICENSE 50/50 com Victor. Storytelling de colaboração técnica.

---

## 2. Comandos pra rodar antes/durante a demo

### A) Push pro GitHub (1 vez, antes da demo)

Abra um CMD ou Git Bash em `C:\Users\User\Downloads\FerramentaEMT\FerramentaEMT-Alef\` e rode:

```
git push origin main
git push origin v1.6.0-rc.1
```

Se pedir credenciais, use seu **personal access token** (GitHub agora exige token, não senha). Se não tiver, gere um em https://github.com/settings/tokens.

### B) Re-validar testes (opcional, ~2s)

```
cd /d "C:\Users\User\Downloads\FerramentaEMT\FerramentaEMT-Alef\FerramentaEMT.Tests"
dotnet test -c Debug --nologo
```

Esperado: **460/460 passando**. Se algum falhar, me avisa.

### C) Re-instalar (se algo der errado)

```
cd /d "C:\Users\User\Downloads\FerramentaEMT\FerramentaEMT-Alef\FerramentaEMT"
Compilar-e-Instalar.bat
```

### D) Gerar instalador novo (se já distribuiu o atual)

```
cd /d "C:\Users\User\Downloads\FerramentaEMT\FerramentaEMT-Alef\FerramentaEMT"
Gerar-Setup.bat
```

Resultado em `artifacts/installer/`.

---

## 3. Arquivos importantes pra ter à mão

| Arquivo | Pra que serve |
|---|---|
| `RELEASE-NOTES-v1.6.0-rc.1.md` | Texto pronto pra colar no GitHub Release |
| `CHANGELOG.md` (seção `[1.6.0-rc.1]`) | Detalhes técnicos completos |
| `comparacao-victor/ANALISE-VICTOR-WAVE2.md` | Storytelling do merge (caso queira mostrar processo) |
| `artifacts/installer/FerramentaEMT-Revit2025-Release.zip` | Distribuir pra alguém que queira testar |
| `artifacts/installer/setup-publish/FerramentaEMT.SetupBootstrapper.exe` | Instalador 1-clique pra demo em outra máquina |

---

## 4. Se algo der errado

| Problema | Solução |
|---|---|
| Revit não mostra abas | Conferir se `%AppData%\Autodesk\Revit\Addins\2025\FerramentaEMT.addin` existe. Se não, rodar `Compilar-e-Instalar.bat` de novo. |
| Build quebra de novo após edit | Olhar `bin/Release/net8.0-windows/` — DLL deve existir. Se vermelho de erro, comparar com último commit verde (`git diff HEAD~1`). |
| Plugin abre mas botão crasha | Olhar `%AppData%\FerramentaEMT\Logs\*.log` (Serilog) — vai ter stack trace. |
| Push falha "Authentication failed" | Token expirado ou faltando. Gerar novo em GitHub → Settings → Developer settings → Personal access tokens. |
| Setup.exe pede "Mais informações" SmartScreen | Normal porque não é digitalmente assinado. Clica "Mais informações" → "Executar mesmo assim". |

---

## 5. Pos-apresentação (próximas semanas → v1.7.0)

Lista priorizada de débitos a fechar:

1. **Re-portar zoneamento NBR 6118 no `PfRebarService`** (~2-3h)
   - Backup em `Services/PF/PfRebarService.cs.bak-alef-v1.5` (945 linhas)
   - Ativar quando `UsarEspacamentoUnico=false` (default)
   - Configs já estão em `PfRebarConfigs.cs`, só precisa religar a leitura no service

2. **Cleanup ADR-003 dos services PF** (~1h)
   - `PfRebarService.cs` linhas 155, 215 — remover `AppDialogService.Show*`
   - `PfTwoPileCapRebarService.cs` linhas 28, 82 — idem
   - Ambos retornam `Result<PfRebarResultado>` ou similar
   - Caller (`Cmd*.cs`) consome e mostra dialog

3. **Auditoria de UI da Wave 2** (~1h)
   - Verificar se as 4 Windows novas seguem `AppTheme.Base.xaml`
   - Confirmar que preview do RebarShape não vaza memória (220 px BitmapImage cacheada via `BitmapCacheOption.OnLoad`)
   - DPI overflow nas janelas (já fixed em outras, conferir nessas)

4. **Promover v1.6.0-rc.1 → v1.6.0 final** depois dos itens 1-3:
   - Bump `AssemblyVersion` 1.6.0-rc.1 → 1.6.0
   - Tag `v1.6.0`
   - Anunciar release final

5. **Sync com Victor**:
   - Mostrar a Wave 2 mergeada
   - Pedir pra ele fazer `git pull` em vez de mandar `.rar` na próxima
   - Se ele não usar Git, manter o ciclo `.rar` mas escrevê-lo no CONTRIBUTING.md

---

## 6. Resumo do que aconteceu hoje (2026-04-27)

- ✅ 8 ondas de merge executadas (4.906 linhas, 33 arquivos)
- ✅ 3 commits acima de v1.5.0:
  - `7db8c65` feat(pf): incorporacao Victor Wave 2
  - `72669a8` fix(pf): IsTwoPileCap + System.Drawing.Common (build fix)
  - `3f5dc04` fix(pf): culture-invariant ToComment (test fix)
- ✅ Tag local `v1.6.0-rc.1`
- ✅ Build Release: 0 erros, 7s
- ✅ Testes: 460/460 passando
- ✅ Plugin instalado e validado no Revit 2025
- ✅ Instalador distribuível gerado (3.8 MB ZIP + setup.exe)
- ✅ Memory entries do Claude atualizados pra próximas conversas
- ⏳ Push pro GitHub — depende do seu token, comando pronto acima

**Boa apresentação!** 🎯
