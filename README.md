# Busca Servidor - Suporte — Guia completo

## Baixar o aplicativo

**[Baixar a pasta Aplicativo completa (.zip)](downloads/BuscaServidor.zip?raw=true)**

1. Clique no link acima para baixar o pacote completo.
2. Extraia o arquivo ZIP no computador onde fará a busca.
3. Abra a pasta **Aplicativo** e execute **BuscaServidor.exe**.

O pacote inclui o executável, o guia, o PDF ilustrado e as licenças. Não é necessário instalar o .NET. Requer Windows de 64 bits.

[Consultar o guia ilustrado em PDF](Aplicativo/Busca-Servidor-Guia-Ilustrado.pdf)

---

Versão 2.2.0 • Windows de 64 bits

O aplicativo encontra serviços MySQL/MariaDB e informa IP, porta, versão e ping para auxiliar a instalação dos terminais. **Não faz login no banco, não solicita usuário ou senha e não envia consultas SQL.** Confirmação de GERENTE e leitura de tabelas foram removidas.

## 1. Uso rápido

1. Abra **BuscaServidor.exe** no computador conectado à rede.
2. Mantenha **Usar faixa / IP manual** desmarcado.
3. Confira as portas; o padrão é `3306,3307,3308,3310,4387`.
4. Clique em **Buscar servidores**.
5. Aguarde a conclusão: os servidores aparecem assim que respondem, mas a busca continua para encontrar outras máquinas.
6. Selecione o resultado, use **Copiar IP** e anote também a porta.

Não é necessário instalar .NET, MySQL ou executar a busca como administrador. A alteração do firewall exige autorização administrativa separada. O programa não configura os terminais automaticamente.

## 2. Busca automática

O automático deixou de depender do cache ARP. Agora testa os endereços de `.1` a `.254` do bloco `/24` de **cada placa IPv4 ativa**, inclusive adaptadores VPN. Para máscaras menores, como `/25`, respeita os limites reais da sub-rede. Placas desconectadas, loopback e endereços de autoconfiguração `169.254.x.x` não geram faixas automáticas.

Exemplo: uma placa em `192.168.1.20/24` e outra em `192.168.2.30/24` fazem o automático testar as duas redes. A placa selecionada na lista não limita o modo automático; serve como referência para a faixa manual e para Força bruta.

IPs e portas são testados em paralelo, com até **512 conexões simultâneas**. Não há ping obrigatório antes da conexão. O aplicativo usa uma única conexão por par IP/porta para ler a apresentação inicial do protocolo, sem autenticar nem enviar comandos.

Há apenas uma linha e um ping por IP identificado. Depois de encontrar um serviço nesse IP, novas tentativas ainda não iniciadas podem ser ignoradas; tentativas já em andamento terminam dentro do prazo. A busca continua nos outros IPs. Caso existam várias instâncias no mesmo IP, é exibida a primeira que responder, não necessariamente a menor porta.

O ARP pode priorizar IPs conhecidos, mas a ausência no ARP não impede o teste. Cada nova busca repete os testes; não há reaproveitamento de resultados antigos.

**Tempo medido:** em 02/10/2026, uma busca de 255 IPs, incluindo loopback, nas cinco portas padrão terminou em 3,1 segundos e encontrou o servidor da rede. O tempo varia conforme quantidade de placas, portas, latência e espera configurada.

## 3. Campos e opções

| Campo ou opção | Função |
| --- | --- |
| **Placa de rede** | Mostra nome, IPv4 e faixa. Define a rede de referência para Força bruta e sugere o campo manual. O automático consulta todas as placas ativas. As interfaces são atualizadas a cada busca. |
| **Usar faixa / IP manual** | Habilita o campo ao lado. Marcado, Buscar servidores usa os IPs/faixas informados, com ou sem a opção Força bruta. |
| **Campo de IP / faixa** | Aceita IPv4 completo, CIDR ou vários itens separados por vírgula. Exemplos: `192.168.1.143` e `192.168.1.0/24`. |
| **Portas** | Portas TCP do serviço. Padrão: `3306,3307,3308,3310,4387`. Aceita até 20 entradas, de 1 a 65535. Separe por vírgulas. |
| **Espera (ms)** | Prazo do ping e referência para o prazo total da conexão TCP e leitura da apresentação do servidor. Padrão: 1000 ms. Aceita 300 a 10000 ms. O TCP arredonda para cima em segundos: 300 ms vira 1 s; 1500 ms vira 2 s. Não representa o tempo total da busca. |
| **Testar também 127.0.0.1 (este computador)** | Permite testar os IPs locais no automático/Força bruta e acrescenta loopback. No modo manual, os IPs explicitamente informados são testados mesmo que esta opção esteja desmarcada. |
| **Força bruta** | Marque e clique em Buscar servidores para percorrer a sub-rede completa da placa selecionada ou a faixa manual. Desmarcado, a busca automática cobre todas as placas IPv4 ativas. Não tenta usuários ou senhas. |
| **Consultar VPN** | Acrescenta vizinhos e rotas individuais conhecidos dos adaptadores VPN e mostra a identificação estimada da VPN. O automático já cobre o bloco `/24` das placas VPN ativas mesmo com esta opção desmarcada. |

O cabeçalho **AUTOMÁTICO** indica que o campo manual está desmarcado. **FAIXA / IP MANUAL** indica que os endereços informados serão usados. A mensagem de andamento e o relatório informam o escopo efetivamente executado.



## 4. Botões

Buscar servidores e Cancelar ficam abaixo das opções da busca. Copiar IP e Salvar relatório ficam no cabeçalho dos resultados. Desativar firewall aparece separado à direita, com destaque de atenção e a confirmação existente.

**Atalhos:** Enter inicia a busca quando disponível; Ctrl+C copia o IP selecionado quando o foco está na tabela. O botão direito sobre uma linha abre as ações de copiar IP e salvar relatório. Clique nos títulos das colunas para ordenar; use Tab para navegar entre os controles.


| Botão | Função |
| --- | --- |
| **Buscar servidores** | Inicia a busca automática em todas as placas ou usa a lista/faixa manual. Substitui os resultados da busca anterior. |
| **Cancelar** | Interrompe as tentativas em andamento. Mantém os resultados já obtidos e informa que a busca ficou parcial. |
| **Copiar IP** | Copia somente o IP selecionado. Anote a porta separadamente. |
| **Salvar relatório** | Salva um arquivo `.txt` com escopo, resultados e diagnósticos disponíveis. Salve antes de iniciar uma nova busca se quiser preservar os resultados anteriores. |
| **Desativar firewall** | Abre a confirmação para desligar os perfis do Firewall do Windows deste computador. Veja a seção 7. |

A barra de progresso aparece apenas durante uma operação. O estado permanece visível no rodapé quando o aplicativo está pronto ou a operação termina.

Durante a busca, as configurações ficam bloqueadas e Cancelar permanece disponível. Durante a operação administrativa de firewall, aguarde a confirmação/elevação do Windows; Cancelar da busca não cancela essa operação.

### Força bruta: alcance e limites

O automático usa o bloco `/24`, como o BAT. Força bruta usa a máscara real da placa selecionada: em uma rede `/23`, por exemplo, pode alcançar dois blocos de 256 endereços.

São percorridos todos os endereços válidos da faixa até o último host, continuando mesmo depois de encontrar um servidor. Não são enviados testes ao endereço de rede ou broadcast. Em `/24`, `.0` e `.255` não são hosts; em máscaras maiores, alguns endereços terminados assim podem ser válidos e são incluídos.

O limite é de 4096 IPs, além do loopback quando aplicável. Redes enormes, como VPNs `/8`, não são expandidas por Força bruta: o programa pede uma faixa manual menor. Não há varredura de todo o espaço IPv4.

## 5. Busca manual e endereços especiais

| Exemplo | Resultado |
| --- | --- |
| `192.168.1.143` | Testa esse IP. |
| `192.168.1.143,192.168.1.150` | Testa os dois IPs. |
| `192.168.1.0/24` | Testa os hosts da faixa. |
| `192.168.0.0/23` | Testa os hosts de uma rede maior. |

O campo aceita faixas de `/20` a `/32`, dentro do limite de endereços. Use IPv4 completo e sem zeros extras: `192.168.1.10`, não `192.168.001.010`. IPv6 não é consultado nesta versão.

**127.0.0.1 é o próprio computador.** Não use esse IP em outro terminal para apontar para o servidor. **120.0.0.1 não é localhost** e é bloqueado como provável erro de digitação.

## 6. Consultar VPN

Marque **Consultar VPN** e inicie a busca. A aplicação consulta adaptadores atuais, vizinhos ARP e rotas para IPs individuais (`/32`). Acrescenta endereços conhecidos e associa os resultados à indicação da VPN.

O índice da interface e a rota preferida pelo Windows são considerados para evitar confundir adaptadores com faixas sobrepostas. Adaptadores reconhecidos mas desconectados aparecem no inventário, sem gerar candidatos adicionais. A aplicação não conecta, desconecta ou configura VPNs.

Algumas VPNs não expõem todos os participantes ao Windows. Se o servidor estiver fora do bloco `/24` automático e não aparecer nos vizinhos/rotas individuais, informe seu IP ou uma faixa pequena no modo manual. Não encontrar peers conhecidos não significa que a VPN não tenha servidores.

### Identificação pelo nome ou descrição

Nome e descrição têm prioridade sobre o prefixo. São reconhecidos Hamachi, Radmin VPN, Tailscale, OpenVPN, WireGuard, SoftEther, ZeroTier, Fortinet/FortiClient, Cisco AnyConnect/Secure Client e PANGP/GlobalProtect.

TAP-Windows indica possível OpenVPN. Wintun sozinho indica um driver compartilhado, não confirma WireGuard nem OpenVPN. VPNs ou túneis sem nome conhecido ficam com produto indeterminado.

### Indicação somente pelo IP

| Prefixo | Indicação |
| --- | --- |
| `25.` | Hamachi provável; alta pelo prefixo. |
| `26.` | Radmin VPN provável; alta pelo prefixo. |
| `100.64.0.0` a `100.127.255.255` | Tailscale possível; média pelo prefixo. |
| `10.8.`, `10.9.`, `10.10.`, `172.27.` | Possível OpenVPN. |
| `10.13.13.`, `10.14.` | Possível WireGuard. |
| `192.168.30.` | Possível SoftEther VPN. |
| `10.147.`, `10.241.` | Possível ZeroTier. |

Os prefixos não são exclusivos: podem representar outras VPNs, CGNAT ou uma LAN comum. A identificação não comprova sessão VPN autenticada nem o caminho de cada pacote.

## 7. Desativar firewall

1. Clique em **Desativar firewall**.
2. Leia a janela. **Cancelar** fecha sem alterar nada. **Confirmar** solicita a operação.
3. Autorize a elevação administrativa do Windows, se apresentada.
4. Confira o resultado em **Detalhes e diagnóstico**.
5. Depois do teste, reative os perfis necessários pela **Segurança do Windows**. O aplicativo não apresenta botão de restauração.

A operação desliga os perfis **Domínio, Privado e Público** do Firewall do Windows da máquina em que o aplicativo está aberto. As regras cadastradas são preservadas; não são apagadas nem individualmente reescritas. O computador fica sem essa proteção durante o teste.

O aplicativo preserva as regras e mantém um registro administrativo do estado anterior. Para reativar a proteção pela interface do Windows, confira cada perfil e a política do ambiente.

Isso não desativa o firewall do servidor remoto, do roteador ou de produtos de terceiros. Políticas da organização podem impedir ou sobrepor a alteração. Desativar o firewall local não garante encontrar um servidor que esteja bloqueado remotamente.

A busca nunca desativa o firewall automaticamente. Os testes de desenvolvimento dessa função usam estados simulados; não houve desativação real do firewall da máquina durante a validação.

## 8. Resultados, ping e diagnóstico

| Coluna | Significado |
| --- | --- |
| **IP DO SERVIDOR** | Endereço que respondeu com protocolo compatível com MySQL/MariaDB. |
| **PORTA** | Porta em que o serviço respondeu. |
| **LOCALIZAÇÃO** | Rede, este computador ou loopback; pode incluir indicação VPN. |
| **PING / IDENTIFICAÇÃO** | MySQL/MariaDB, ping em ms, TTL, sistema operacional estimado e indicação VPN. A versão anunciada pelo servidor fica nos detalhes. |
| **STATUS** | Serviço identificado ou resposta de erro reconhecida do servidor. |

Clique em uma linha para ver o relatório. **Serviço identificado** não confirma o nome do banco nem a existência de GERENTE: a versão 2.0 não acessa bancos ou tabelas.

Um único ping por servidor informa o tempo de ida e volta. TTL entre 65 e 128 indica “Windows”; de 1 a 64, “Linux/Unix”; outros valores são indeterminados. TTL pode ser alterado por roteadores/configurações, por isso não comprova o sistema operacional. Ping bloqueado não remove um serviço já encontrado.

## 9. Solução de problemas e impacto

| Situação | O que verificar |
| --- | --- |
| Nenhum serviço encontrado | Portas, placas ativas, espera, serviço e bloqueios de rede. Use Força bruta ou IP manual se o servidor estiver fora do `/24` local. |
| Porta aberta sem resultado | O programa exige apresentação válida do protocolo; outro serviço TCP não é tratado como banco. |
| Nenhum IPv4 disponível | Conexão das placas e configuração IPv4; tente um endereço manual. |
| Erro 1040 | Servidor atingiu limite de conexões. Encaminhe ao responsável e evite repetir tentativas. |
| Erro 1129 ou 1130 | IP bloqueado ou acesso remoto restrito pelo servidor. A aplicação não modifica essas restrições. |
| Erro 1045 ou 3159 inicial | O servidor enviou uma resposta de autenticação/acesso ou exigência de TLS antes de qualquer login do aplicativo. Essa resposta pode identificar o protocolo, sem acesso ao banco. |
| Operação de firewall recusada | Confira autorização de administrador, serviço do Firewall do Windows e políticas da organização. |

A descoberta apenas recebe a apresentação inicial do serviço. Não envia login, senha, SQL, manutenção ou comandos de alteração ao banco. Fechar a conexão antes da autenticação pode aparecer como conexão abortada em logs/contadores; o aplicativo não promete ausência de registros ou impacto zero em servidores sobrecarregados. Evite repetir buscas desnecessariamente.

O relatório pode conter IPs, nomes de adaptadores e versão dos serviços. Compartilhe conforme a rotina de suporte do ambiente.

## 10. Arquivos e referências

- **BuscaServidor.exe:** aplicativo portátil.
- **COMO-USAR.md:** este guia único.
- **SHA256.txt:** hash do executável para conferência de integridade.
- **Licencas:** avisos de licenciamento distribuídos.

Na pasta de desenvolvimento, `src` contém o código e `Relatorios` as evidências. A versão 2.0 remove o conector SQL e as rotinas de autenticação/consulta. Testes são excluídos do EXE distribuído.

Fontes dos critérios de VPN: [Hamachi](https://support.goto.com/nl/hamachi/help/the-logmein-hamachi-client-hamachi-c-hamachi-aboutclients), [Radmin VPN](https://www.radmin-vpn.com/help/), [Tailscale e CGNAT](https://tailscale.com/docs/concepts/tailscale-ip-addresses), [OpenVPN](https://openvpn.net/community-docs/creating-configuration-files-for-server-and-clients.html), [Wintun](https://www.wintun.net/). Comportamento dos perfis de firewall: [Microsoft — FirewallEnabled](https://learn.microsoft.com/en-us/windows/win32/api/netfw/nf-netfw-inetfwpolicy2-put_firewallenabled).