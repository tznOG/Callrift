# Fonte do driver de isolamento

O Callrift usa o binário original `ProtonVPN.CalloutDriver.sys` 1.0.5.0,
copyright (c) 2023 Proton AG, sem modificações. O pacote acompanha a
licença original e os arquivos de fonte do driver, incluindo projeto,
cabeçalhos, recursos e instruções de assinatura publicados pela Proton.
Os arquivos `.cab` pré-compilados não fazem parte do pacote de fontes.

- Repositório: https://github.com/ProtonVPN/win-app
- Revisão: `a4e78deae2e123ee0ac1c48cc69a66647c5228da`
- Fonte: `src/ProtonVPN.CalloutDriver`
- Binário: `Setup/Native/x64/ProtonVPN.CalloutDriver.sys`
- SHA-256 do binário: `ba788ffdc1bd95dd4aa0eccb1b43a5e512eae09fb2d8d623e7266bde65d79e00`
- Licença: GNU GPL versão 3; consulte o `LICENSE` original.

`SOURCE-SHA256.json` identifica a revisão e o hash de cada arquivo
original preservado. `BUILD-AND-PROVENANCE.md` é uma nota do Callrift;
não altera as instruções nem o código da Proton.

O projeto `.vcxproj` usa o toolset `WindowsKernelModeDriver10.0`, KMDF,
Windows SDK e WDK. Abra-o em um ambiente Visual Studio com esses
componentes e selecione `Release` / `x64`. O projeto habilita geração WPP
dos arquivos `.tmh`. Não há bibliotecas do restante do cliente Proton
referenciadas pelo projeto do driver. Os demais fontes originais da
revisão continuam disponíveis no repositório acima.

A assinatura do binário distribuído pertence à Proton AG. Uma compilação
local não reproduz essa assinatura e não substitui automaticamente o
binário aceito pelo Callrift: o aplicativo verifica o SHA-256 fixado. O
fluxo original de assinatura está em `src/ProtonVPN.CalloutDriver/README.md`.
A compilação local do driver não foi executada nesta validação; o teste
do Callrift utilizou o binário oficial assinado.
