# Licenca em Desenvolvimento

## O que foi alterado

Foi habilitado um bypass permanente de licenca para desenvolvimento local.

- Em build `DEBUG`, o plugin sempre entra em `LicenseStatus.Development`.
- Em build `RELEASE`, a validacao normal de licenca continua ativa.

## Arquivos alterados

- [Licensing/LicenseService.cs](Licensing/LicenseService.cs)
- [Licensing/LicenseStatus.cs](Licensing/LicenseStatus.cs)
- [Views/AboutWindow.xaml.cs](Views/AboutWindow.xaml.cs)
- [Views/LicenseActivationWindow.xaml.cs](Views/LicenseActivationWindow.xaml.cs)

## Comportamento atual

- `DEBUG`: libera uso do plugin sem exigir trial ou licenca paga.
- `RELEASE`: exige licenca valida ou trial, como no fluxo original.

## Ponto tecnico principal

O bypass esta centralizado em `LicenseService.IsDevelopmentBypassEnabled()`:

- `#if DEBUG` retorna `true`
- `#else` retorna `false`

E o estado sintetico eh criado em `LicenseService.CreateDevelopmentState()`.

## Antes de gerar distribuicao

Quando eu pedir uma versao de distribuicao, revisar estes pontos:

1. Confirmar que a compilacao sera feita em `Release`.
2. Confirmar que o estado `Development` nao esta sendo usado em nenhum pacote final.
3. Validar o fluxo de ativacao/licenca no build final.
4. Testar a janela `Sobre` e a janela de ativacao para garantir que exibem o estado correto.

## Protecao contra conflito de manifestos

O instalador de distribuicao agora remove automaticamente o manifesto de desenvolvimento:

- remove `%AppData%\Autodesk\Revit\Addins\2025\FerramentaEMT.addin`
- instala o manifesto de distribuicao `FerramentaEMT.Distribuicao.addin`

Assim, o ambiente de teste nao fica com a variante `DEBUG` e a variante de distribuicao carregadas ao mesmo tempo.

## Observacao

Enquanto estivermos trabalhando no plugin, este comportamento em `DEBUG` eh intencional.
Nao usar build `DEBUG` como pacote de entrega ao cliente.
