# Validação do Callrift 1.4.0-preview.1

## Aplicativo

**70/70 verificações passaram** com `tests/Run-Tests.ps1`, usando o executável compilado da prévia.

- Configurações normais e do comparador verificadas pelo sing-box real.
- TUN sem captura automática, ABI nativa x64 e recusa de lista vazia de executáveis.
- Seleção de Discord com validação de assinatura; recusa de outro editor e de executável renomeado sem assinatura.
- Identidade do serviço de limpeza: somente o tipo, inicialização e caminho previstos são aceitos.
- Registro local limitado a identificadores de eventos, sem mensagens arbitrárias.
- Avisos de atualização com respostas simuladas: versões, arquivos, falhas HTTP, cancelamento, URLs externas e upload incompleto.
- Migração da prévia para a futura versão estável de mesma numeração; pré-releases não são oferecidas automaticamente.
- DPAPI, configurações, Presence, reinício autorizado de auxiliar de teste, supervisão de processos, comparador, três idiomas e renderização das quatro páginas.

Os testes automatizados usam dados próprios em `tests/output`. Não importam perfis reais, não encerram o Discord e não ligam o túnel do usuário.

## Rede

A seleção por aplicativo foi testada separadamente com um perfil Proton e dois executáveis de teste. TCP e UDP funcionaram pelo túnel em IPv4 e IPv6; conexões diretas sobreviveram ao encerramento abrupto do motor. Rotas e DNS existentes foram preservados. Uma chamada real confirmou compartilhamento com áudio e imagem.

Consulte [o relatório de isolamento](NETWORK-ISOLATION.md) para resultados, condições e procedimentos. Os ensaios de rede ocorreram antes dos ajustes finais de limpeza do serviço e empacotamento. A rodada final de publicação não interrompeu a sessão em uso.

## Instalador

**16/16 verificações passaram** com `tests/Test-Installer.ps1`.

O teste de instalação usa pasta, registro, atalhos e mutex exclusivos. Exercita instalação, atualização no mesmo local, recusa enquanto a sessão de teste está aberta, desinstalação e preservação de arquivos desconhecidos e dados separados. Não substitui a instalação em uso.

Quando outra sessão do Callrift controla o componente de rede, a limpeza o preserva. A remoção administrativa de um serviço remanescente depende de ele estar sem uso e com a identidade prevista; a desinstalação informa falha quando essa limpeza não pode ser concluída.

## Distribuição

O build fixa os hashes do motor, Wintun e driver da Proton. O driver mantém sua assinatura original. O pacote de fontes do driver tem 22 arquivos originais conferidos por SHA-256, além do manifesto e das notas de procedência.

Instalador, portátil e arquivo de fontes recebem checksums. As pastas de trabalho, configurações, chaves e resultados locais de testes ficam fora do Git e dos downloads. O instalador e o executável do Callrift não têm assinatura de editor.

A [validação da 1.3.0](validation/v1.3.0.md) permanece disponível como registro da versão anterior.
