# Validação do Callrift 1.3.0

Resultado desta versão: **64/64 verificações do aplicativo** passaram com `tests/Run-Tests.ps1`. As respostas da API de releases foram simuladas para testar versões futuras sem publicar nada. A sessão real do Discord não foi reiniciada nem desconectada pelos testes.

A validação anterior do catálogo real passou com `-Network`; o catálogo não foi consultado novamente nesta rodada. Os testes de instalação usam identificação, atalhos e pasta exclusivos, sem instalar sobre a cópia do usuário ou usar seus perfis reais.

## Atualizações

- Configuração `tznOG/Callrift` incorporada ao executável, sem credenciais.
- Consulta pública `releases/latest`, User-Agent próprio, timeout de oito segundos e cancelamento no encerramento.
- Comparação numérica, versão igual/anterior, tags inválidas, rascunhos, pré-lançamentos, arquivo ausente e upload incompleto verificados.
- Escolha do instalador ou ZIP conforme o marcador da distribuição.
- Respostas 404, rate limit 403, JSON inválido, resposta excessiva e cancelamento verificados com um handler HTTP controlado.
- URLs fora do repositório e esquemas externos recusados; nenhum executável remoto é iniciado pelo aplicativo.
- Preferência padrão ativada para configurações antigas e desativação explícita preservada.
- Aviso e banner de uma versão futura fictícia renderizados; diálogo cancelado no teste, sem abrir downloads.
- Banner exercitado também no tamanho mínimo de 920 × 700. Temas e idiomas seguem as mesmas referências do restante da interface.

## Instalador

**16/16 verificações** passaram com `tests/Test-Installer.ps1`:

- Instalação por usuário em pasta isolada, registro de remoção no Windows, marcador e componentes.
- Criação do atalho de menu Iniciar e do atalho opcional na área de trabalho.
- Atualização de uma instalação de teste com o mesmo AppId, nova versão no registro e restauração do hash do motor.
- Bloqueio de instalação e remoção enquanto o mutex do aplicativo está presente, sem encerrar processos.
- Remoção do aplicativo, registro e atalhos após fechar o mutex de teste.
- Preservação de arquivos de dados isolados fora da instalação e de um arquivo desconhecido colocado nela.
- A versão portátil permanece sem marcador de instalação.
- O reconhecimento da tarefa de inicialização foi testado pelo caminho completo, recusando outra cópia e XML com entidades externas. Não foi criada nem removida uma tarefa real do usuário.

Os dois pacotes incluem os componentes e suas licenças. O compilador do instalador é baixado de origem oficial com hash fixo e extraído em modo portátil. O fluxo completo de alerta após uma publicação real dependerá da primeira release estável no repositório escolhido.

## Identidade e compatibilidade

- Executável e assembly públicos chamados `Callrift`, versão 1.3.0, com título e mascote carregados na janela real.
- PNG embarcado com 128 pixels de largura; banner e logo original não são carregados pela interface. ICO com sete tamanhos entre 16 e 256 pixels.
- Diretório de dados antigo preservado e coberto por teste. Não houve movimentação de perfis, favoritos ou preferências reais.
- Os identificadores internos da tarefa de inicialização e da instância única foram preservados para evitar tarefas e sessões duplicadas entre versões. O executável da tarefa precisa ser atualizado desativando e reativando a opção no app.
- Descrição **VPN para Discord** abaixo da marca no app, com tradução para inglês/espanhol. Banner, README e apresentação da release atualizados com a função explícita.
- Capturas do README vêm da execução nativa dos testes, com perfis fictícios.

## Interface

- A janela real foi aberta em modo normal por um processo de teste independente, com captura da área nativa e checagem de que a borda superior não estava branca.
- WindowChrome preserva redimensionamento e maximização, com cores da moldura definidas pelo tema.
- Lista de perfis com dados e seleção foi exercitada habilitada e desabilitada, sem fundo branco nativo no tema escuro.
- Dropdown do executável foi aberto, com inspeção do popup e de suas cores. Foram corrigidos também os nomes dos jogos, menu de idiomas, caixas de texto, checkboxes, barras de rolagem e diálogos.
- Tema claro/escuro, quatro páginas e tamanho mínimo de 920 × 700 foram exercitados. As páginas principais também foram verificadas em português, inglês e espanhol.
- Campos de perfil, nome de jogo e seleção de executável recebem cores explícitas do tema; a lista de perfis não herda o fundo branco do estado desabilitado do Windows.
- A página de conexão possui rolagem para preservar controles quando o conteúdo não cabe. O rodapé reserva uma coluna para o estado da VPN, evitando sobreposição por mensagens longas.

As imagens de inspeção em `tests/output` usam perfis e atividades de teste, sem credenciais reais.

## Reinício do Discord

- O diálogo de confirmação foi aberto e cancelado no teste da UI.
- O serviço rejeitou nomes de processos fora dos canais Discord suportados.
- Um processo auxiliar isolado, criado pelo teste, foi usado para verificar o encerramento autorizado. Um timestamp de início incorreto foi recusado; com a identidade correta, somente esse auxiliar foi encerrado.
- O Discord real do usuário não foi encerrado para validar o fluxo.
- O aplicativo solicita confirmação após verificar a VPN quando o Discord está aberto. A recusa mantém um aviso e uma ação de reinício. A reabertura ocorre somente após o encerramento e se a VPN continuar ativa.

## Comparador de perfis

- A configuração de teste passou pelo `sing-box 1.14.4 check` real. Não contém TUN, `auto_route`, regra por processo nem modificação do DNS global. Há somente proxy HTTP autenticado em loopback e endpoint WireGuard em userspace.
- Mediana, variação, resultados parciais, exclusão de rodadas antigas e ordenação por respostas concluídas/menor tempo foram verificados com amostras controladas.
- Persistência do histórico foi exercitada, sem gravar chaves privadas ou a configuração `.conf` nos resultados.
- Cancelamento antes de iniciar não criou sessão de teste.
- Um motor real isolado, com endpoint de documentação e chaves de teste, foi iniciado e cancelado; seus processos e o arquivo temporário de configuração foram removidos. Esse teste não cria adaptador nem usa perfil Proton real.
- Uma comparação ponta a ponta entre servidores Proton reais ainda depende dos perfis do usuário e do clique em **Testar perfis**, com a VPN desconectada.

A métrica apresentada é o tempo total de requisições HTTPS pelo túnel, não ping do jogo, latência exata de mídia do Discord, perda de pacotes ou velocidade de download. As três amostras usam a mesma sonda, com aquecimento/verificação de conectividade separado. O comparador não usa ICMP para inferir que uma VPN está funcionando.

## Presence, perfis e processos

- Catálogo real do Discord: **10.474 entradas**, sendo 10.473 com executável Windows compatível na ocasião da consulta. As quantidades podem mudar.
- Busca por nome/ID/aliases, termos separados por vírgula, favoritos, cache de dois dias e fallback offline foram exercitados.
- Início e parada pelos botões da UI, atividades simultâneas, seleção segura do executável, supervisão com Job Object e limpeza de diretórios temporários passaram.
- Perfis DPAPI, configurações persistentes, recusa de hooks de shell, caminhos inseguros e configurações WireGuard inválidas passaram.
- A configuração normal da VPN continua encaminhando Discord Stable/PTB/Canary/Development para WireGuard e os demais processos para a saída direta; passou na validação do motor.
- O Presence não consulta o GitHub do projeto original. A licença MIT do código adaptado está preservada em arquivo próprio do projeto, sem exigência do diretório `upstream` no build.

## Limites preservados

Não há kill switch nem proteção de DNS da Proton. Se o túnel cair, o Discord pode voltar à conexão direta. O roteamento por TUN também faz classificação local do tráfego dos outros aplicativos; não há promessa de impacto zero na latência dos jogos. O comparador usa processos adicionais apenas durante o teste manual, um perfil por vez; não permanece executando em segundo plano após terminar.

Ainda devem ser conferidos no ambiente de destino: criação/remoção de adaptador e rotas em falhas reais, tarefa de inicialização em login real, reinício dos canais Discord instalados e desempenho durante chamadas e jogos. O funcionamento confirmado pelo usuário na primeira versão não substitui esses cenários específicos.
