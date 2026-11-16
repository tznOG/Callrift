# Instalação e distribuição de atualizações

O Callrift é distribuído como instalador e ZIP portátil. Ambos usam o mesmo aplicativo e os mesmos dados em `%LOCALAPPDATA%\DiscordVPN`, preservado por compatibilidade.

## O que o usuário vê

Desde a versão 1.3.0, o app faz uma consulta em segundo plano ao abrir. Se encontrar uma versão estável mais nova e o arquivo correspondente estiver disponível, mostra **Atualização disponível**, com **Baixar atualização** e **Agora não**. Um banner permite voltar ao download depois. Quando iniciado oculto pelo Windows, usa o aviso da bandeja e apresenta o diálogo quando o usuário abre a janela.

- Instalada: oferece `Callrift-Setup-X.Y.Z.exe`.
- Portátil: oferece `Callrift-X.Y.Z-Windows-x64.zip`.
- **Configurações → Atualizações** permite consultar manualmente ou desativar a consulta ao abrir.
- O download abre no navegador. O Callrift não baixa executáveis em segundo plano, não executa o instalador e não impõe atualização obrigatória.
- Antes de instalar ou substituir arquivos, use **Sair** na bandeja. A VPN e as atividades serão encerradas pelo próprio app.
- As versões anteriores à 1.3.0 precisam ser atualizadas manualmente uma vez para receber esse recurso.

O instalador mantém o mesmo identificador entre versões, atualiza no mesmo caminho e preserva os dados. Não ativa a inicialização automática. Para migrar da versão portátil, desative **Iniciar com o Windows** na cópia antiga e reative na instalada. Na desinstalação, o app remove apenas a tarefa de inicialização que aponta para o executável desinstalado; não remove tarefas de outra cópia. Os dados do usuário permanecem.

## Origem e rede

`release-config.json` contém `tznOG/Callrift`. O build incorpora essa configuração ao executável. A opção `build.ps1 -Repository owner/repo` pode substituí-la para builds de outro repositório. A workflow usa o endereço do próprio repositório do GitHub Actions.

A consulta usa `GET https://api.github.com/repos/tznOG/Callrift/releases/latest`, sem autenticação, com timeout de oito segundos e limite de resposta de 1 MiB. Não transmite configurações VPN, nomes de perfis, favoritos ou tokens. O GitHub recebe o IP e o User-Agent da conexão como em qualquer acesso HTTP. A consulta não usa um serviço ou timer permanente: ocorre uma vez por execução e por ação manual.

A API de [latest release do GitHub](https://docs.github.com/en/rest/releases/releases#get-the-latest-release) fornece a release pública marcada como mais recente. O cliente também ignora rascunhos, pré-lançamentos e tags fora do formato estável `vX.Y.Z`/`X.Y.Z`. Usa comparação numérica de versões e exige que o asset esteja enviado e tenha o nome esperado. URLs de download só são aceitas no repositório configurado. Respostas inválidas, ausência de release, falta de internet e limite de consultas não bloqueiam o uso.

## Publicar uma versão que gere aviso

1. Atualize as versões em `src/AssemblyInfo.cs`, `src/MainWindow.xaml` e `src/I18n.cs`, o README e o changelog.
2. Confira `release-config.json`. Neste projeto, mantenha `tznOG/Callrift`.
3. Compile e teste:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Package-Release.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/Test-Installer.ps1
```

4. Crie os commits e uma tag estável, por exemplo `v1.3.1`, na conta correta. Envie somente quando decidir publicar.
5. Prepare a release como rascunho com os arquivos **`Callrift-Setup-1.3.1.exe`**, **`Callrift-1.3.1-Windows-x64.zip`** e **`SHA256SUMS.txt`**.
6. Confira ambos os downloads e publique como release estável, marcada como **Latest**. Não marque como pré-lançamento se quiser que o cliente estável avise os usuários.

Os usuários da versão 1.3.0 receberão o aviso ao abrir novamente. Somente enviar código ou uma tag, criar um rascunho ou guardar um artefato no Actions não oferece atualização ao cliente. Não renomeie os assets: os nomes são verificados pelo app.

## Empacotamento

`scripts/Get-InstallerCompiler.ps1` baixa o [Inno Setup 7.1.1 oficial](https://jrsoftware.org/inno-setup/downloads), verifica SHA256 e usa seu [modo portátil](https://jrsoftware.org/inno-setup/documentation/topic_technotes.htm). Não instala o compilador globalmente. `installer/Callrift.iss` define a instalação por usuário, atalho no menu Iniciar, atalho opcional na área de trabalho, pré-requisito .NET 4.8 e desinstalador.

A instalação e a remoção pedem que o app esteja fechado; não encerram o Discord. O instalador e os binários do Callrift ainda não possuem assinatura de editor. O instalador não remove arquivos desconhecidos nem apaga a pasta de perfis. O ZIP portátil não possui o marcador `installed.marker`, usado para escolher o tipo de download.
