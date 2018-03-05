using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Reflection;
using System.Threading.Tasks;

namespace BlackBot {
    class DiscordHook {
        public static ISocketMessageChannel reportchannel;

        public static void SendReport(string message,string playername) {
            var embed = new EmbedBuilder();
            embed.WithTitle(playername);
            embed.WithDescription(message);
            embed.WithColor(255, 0, 0);
            reportchannel.SendMessageAsync("", false, embed).GetAwaiter().GetResult();
        }
    }

    public static class DiscordBot {
        static private DiscordSocketClient _client;
        static private CommandHandler _handler;

        public static async Task Connect() {
            _client = new DiscordSocketClient(new DiscordSocketConfig {
                WebSocketProvider = Discord.Net.Providers.WS4Net.WS4NetProvider.Instance
            });
            _handler = new CommandHandler(_client);
            await _client.LoginAsync(TokenType.Bot, "NDIwMjc2OTI2MDk2MzQzMDQw.DX8VAw.wP-XLvKnkKkIHF9olwM454fXozk");
            await _client.StartAsync();
            await Task.Delay(-1);
        }
    }

    public class CommandHandler {
        private DiscordSocketClient _client;
        CommandService _service;
        public static string prefix = ":";


        public CommandHandler(DiscordSocketClient client) {

            _client = client;
            _service = new CommandService();
            _service.AddModulesAsync(Assembly.GetEntryAssembly());
            _client.MessageReceived += HandleCommandAsync;
        }

        private async Task HandleCommandAsync(SocketMessage s) {
            var msg = s as SocketUserMessage;
            var context = new SocketCommandContext(_client, msg);

            if (msg == null) return;
            if (msg.Author.Id == _client.CurrentUser.Id || msg.Author.IsBot) return;
            int argPos = 0;
            if (msg.HasStringPrefix(prefix, ref argPos)) {
                var result = await _service.ExecuteAsync(context, argPos);
                if (!result.IsSuccess) {
                    await context.Channel.SendMessageAsync(result.ErrorReason);
                }
            }
            if (msg.HasCharPrefix(':', ref argPos)) {
                var result = await _service.ExecuteAsync(context, argPos);
            }
        }
    }
    public class Commands : ModuleBase<SocketCommandContext> {
        [Command("thischannel")]
        public async Task Thischannel(string word) {
            if (word == "report") {
                DiscordHook.reportchannel = Context.Channel;
            }
            //else if () {
            //}more channels to come
            else {
                await Context.Channel.SendMessageAsync("channel does not exist");
            }
        }
        [Command("changeprefix")]
        public async Task Changeprefix(string prefix) {
            CommandHandler.prefix = prefix;
            await Context.Channel.SendMessageAsync("New prefix is " + prefix);
        }
    }
}