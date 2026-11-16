# Contribuir com o Callrift

Para relatar um problema, informe a versão do app, do Windows e do canal Discord. Descreva o que aconteceu e os passos para repetir. Capturas ajudam; revise nomes de perfis, IPs e dados pessoais antes de enviar. Não anexe arquivos `.conf`, chaves ou tokens.

## Preparar o ambiente

Use Windows x64 com .NET Framework 4.8. Clone o repositório e rode:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/Run-Tests.ps1
```

O build baixa os componentes de rede para `tools`, verifica os hashes e prepara `dist/Callrift`. Os testes usam dados isolados em `tests/output`, abrem janelas de teste e não conectam um perfil real. A opção `-Network` também consulta o catálogo público do Discord. A consulta de atualizações é testada com respostas HTTP simuladas, sem criar releases. Para validar instalação, atualização e remoção numa pasta e identidade isoladas, execute `tests/Test-Installer.ps1` após o build. Execute a suíte numa sessão gráfica do Windows.

O instalador é definido em `installer/Callrift.iss`; `release-config.json` configura a origem das atualizações. O compilador Inno Setup é extraído em modo portátil em `tools`.

O código usa C# 5 e o compilador do .NET Framework, sem pacotes NuGet. Mantenha essa compatibilidade. `src/MainWindow.xaml` define as páginas; `Controls.xaml`, os controles; `Core.cs`, os perfis e a configuração do motor; `Vpn.cs`, a sessão do túnel; `Activity.cs`, o Presence.

Para regenerar os ícones a partir do PNG:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Create-Icon.ps1
```

## Enviar uma mudança

Abra uma branch com um nome curto, como `fix/profile-selection`. Mantenha cada commit focado numa mudança real. Exemplos: `fix: preserve selected profile after refresh` e `docs: explain startup configuration`.

Na proposta, explique o problema, o comportamento resultante e como verificou a mudança. Para alterações visuais, inclua uma captura e confira os dois temas. Mudanças em rotas ou permissões precisam de testes que verifiquem o comportamento afetado.

Não inclua configurações VPN, dados de usuário, motores baixados, saídas de build ou o diretório de referência `upstream`. Preserve os avisos dos componentes adaptados e distribuídos. A [validação](docs/VALIDATION.md) descreve os cenários já exercitados e os que precisam de teste manual.
