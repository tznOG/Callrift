# Callrift — VPN para Discord

Guia de uso do Callrift.

Aplicativo nativo WPF com WireGuard da Proton VPN e **Presence**, nosso módulo de atividades. Windows 10/11 **x64**, com .NET Framework 4.8. A interface, o gerenciamento de atividades e a manutenção do módulo fazem parte deste projeto; não é necessário instalar Disactivity nem consultar seus releases.

## Abrir

Escolha um dos downloads da release:

- **Instalador:** execute `Callrift-Setup-1.4.0-preview.1.exe`. A instalação é por usuário, em `%LOCALAPPDATA%\Programs\Callrift` por padrão. Cria um atalho no menu Iniciar e oferece um atalho opcional na área de trabalho. O Windows permite desinstalar pela lista de aplicativos.
- **Portátil:** extraia a pasta **inteira** `Callrift` e execute `Callrift.exe`. Mantenha o motor e o auxiliar de atividades nas subpastas originais. Os dados permanecem no diretório do usuário do Windows, também na versão portátil.

Ambas as distribuições ainda não têm assinatura de editor comercial. O instalador não ativa a VPN nem a inicialização com Windows automaticamente.

**Para atualizar:** saia da versão anterior pelo menu da bandeja antes de substituir a pasta do aplicativo. Perfis, favoritos e configurações permanecem em `%LOCALAPPDATA%\DiscordVPN`. Como o executável mudou de nome, desative a inicialização com Windows na versão antiga e reative no Callrift. Faça o mesmo se mudar a pasta do app.

1. Clique em **Gerar .conf na Proton VPN**, acima do botão de importar. Faça login na Proton, vá a **Downloads → WireGuard configuration**, escolha o servidor e baixe o arquivo. Guia oficial: https://protonvpn.com/support/wireguard-configurations .
2. Importe o `.conf` e selecione o perfil.
3. Clique em **Conectar VPN** e aceite o pedido do Windows para o componente do túnel. O app só anuncia conexão depois de uma requisição HTTPS bem-sucedida **pelo WireGuard**.
4. Se o Discord já estiver aberto, o app mostra uma confirmação para reiniciá-lo. **Somente depois de autorizar**, ele encerra os processos identificados e abre o mesmo canal do Discord novamente. Isso interrompe chamadas e transmissões em andamento. Se escolher **Agora não**, um aviso e o botão **Reiniciar Discord** continuam disponíveis. Se a VPN cair durante o reinício, o app não reabre o Discord automaticamente.
5. Para entrar no Windows já conectado, ative **Iniciar com o Windows**, **Conectar o último perfil** e, se quiser, **Abrir Discord depois de verificar a VPN**. Essa opção cria uma tarefa do Agendador para seu usuário, com permissão elevada. Mantenha o app no mesmo caminho. Desative a opção antes de mover a pasta.

O botão principal alterna entre **Conectar** e **Desconectar**. Fechar a janela mantém o app na bandeja por padrão; **Sair**, na bandeja, desconecta e encerra todas as atividades.

## Avisos de atualização

A **1.4.0-preview.1** é uma prévia com download manual. Ela não é oferecida automaticamente a usuários da versão estável. A prévia continua verificando as releases estáveis e poderá oferecer a 1.4.0 final quando estiver disponível.

O Callrift 1.3.0 ou mais recente consulta `https://api.github.com/repos/tznOG/Callrift/releases/latest` uma vez ao abrir, após um breve atraso. Se a release estável tiver uma versão maior e um arquivo compatível já enviado, mostra um aviso e um banner. A instalada oferece o instalador; a portátil oferece o ZIP. **Baixar atualização** abre o arquivo no navegador; o app não executa arquivos baixados.

Em **Configurações → Atualizações**, use **Verificar agora** ou desative **Verificar atualizações ao abrir**. Na inicialização pela bandeja, o aviso não força uma janela na tela; ele fica disponível quando você abre o app. Ausência de internet, release ainda não publicada e limite de consultas do GitHub não interrompem o aplicativo.

Antes de atualizar, use **Sair** na bandeja. O instalador pede que o app esteja fechado e substitui os arquivos na mesma pasta. Se usar a portátil, extraia a nova versão e substitua a pasta do aplicativo. Perfis, favoritos e preferências são preservados. Desinstalar preserva os dados e desativa somente a tarefa de inicialização que aponta para a instalação removida. A limpeza remove o componente de rede próprio quando ele não está em uso por outra cópia. Se necessário, o Windows pede permissão de administrador.

## Como o roteamento funciona

O Windows Filtering Platform seleciona os caminhos completos dos executáveis oficiais do Discord, com assinatura validada. `Discord.exe`, `DiscordPTB.exe`, `DiscordCanary.exe` e `DiscordDevelopment.exe` usam o adaptador privado do Callrift para TCP e UDP. Os demais aplicativos mantêm as rotas existentes e não atravessam o motor sing-box. A versão do navegador e clientes alternativos não estão incluídos.

O adaptador é criado sem captura automática das rotas do computador. As rotas auxiliares pertencem somente a ele; as rotas, métricas e configurações de DNS das outras interfaces são preservadas. Um driver original assinado da Proton faz o redirecionamento por aplicativo. O cliente Proton VPN não precisa estar instalado. Evite usar simultaneamente outro cliente VPN que controle o mesmo driver de isolamento.

O DNS compartilhado do Windows continua usando a configuração do sistema. O campo DNS do perfil Proton não é aplicado globalmente. Se o Discord instalar uma nova versão enquanto a VPN estiver conectada, desconecte e conecte novamente o Callrift para reconhecer o novo executável e autorize o reinício do Discord.

**Sem kill switch:** ao desconectar, falhar o motor ou sair do app, o Discord pode voltar à internet normal. A configuração de inicialização conecta depois do login, não antes do boot; o app não bloqueia o Discord enquanto aguarda a VPN. Desative o auto-start do próprio Discord para usar a opção que o abre após a verificação do túnel. Reconexões de sessões de voz/vídeo já estabelecidas dependem do Discord. Não foi implementada reconexão automática de túnel em caso de queda.

O resultado depende da rota e da origem da restrição. Uma VPN não habilita recursos indisponíveis na sua conta do Discord.

## Comparar os perfis importados

Com a VPN desconectada, clique em **Testar perfis**. O app testa um perfil por vez para limitar o consumo durante a comparação. Cada motor de teste possui apenas um proxy local autenticado e o endpoint WireGuard: **não cria adaptador TUN nem altera a rota do Windows**.

O teste verifica acesso à internet pelo perfil e cronometra três requisições HTTPS a uma mesma sonda. A lista mostra a mediana em milissegundos, a quantidade de respostas e o melhor resultado da rodada. O tooltip inclui a data e a variação entre a amostra mais lenta e a mais rápida. A classificação prioriza os perfis com mais respostas concluídas e, entre eles, a menor mediana. Os resultados ficam salvos localmente. O botão **Cancelar teste** encerra a sessão de teste e preserva os resultados já concluídos.

**A métrica é tempo de resposta HTTPS pelo túnel**, incluindo negociação/conexão e resposta do servidor. Não é ping ICMP, ping do Discord ou do jogo, perda de pacotes nem teste de velocidade. Falha da sonda pode ser um bloqueio do destino; não prova que o arquivo VPN é inválido. O resultado varia conforme servidor, rota e horário.

O comparador fica desativado durante a VPN ativa. Usar a mesma chave WireGuard em duas sessões simultâneas pode deslocar a sessão do servidor e interromper a conexão existente.

## Presence

Módulo nativo mantido neste projeto, com catálogo direto da API pública do Discord e processos auxiliares próprios. A base de algumas funções foi adaptada do projeto MIT de holasoyender; os créditos estão em `licenses/Disactivity-MIT.txt`. A licença é preservada, mas o app não depende de seu executável, instalador, serviço ou fluxo de releases. O build também não exige o diretório `upstream`.

- Catálogos públicos de jogos e outros aplicativos detectáveis do Discord.
- Busca por nome, ID e aliases, com vários termos separados por vírgulas.
- Favoritos persistentes, destacados antes dos demais resultados.
- Paginação de 50 itens, seleção de executável Windows e seleção automática pelo caminho mais curto.
- Várias atividades simultâneas; filtro de atividades em execução, parada individual e parada de todas.
- Contador de tempo e progresso de 15 minutos, apenas indicativo; não valida missões ou recompensas.
- Processo auxiliar com janela Win32 fora da tela; os jogos reais não são baixados nem executados.
- Cache de dois dias, atualização manual e fallback offline. O catálogo só é carregado ao abrir a aba e é liberado da memória ao sair dela; atividades em execução continuam supervisionadas. Ícones têm cache em memória limitado a 96 miniaturas de 64 pixels e são carregados sob demanda, no máximo quatro downloads simultâneos.
- Temas claro/escuro e interface em português, inglês e espanhol. Mensagens técnicas e registros usam português.

A interface usa temas próprios também nas listas, menus de seleção, caixas de texto, checkboxes, barras de rolagem e diálogos. A moldura usa WindowChrome e acompanha o tema. A janela suporta os três idiomas e possui rolagem para preservar os controles no tamanho mínimo. A detecção efetiva da atividade depende das regras e preferências do Discord, que podem mudar. Atualizações deste app são distribuídas no próprio pacote; não há consulta de versão ao repositório de outro aplicativo.

## Dados e permissões

Por compatibilidade com a versão anterior, os dados continuam em `%LOCALAPPDATA%\DiscordVPN`: configurações, favoritos, cache e perfis. Os `.conf` importados ficam criptografados com DPAPI para o usuário atual; não são copiados em texto puro. O arquivo original baixado continua na pasta em que você o salvou.

O worker do túnel solicita elevação ao conectar. A remoção de um componente de rede remanescente também pode solicitar elevação. A UI normal e o simulador de atividades não precisam de administrador. No auto-start autorizado, o Agendador inicia a UI elevada para evitar outro pedido de UAC; a abertura do Discord usa o Explorer como intermediário. O worker recebe apenas a configuração WireGuard validada e comunica por named pipe restrito ao usuário. A configuração do motor fica temporariamente em pasta com ACL restrita e é removida após o carregamento; há limpeza ao encerrar a sessão. Nenhum script do `.conf` é executado. Encerramento normal solicita saída graciosa do motor; um Job Object encerra processos dependentes em caso de falha do supervisor.

Sem coleta de uso. Há uma consulta à API pública do GitHub para atualizações (desativável), chamadas ao Discord para catálogo/ícones, ao `api.ipify.org` pelo túnel para conferir conectividade e ao `www.gstatic.com/generate_204` durante os testes de perfis. O painel mantém os últimos 250 eventos em memória. O diagnóstico local em `diagnostics/vpn-events.log` grava somente horários e identificadores de eventos, com rotação de aproximadamente 64 KB, sem chaves, endereços ou destinos de navegação. O histórico dos testes de perfil é salvo em `profile-tests.json`, sem configuração WireGuard nem chaves.

## Compilar e testar

No PowerShell, a partir da raiz do projeto:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Create-Icon.ps1
powershell -ExecutionPolicy Bypass -File build.ps1
powershell -ExecutionPolicy Bypass -File tests/Run-Tests.ps1 -Network
```

O compilador C# do .NET Framework faz parte do Windows; não é necessário instalar SDK .NET, Rust ou Node. O script baixa versões fixas do motor/driver de suas origens oficiais e verifica SHA256 antes de usá-las. Para gerar o pacote:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Package-Release.ps1
```

Consulte a documentação do repositório para o escopo de validação e os avisos de terceiros que acompanham o pacote.

## Remover o componente da versão portátil

Use **Sair** na bandeja antes de excluir a pasta portátil. O encerramento normal remove o serviço de rede e sua cópia protegida do driver. Se houve encerramento abrupto, execute `Callrift.exe --network-cleanup` antes de excluir a pasta; aceite a confirmação do Windows. Essa limpeza não apaga perfis ou favoritos e não interrompe outra sessão ativa do Callrift.