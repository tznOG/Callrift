# Publicar o Callrift no GitHub

## Prévia 1.4.0-preview.1

A prévia usa a tag `v1.4.0-preview.1` e deve ser marcada como **Set as a pre-release**, sem substituir a release estável. Use o título **Callrift 1.4.0-preview.1 — VPN somente para o Discord** e o corpo de `docs/releases/v1.4.0-preview.1.md`.

O empacotamento aceita versões estáveis e o sufixo `-preview.N`. Anexe o instalador, o ZIP portátil, o ZIP de fontes e `SHA256SUMS.txt`. Todos são gerados por `scripts/Package-Release.ps1`. Para preservar builds anteriores, use `-ApplicationDirectory` e `-OutputDirectory` com pastas separadas.

A prévia tem download manual. `releases/latest`, consultado pelo aplicativo, continua apontando para a última versão estável. A prévia reconhece a futura versão estável 1.4.0 como atualização.


O projeto está preparado como **arquivos locais, sem repositório Git ativo**. Commits e tags ficam para depois da revisão, com a identidade que você informar. Nenhum repositório remoto, push ou release foi criado. Os passos abaixo são um roteiro; execute os comandos de Git somente quando decidir prosseguir.

O repositório escolhido é `tznOG/Callrift`. Confira que está conectado à conta correta antes de criar ou enviar o projeto. O README usa links relativos para Releases e Issues, por isso acompanha o repositório escolhido.

## 1. Conferir a entrega

Abra `dist/Callrift/Callrift.exe` e revise a identidade visual. Para atualizar a versão que usava antes, saia dela pela bandeja. Caso usasse inicialização com Windows, desative na versão antiga e reative no Callrift.

Leia estes arquivos antes de publicar:

- `README.md`: página que os visitantes verão no GitHub.
- `docs/USAGE.md`: instruções completas e limites do aplicativo.
- `docs/releases/v1.3.0.md`: texto pronto da primeira release Callrift.
- `LICENSE`, `THIRD-PARTY-NOTICES.md` e `licenses/Disactivity-MIT.txt`: licença do projeto e créditos preservados.

Os PNGs da logo e do banner estão em `assets/brand`. As capturas de `docs` usam dados de demonstração.

## 2. Configurar a identidade e criar os commits depois

O nome do autor e o e-mail ainda precisam ser informados por você. Não use dados de outra conta conectada. Você pode escolher o endereço privado exibido em **GitHub → Settings → Emails**. A [documentação de e-mail de commits](https://docs.github.com/en/account-and-profile/how-tos/email-preferences/setting-your-commit-email-address) explica a associação com sua conta.

O Git portátil usado na preparação fica em `tools/mingit/cmd/git.exe`, fora dos arquivos de publicação. Quando aprovar os arquivos e quiser começar os commits, na raiz deste projeto:

```powershell
$git = Join-Path $PWD 'tools/mingit/cmd/git.exe'
& $git init -b main
& $git config user.name "SEU NOME"
& $git config user.email "SEU EMAIL DE COMMITS"
& $git config core.whitespace cr-at-eol
```

Se usar Git instalado no Windows, substitua `& $git` por `git`. A configuração acima vale apenas para este repositório. Se extrair o ZIP de código-fonte em outra pasta, use Git instalado, pois `tools` não acompanha esse ZIP.

Uma divisão sugerida para os primeiros commits, sem inventar um histórico de desenvolvimento:

```powershell
& $git add .gitignore LICENSE THIRD-PARTY-NOTICES.md build.ps1 release-config.json src tests assets licenses scripts/Create-Icon.ps1 scripts/Get-Engine.ps1 docs/USAGE.md
& $git diff --cached --stat
& $git commit -m "feat: add Callrift for Windows"

& $git add README.md CHANGELOG.md CONTRIBUTING.md SECURITY.md docs .github/ISSUE_TEMPLATE .github/pull_request_template.md
& $git diff --cached --stat
& $git commit -m "docs: add user guide and release documentation"

& $git add scripts/Package-Release.ps1 scripts/Get-InstallerCompiler.ps1 scripts/Build-Installer.ps1 scripts/Export-Source.ps1 installer .github/workflows
& $git diff --cached --stat
& $git commit -m "build: add release packaging and Windows CI"

& $git status
& $git log --oneline --decorate
& $git tag -a v1.3.0 -m "Callrift 1.3.0"
```

Revise o conteúdo antes de cada commit. A tag deve marcar exatamente o código que gerou o pacote testado. O ZIP `Callrift-1.3.0-source.zip` contém os arquivos do projeto, sem `.git`, commits, nome ou e-mail de autor Git configurado.

## 3. Criar o repositório vazio

Quando aprovar a publicação:

1. Entre na sua conta do GitHub e clique em **+ → New repository**.
2. Escolha o nome **Callrift**.
3. Sugestão de descrição: **VPN por processo para Discord e Presence nativo no Windows. Sua call. Outra rota.**
4. Escolha **Public** se quiser que qualquer pessoa veja o código e baixe as releases.
5. Deixe desmarcadas as opções de adicionar README, `.gitignore` ou licença. Esses arquivos já estão no projeto.
6. Clique em **Create repository**.

Guia oficial: [criar um repositório para seu projeto](https://docs.github.com/en/get-started/start-your-journey/creating-a-repository-for-your-project-on-github).

## 4. Enviar código e tag

**Estes comandos enviam o projeto ao GitHub. Execute somente depois da revisão e da sua decisão de publicar.** Na raiz do projeto:

```powershell
& $git remote add origin https://github.com/tznOG/Callrift.git
& $git push -u origin main
& $git push origin v1.3.0
```

Se estiver usando Git instalado, os mesmos comandos começam com `git`. Para enviar com autenticação no navegador, use Git for Windows instalado com Git Credential Manager; o MinGit portátil é suficiente para os commits locais, mas não inclui esse gerenciador. A autenticação ocorre pelo gerenciador do Git/GitHub; não coloque tokens nos arquivos ou na URL.

Se `origin` já existir, confira `git remote -v` e, se necessário, use `git remote set-url origin https://github.com/tznOG/Callrift.git` em vez de `remote add`. Não use `--force`.

A workflow **Windows build** executa a compilação e guarda um pacote candidato nos artefatos do Actions. Ela tem permissão de leitura, não cria releases, não envia commits nem publica versões automaticamente. Os testes da janela nativa são executados localmente; o runner do Actions apenas compila e empacota.

## 5. Preparar a apresentação

Na página do repositório, edite **About** com a descrição acima. Sugestões de tópicos: `windows`, `wpf`, `csharp`, `discord`, `wireguard`, `vpn`, `split-tunneling`.

O banner aparece no README. Para a imagem de compartilhamento de links, abra **Settings → General → Social preview** e envie `assets/brand/callrift-banner.png`; confira o recorte no GitHub antes de salvar. O mascote transparente pode ser usado como avatar de uma organização dedicada, se você criar uma depois.

Em **Settings → Security**, ative o recebimento privado de relatos de vulnerabilidade se quiser oferecer esse canal. O arquivo `SECURITY.md` explica o comportamento quando esse recurso não está disponível.

## 6. Criar a release como rascunho

1. Abra **Releases → Draft a new release**.
2. Selecione a tag já enviada **v1.3.0**.
3. Título: **Callrift 1.3.0 — Instalador e atualizações**
4. Cole o conteúdo de `docs/releases/v1.3.0.md` na descrição. O arquivo começa pelo parágrafo de apresentação; não repita o título da release no corpo.
5. Anexe `dist/Callrift-Setup-1.3.0.exe`, `dist/Callrift-1.3.0-Windows-x64.zip` e `dist/SHA256SUMS.txt`.
6. Clique em **Save draft** para revisar antes de tornar público.

O instalador é o download principal; o ZIP é a opção portátil. Ambos já incluem o motor, a DLL, o auxiliar, licenças e o arquivo-fonte do sing-box. Não anexe somente o executável interno `Callrift.exe`. O GitHub também oferece os arquivos de código-fonte da tag automaticamente; não substituem o ZIP pronto para Windows.

Confira o nome e a versão do ZIP, a descrição, o checksum e a instalação extraída. Depois, quando decidir disponibilizar o download, abra o rascunho e clique em **Publish release**. Os links **Baixar** do README passam a apontar para a release publicada.

Referência oficial: [gerenciar releases](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository).

A partir da 1.3.0, o Callrift consulta as releases de `tznOG/Callrift`. Publique versões estáveis com tag `vX.Y.Z`, marque como Latest e mantenha os nomes dos assets gerados. Rascunhos e artefatos do Actions não geram aviso no app. Consulte [como distribuir atualizações](UPDATES.md).

## 7. Próximas versões

Faça commits curtos que descrevam mudanças reais. Por exemplo:

```text
fix: keep profile selection after import
feat: add profile sorting
docs: clarify Discord restart behavior
```

Não é preciso criar muitos commits para parecer um projeto antigo. Evite juntar uma correção de roteamento, uma mudança visual e uma alteração de documentação sem relação no mesmo commit. Configure seu nome/e-mail antes de fazer novos commits se quiser mudar a identidade usada.

Para lançar uma próxima versão:

1. Atualize `src/AssemblyInfo.cs`, a versão exibida em `MainWindow.xaml`/`I18n.cs`, `CHANGELOG.md` e o nome do ZIP no README.
2. Escreva notas próprias em `docs/releases/vX.Y.Z.md`.
3. Compile, rode os testes e gere o pacote:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Package-Release.ps1
```

4. Revise o diff e faça o commit da versão. Crie uma tag anotada, por exemplo `git tag -a v1.3.1 -m "Callrift 1.3.1"`.
5. Só depois de aprovar, envie branch e tag e prepare o novo rascunho de release.

`Package-Release.ps1` não acessa o GitHub. Ele compila, roda os testes locais, confere arquivos e hashes dos componentes, recusa dados de usuário e gera o instalador, o ZIP e `SHA256SUMS.txt`. Se você já executou build e testes para os mesmos arquivos, pode usar `-SkipBuild -SkipTests` para apenas empacotar. Não use essas opções para alegar que verificou mudanças ainda não testadas.

Arquivos `.conf`, chaves, perfis, preferências, caches, `tools`, `dist` e `upstream` estão no `.gitignore`. Use `git status` e revise o que está sendo adicionado antes de cada commit. Releases devem conter apenas o pacote gerado, checksum e materiais públicos revisados.
