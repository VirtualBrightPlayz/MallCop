using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace BLART
{
    public class Program
    {
        public class Config
        {
            public string Token { get; set; } = "bot_token";
            public ulong SetupChannelId { get; set; }
            public ulong CategoryId { get; set; }
        }

        private DiscordSocketClient _client;
        public Config config;
        private string path = "config.json";

        static void Main(string[] args)
        {
            new Program().MainAsync().GetAwaiter().GetResult();
        }

        public async Task MainAsync()
        {
            _client = new DiscordSocketClient();
            _client.Log += Log;
            _client.UserVoiceStateUpdated += UserVoiceUpdated;

            if (!File.Exists(path))
                await File.WriteAllTextAsync(path, JsonConvert.SerializeObject(new Config(), Formatting.Indented));

            config = JsonConvert.DeserializeObject<Config>(await File.ReadAllTextAsync(path));

            await _client.LoginAsync(TokenType.Bot, config.Token);
            await _client.StartAsync();

            await Task.Delay(-1);
        }

        private async Task UserVoiceUpdated(SocketUser arg1, SocketVoiceState arg2, SocketVoiceState arg3)
        {
            if (arg1 is SocketGuildUser user)
            {
                var chnl = user.VoiceChannel;
                if (chnl != null && chnl.Id == config.SetupChannelId)
                {
                    RestVoiceChannel vc = await chnl.Guild.CreateVoiceChannelAsync(user.Username, prop =>
                    {
                        prop.CategoryId = config.CategoryId;
                    });
                    await vc.AddPermissionOverwriteAsync(user, OverwritePermissions.AllowAll(vc));
                    await user.ModifyAsync(prop =>
                    {
                        prop.Channel = vc;
                    });
                }
                if (arg2.VoiceChannel != null && arg2.VoiceChannel.CategoryId == config.CategoryId && arg2.VoiceChannel.Id != config.SetupChannelId && arg2.VoiceChannel.Users.Count == 0)
                {
                    await arg2.VoiceChannel.DeleteAsync();
                }
                if (arg3.VoiceChannel != null && arg3.VoiceChannel.CategoryId == config.CategoryId && arg3.VoiceChannel.Id != config.SetupChannelId && arg3.VoiceChannel.Users.Count == 0)
                {
                    await arg3.VoiceChannel.DeleteAsync();
                }
            }
        }

        private Task Log(LogMessage arg)
        {
            Console.WriteLine(arg.ToString());
            return Task.CompletedTask;
        }
    }
}
