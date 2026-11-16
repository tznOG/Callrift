# Relatar uma vulnerabilidade

Falhas no tratamento de perfis, caminhos de arquivos, elevação ou comunicação entre processos merecem um relato privado. Se o repositório oferecer **Security → Advisories → Report a vulnerability**, use esse canal. O mantenedor precisa ativar o recebimento privado nas configurações do GitHub.

Se a opção não estiver disponível, abra uma issue apenas pedindo um canal privado, sem publicar chaves, configurações, dados pessoais ou instruções de exploração. Não existe um endereço de segurança dedicado definido para este projeto.

Inclua a versão do Callrift, Windows, passos para reproduzir e o efeito observado. Use dados de teste. Nunca envie sua chave WireGuard, arquivo `.conf` real ou token do Discord.

Os perfis importados usam DPAPI para seu usuário do Windows. Isso não protege contra outro processo com acesso à mesma sessão. O arquivo original baixado da Proton continua sob seus cuidados. Consulte os limites de roteamento e permissões no [guia de uso](docs/USAGE.md).
