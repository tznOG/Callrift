# Notas de versão

## 1.4.0 — Atualização pelo aplicativo

- Publicação no canal estável, reconhecida pelo aviso de atualização da 1.3.0 e da 1.4.0-preview.1.
- Instalador e portátil com os nomes esperados pelo atualizador, preservando perfis, favoritos e configurações.
- Mantém o isolamento por aplicativo e as correções da prévia descritos abaixo.

Para receber o aviso, abra o Callrift com a verificação automática ativada ou use **Configurações → Atualizações → Verificar agora**. O botão oferece o download; a instalação continua sob controle do usuário.

## 1.4.0-preview.1 — Isolamento do Discord

- Seleção dos executáveis oficiais do Discord pelo Windows Filtering Platform, com assinatura validada antes de conectar.
- Adaptador privado sem captura global: os outros aplicativos mantêm suas rotas existentes e não atravessam o motor VPN.
- TCP e UDP pelo WireGuard, preservando o DNS e as configurações das demais interfaces.
- Encerramento com remoção das regras, do serviço manual e da cópia protegida do driver. Limpeza de componentes remanescentes pela desinstalação.
- Supervisão de sessão corrigida para evitar que o monitor de uma conexão antiga altere uma conexão nova.
- Diagnóstico local limitado a horários e identificadores de eventos, com rotação.
- Instalador e portátil para a prévia, com fontes e licenças dos componentes incluídos.
- Aviso preparado para a futura versão estável 1.4.0. Pré-releases continuam fora da atualização automática.

**Download manual:** esta versão é uma prévia. Saia da versão anterior pela bandeja antes de atualizar; perfis, favoritos e preferências são preservados.

## 1.3.0 — Instalador e atualizações

- Instalador por usuário com atalhos, atualização no mesmo local e desinstalador.
- Versão portátil mantida como download separado.
- Aviso ao abrir quando uma nova release estável de `tznOG/Callrift` estiver disponível.
- Verificação manual, opção de desativar a consulta automática e download conforme a distribuição.
- Atualizações preservam perfis e favoritos. A desinstalação também preserva os dados e desativa somente a inicialização vinculada à instalação removida.
- Dois downloads e seus hashes SHA256 gerados pelo empacotamento.

Os avisos estão disponíveis a partir desta versão. A primeira atualização de versões anteriores deve ser feita manualmente.

## 1.2.0 — Callrift

Primeira distribuição com o nome Callrift.

- Descrição **VPN para Discord** na interface, banner e apresentação do projeto.
- Nova identidade: mascote, ícone do executável, bandeja e banner do projeto.
- Aplicativo renomeado para `Callrift.exe`. Perfis, favoritos e preferências da versão anterior continuam disponíveis.
- VPN WireGuard para os processos do Discord, com confirmação antes de reiniciar um Discord já aberto.
- Comparador de perfis com três amostras HTTPS, mediana, variação e indicação do melhor resultado da rodada.
- Presence com catálogo, favoritos, busca, múltiplas atividades e auxiliares locais.
- Interface nativa com temas claro/escuro, controles de seleção próprios e três idiomas.
- Documentação de uso, build, validação e distribuição; pacote portátil com checksum SHA256.

**Ao atualizar:** desative a inicialização com Windows na versão antiga e reative no Callrift. O nome do executável mudou.

**Limites:** sem kill switch, reconexão automática ou proteção de DNS da Proton. O teste de perfis mede resposta HTTPS, não ping de jogos. Veja o [guia de uso](docs/USAGE.md).

## Antes do Callrift

O aplicativo foi desenvolvido e testado localmente como Discord VPN 1.0/1.1. Essas versões antecedem a preparação deste repositório e não representam releases públicos anteriores.
