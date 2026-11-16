# Notas de versão

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
