using System;
using System.Collections.Generic;
namespace Callrift {
    public static class I18n {
        public static string Language = "pt-BR";
        static Dictionary<string,string[]> strings = new Dictionary<string,string[]>();
        static void A(string key,string text) { strings[key] = text.Split('|'); }
        static I18n() {
            A("BrandDescription","VPN para Discord|VPN for Discord|VPN para Discord");
            A("Updates","Atualizações|Updates|Actualizaciones");
            A("AutoUpdates","Verificar atualizações ao abrir|Check for updates on startup|Buscar actualizaciones al abrir");
            A("CheckUpdates","Verificar agora|Check now|Buscar ahora");
            A("GetUpdate","Baixar atualização|Download update|Descargar actualización");
            A("UpdateReady","Receba avisos de novas versões do Callrift.|Get notified about new Callrift versions.|Recibe avisos de nuevas versiones de Callrift.");
            A("UpdateHint","Uma nova versão do Callrift está disponível.|A new Callrift version is available.|Hay una nueva versión de Callrift disponible.");
            A("UpdateTitle","Atualização disponível|Update available|Actualización disponible");
            A("UpdateVersion","Callrift {0} disponível|Callrift {0} is available|Callrift {0} disponible");
            A("UpdateMessage","Você está usando a versão {0}. A versão {1} já pode ser baixada.\n\nAbra o download no GitHub. Saia do Callrift pela bandeja antes de instalar ou substituir os arquivos. Seus perfis e favoritos serão preservados.|You are using version {0}. Version {1} is ready to download.\n\nOpen the download on GitHub. Exit Callrift from the tray before installing or replacing files. Your profiles and favorites will be preserved.|Usas la versión {0}. La versión {1} ya está disponible.\n\nAbre la descarga en GitHub. Sal de Callrift desde la bandeja antes de instalar o reemplazar archivos. Se conservarán tus perfiles y favoritos.");
            A("UpdateChecking","Verificando novas versões…|Checking for new versions…|Buscando nuevas versiones…");
            A("UpdateCurrent","Você está usando a versão mais recente publicada.|You are using the latest published version.|Usas la versión publicada más reciente.");
            A("UpdateUnpublished","Nenhuma atualização estável compatível foi publicada ainda.|No compatible stable update has been published yet.|Todavía no hay una actualización estable compatible.");
            A("UpdateUnavailable","Não foi possível verificar agora. Você pode continuar usando o app.|Unable to check right now. You can keep using the app.|No se pudo comprobar ahora. Puedes seguir usando la app.");
            A("Native","Nativo e leve|Native and lightweight|Nativo y ligero");
            A("Connection","Conexão|Connection|Conexión"); A("Logs","Atividade|Activity log|Registro"); A("Settings","Configurações|Settings|Configuración");
            A("VpnTitle","Seu Discord, conectado.|Your Discord, connected.|Tu Discord, conectado.");
            A("VpnSubtitle","Uma rota para sua call. A conexão de sempre para seus jogos.|A route for your calls. Your usual connection for your games.|Una ruta para tus llamadas. Tu conexión habitual para tus juegos.");
            A("Route","ROTEAMENTO|ROUTING|ENRUTAMIENTO"); A("DiscordOnly","Somente Discord|Discord only|Solo Discord"); A("OtherApps","OUTROS APLICATIVOS|OTHER APPS|OTRAS APLICACIONES"); A("Direct","Conexão direta|Direct connection|Conexión directa");
            A("OpenDiscord","Abrir Discord|Open Discord|Abrir Discord"); A("YourProfiles","Seus perfis|Your profiles|Tus perfiles"); A("Profile","PERFIL|PROFILE|PERFIL"); A("Imported","IMPORTADO EM|IMPORTED|IMPORTADO");
            A("NoProfiles","Seu primeiro perfil começa aqui.|Your first profile starts here.|Tu primer perfil empieza aquí."); A("ImportHint","Gere um arquivo WireGuard na Proton e importe abaixo.|Create a WireGuard file at Proton and import it below.|Genera un archivo WireGuard en Proton e impórtalo abajo.");
            A("ImportConf","＋  Importar .conf|＋  Import .conf|＋  Importar .conf"); A("Remove","Remover|Remove|Eliminar"); A("Folder","Abrir pasta|Open folder|Abrir carpeta");
            A("StartWindows","Iniciar com o Windows|Start with Windows|Iniciar con Windows"); A("StartupHint","Use o último perfil automaticamente ao entrar no Windows.|Use the last profile automatically when you sign in.|Usa el último perfil automáticamente al iniciar sesión.");
            A("ActivityTitle","Sua atividade. Sua presença.|Your activity. Your presence.|Tu actividad. Tu presencia."); A("ActivitySubtitle","Presence: seu catálogo e suas atividades, em um módulo próprio do app.|Presence: your catalogue and activities, built into this app.|Presence: tu catálogo y tus actividades, en un módulo propio de la app.");
            A("Refresh","Atualizar|Refresh|Actualizar"); A("AllGames","Todos|All|Todos"); A("Favorites","★  Favoritos|★  Favorites|★  Favoritos"); A("Running","Em execução|Running|En ejecución"); A("StopAll","Parar todos|Stop all|Detener todos"); A("DetectionHint","Ative a detecção de jogos nas configurações do Discord.|Enable game detection in Discord settings.|Activa la detección de juegos en Discord.");
            A("SearchHint","Buscar nome, ID ou apelido...|Search name, ID or alias...|Buscar nombre, ID o alias...");
            A("LogsTitle","Tudo no seu controle.|Everything under your control.|Todo bajo tu control."); A("LogsSubtitle","Eventos locais da conexão e das atividades. Sem registrar chaves.|Local connection and activity events. No keys logged.|Eventos locales de conexión y actividades. Sin registrar claves.");
            A("CopyLogs","Copiar registro|Copy log|Copiar registro"); A("Clear","Limpar|Clear|Limpiar"); A("SettingsTitle","Do seu jeito.|Make it yours.|A tu manera."); A("Startup","Inicialização e bandeja|Startup and tray|Inicio y bandeja");
            A("AutoConnect","Conectar o último perfil ao abrir o app|Connect the last profile when opening the app|Conectar el último perfil al abrir la app"); A("AutoDiscord","Abrir Discord depois de verificar a VPN|Open Discord after verifying the VPN|Abrir Discord después de verificar la VPN");
            A("TrayOption","Continuar na bandeja ao fechar a janela|Keep running in the tray when closing the window|Continuar en la bandeja al cerrar la ventana");
            A("ElevationHint","O Windows pede permissão ao conectar. A inicialização usa uma tarefa autorizada por você.|Windows requests permission when connecting. Startup uses a task you authorize.|Windows pide permiso al conectar. El inicio usa una tarea autorizada por ti.");
            A("Appearance","Aparência e idioma|Appearance and language|Apariencia e idioma"); A("LightTheme","Usar tema claro|Use light theme|Usar tema claro"); A("About","Sobre este app|About this app|Acerca de esta app");
            A("AboutText","Callrift 1.3 · VPN para Discord · Presence · Interface nativa WPF · WireGuard / sing-box 1.14.4.|Callrift 1.3 · VPN for Discord · Presence · Native WPF interface · WireGuard / sing-box 1.14.4.|Callrift 1.3 · VPN para Discord · Presence · Interfaz nativa WPF · WireGuard / sing-box 1.14.4.");
            A("RouteDetail","Apenas Discord está na saída VPN. O adaptador virtual também classifica o tráfego dos outros apps, encaminhado diretamente. O DNS do Windows é preservado. Esta versão não tem kill switch.|Only Discord uses the VPN outbound. The virtual adapter also classifies other app traffic, forwarded directly. Windows DNS is preserved. This version has no kill switch.|Solo Discord usa la salida VPN. El adaptador virtual también clasifica el tráfico de otras apps, enviado directamente. Se conserva el DNS de Windows. Esta versión no tiene kill switch.");
            A("Licenses","Licenças e créditos|Licenses and credits|Licencias y créditos"); A("Run","Iniciar|Run|Iniciar"); A("Stop","Parar|Stop|Detener");
            A("Connect","Conectar VPN|Connect VPN|Conectar VPN"); A("Disconnect","Desconectar VPN|Disconnect VPN|Desconectar VPN"); A("Connecting","Conectando e verificando...|Connecting and verifying...|Conectando y verificando...");
            A("Ready","Pronto para conectar|Ready to connect|Listo para conectar"); A("Connected","Discord pela VPN|Discord through VPN|Discord por VPN"); A("Disconnected","Desconectado|Disconnected|Desconectado"); A("Working","Em andamento|Working|En curso");
            A("SelectProfile","Importe ou selecione um perfil da Proton VPN.|Import or select a Proton VPN profile.|Importa o selecciona un perfil de Proton VPN."); A("Verifying","Aguardando o Windows e verificando o túnel.|Waiting for Windows and verifying the tunnel.|Esperando a Windows y verificando el túnel.");
            A("DiscordOpen","Aberto|Running|Abierto"); A("DiscordClosed","Fechado|Closed|Cerrado"); A("NoResults","Nenhum aplicativo encontrado.|No apps found.|No se encontraron aplicaciones."); A("Loading","Carregando catálogo do Discord...|Loading Discord catalogue...|Cargando catálogo de Discord...");
            A("Catalogue","aplicativos|apps|aplicaciones"); A("Cache","Cache local|Local cache|Caché local"); A("Live","Catálogo atualizado|Catalogue updated|Catálogo actualizado"); A("Offline","Sem rede; usando cache|Offline; using cache|Sin conexión; usando caché");
            A("RemoveConfirm","Remover este perfil do aplicativo? O arquivo .conf original será preservado.|Remove this profile from the app? The original .conf file will be preserved.|¿Eliminar este perfil de la app? Se conservará el archivo .conf original.");
            A("Generate","Gerar .conf na Proton VPN  ↗|Generate .conf at Proton VPN  ↗|Generar .conf en Proton VPN  ↗");
            A("TestProfiles","Testar perfis|Test profiles|Probar perfiles"); A("CancelTest","Cancelar teste|Cancel test|Cancelar prueba");
            A("Testing","Testando...|Testing...|Probando..."); A("TestFailed","Sem resposta|No response|Sin respuesta"); A("NotTested","Não testado|Not tested|Sin probar"); A("PleaseWait","Aguarde|Please wait|Espera"); A("BestProfile","Melhor resultado|Best result|Mejor resultado");
            A("TestHint","Compare os perfis por HTTPS dentro de cada túnel, com a VPN desconectada.|Compare HTTPS through each tunnel while the VPN is disconnected.|Compara HTTPS por cada túnel con la VPN desconectada.");
            A("TestMetric","TEMPO HTTPS|HTTPS TIME|TIEMPO HTTPS"); A("RestartTitle","Reiniciar o Discord?|Restart Discord?|¿Reiniciar Discord?");
            A("RestartMessage","O Discord já está aberto. Para que as conexões existentes passem pela VPN, precisamos encerrá-lo e abri-lo novamente.\n\nIsso interrompe chamadas e compartilhamentos de tela em andamento. Autoriza reiniciar agora?|Discord is already running. Its existing connections need a restart to use the VPN.\n\nThis ends ongoing calls and screen sharing. Restart it now?|Discord ya está abierto. Para usar la VPN en las conexiones existentes, es necesario cerrarlo y abrirlo de nuevo.\n\nEsto interrumpe llamadas y transmisiones en curso. ¿Autorizas reiniciarlo ahora?");
            A("RestartDiscord","Reiniciar Discord|Restart Discord|Reiniciar Discord"); A("NotNow","Agora não|Not now|Ahora no"); A("RestartHint","Reinicie o Discord para renovar as conexões existentes.|Restart Discord to renew existing connections.|Reinicia Discord para renovar las conexiones existentes.");
        }
        public static string T(string key) { string[] values; if (!strings.TryGetValue(key,out values)) return key; return values[Language == "en-US" ? 1 : Language == "es-ES" ? 2 : 0]; }
    }
}
