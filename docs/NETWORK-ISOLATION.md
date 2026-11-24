# Isolamento de rede por aplicativo

A prévia 1.4.0 substitui a captura global do adaptador TUN pela seleção dos executáveis do Discord no Windows Filtering Platform (WFP).

## O que mudou

Na 1.3.0, `auto_route=true` encaminhava o tráfego do computador ao motor. A saída `direct` devolvia o tráfego dos demais aplicativos à conexão comum, mas eles ainda dependiam desse motor. A prévia usa `auto_route=false` e filtros por caminho completo do executável, com validação da assinatura do Discord.

- O driver original assinado da Proton redireciona TCP/UDP selecionado para o adaptador privado.
- Os demais processos usam suas rotas existentes; filtros de autorização impedem que utilizem o endereço privado como saída alternativa.
- Rotas auxiliares de métrica 60000 pertencem somente ao adaptador do Callrift. Rotas, métricas e DNS existentes não são alterados.
- Encaminhamento IP e weak-host ficam desabilitados somente no adaptador privado. A pilha gVisor evita uma exceção persistente no firewall.
- O motor encaminha a entrada privada para WireGuard. A seleção de executáveis já ocorreu no Windows; não há segunda consulta ao processo no primeiro pacote.
- A sessão WFP é dinâmica e desaparece quando o worker termina. Um Job Object supervisiona o motor. O encerramento normal remove o serviço manual próprio e sua cópia do driver.
- A desinstalação pode limpar componentes remanescentes de encerramento abrupto. Verifica o caminho, tipo de serviço, hash e ausência de sessão ativa; não remove serviços de outros clientes VPN.

## Validação de rede

Os ensaios foram executados em Windows x64, com IPv4 e IPv6 nativos. Relatórios brutos ficam em `tests/output`, fora do Git e dos pacotes.

No ensaio com executáveis de teste e um perfil Proton:

- TCP e UDP selecionados funcionaram pelo túnel em IPv4 e IPv6, com saída pública diferente da conexão direta.
- O programa direto completou 130 verificações TCP e UDP em IPv4 e 127 em IPv6, sem falhas nem reconexões.
- Novas conexões diretas mantiveram a saída nativa. Uma tentativa TCP de usar explicitamente o endereço privado por outro executável foi bloqueada.
- Encerrar abruptamente o motor interrompeu as conexões selecionadas; as conexões diretas sobreviveram.
- Rotas, DNS, interfaces existentes e firewall foram comparados antes, durante e depois do teste.
- A limpeza do ensaio removeu adaptador, serviço e arquivo do driver; o snapshot WFP ficou sem objetos Callrift.

Na chamada real, houve reinício autorizado do Discord e confirmação de compartilhamento com áudio e imagem funcionando. Em 8 minutos de observação, incluindo a preparação da chamada, os probes diretos completaram 464 verificações TCP e 464 UDP por família de IP, sem falhas. As amostras não encontraram TCP externo do Discord pela conexão direta nem TCP de outros programas no túnel. Rotas e DNS existentes permaneceram iguais.

Endpoints UDP abertos no adaptador por serviços de descoberta não demonstram transmissão pelo túnel. Consultas de um executável não selecionado responderam pela saída normal e expiraram quando vinculadas ao endereço privado; o timeout isolado não identifica qual filtro descartou o pacote.

## Escopo da prévia

Os resultados descrevem o ambiente ensaiado, não todas as configurações de Windows e rede. Mudanças de interface, instalação de uma nova versão do Discord durante a sessão e redes sem IPv6 nativo ainda precisam de ensaios adicionais. Os filtros representam os executáveis descobertos ao conectar: reconecte a VPN após atualizar o Discord.

O DNS compartilhado continua usando a configuração do sistema. Não há kill switch persistente nem reconexão automática: ao encerrar o túnel, o Discord pode voltar à conexão comum. Navegadores não fazem parte da seleção. Não use simultaneamente outro cliente VPN que controle os mesmos callouts da Proton.

## Reproduzir

`tests/Prepare-NetworkIsolationTests.ps1` prepara os executáveis. Em uma sessão administrativa dedicada, `tests/Invoke-NetworkIsolationTest.ps1` executa o ensaio; `-UseProton` usa o perfil selecionado e `-TestIPv6` exige IPv6 nativo. `-DurationSeconds 120` amplia a observação. Esses testes alteram temporariamente componentes de rede e devem ser executados fora de uma sessão de uso do Callrift.

`tests/Remove-NetworkIsolationTestDriver.ps1` remove somente o serviço parado e arquivo esperados, verificando caminho, hash e ausência de sessão ativa.

## Referências

- [TUN do sing-box](https://sing-box.sagernet.org/configuration/inbound/tun/)
- [Implementação Windows do sing-tun](https://github.com/SagerNet/sing-tun/blob/040db71fcd33/tun_windows.go)
- [Interface e strong-host do Windows](https://learn.microsoft.com/en-us/windows/win32/api/netioapi/nf-netioapi-setipinterfaceentry)
- [Arbitragem WFP](https://learn.microsoft.com/en-us/windows/win32/fwp/filter-arbitration)
- [Driver Proton fixado](https://github.com/ProtonVPN/win-app/tree/a4e78deae2e123ee0ac1c48cc69a66647c5228da/src/ProtonVPN.CalloutDriver)
- [Fontes, licença e procedência do driver](DRIVER-SOURCE.md)
