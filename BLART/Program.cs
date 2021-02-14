using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            public int MaxRentedVCs { get; set; } = 25;
            public List<ulong> ModeratorRoleIds { get; set; } = new List<ulong>()
            {
                0
            };
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
            _client = new DiscordSocketClient(new DiscordSocketConfig()
            {
                // LogLevel = LogSeverity.Verbose,
                AlwaysDownloadUsers = true,
                LargeThreshold = 10000,
                MessageCacheSize = 500
            });
            _client.Log += Log;
            _client.UserVoiceStateUpdated += UserVoiceUpdated;
            _client.ChannelUpdated += ChannelUpdated;

            if (!File.Exists(path))
                await File.WriteAllTextAsync(path, JsonConvert.SerializeObject(new Config(), Formatting.Indented));

            config = JsonConvert.DeserializeObject<Config>(await File.ReadAllTextAsync(path));

            await _client.LoginAsync(TokenType.Bot, config.Token);
            await _client.StartAsync();

            await Task.Delay(-1);
        }

        private  async Task ChannelUpdated(SocketChannel before, SocketChannel after)
        {
            if (after is SocketVoiceChannel channel && channel.CategoryId == config.CategoryId && channel.Id != config.SetupChannelId)
            {
                List<ulong> roles = new List<ulong>();
                List<ulong> users = new List<ulong>();
                foreach (Overwrite ovr in channel.PermissionOverwrites)
                {
                    var tuser = channel.Guild.GetUser(ovr.TargetId);
                    if (ovr.Permissions.MuteMembers == PermValue.Allow || ovr.Permissions.DeafenMembers == PermValue.Allow || ovr.Permissions.MoveMembers == PermValue.Allow || ovr.Permissions.CreateInstantInvite == PermValue.Allow)
                    {
                        if (ovr.TargetType == PermissionTarget.Role)
                        {
                            roles.Add(ovr.TargetId);
                        }
                        else
                        {
                            users.Add(ovr.TargetId);
                        }
                    }
                    if (ovr.TargetType == PermissionTarget.Role && ovr.TargetId != channel.Guild.EveryoneRole.Id)
                    {
                        roles.Add(ovr.TargetId);
                    }
                    else if (ovr.TargetType == PermissionTarget.User && (ovr.Permissions.ViewChannel == PermValue.Deny || ovr.Permissions.Connect == PermValue.Deny) && tuser != null && tuser.Roles.ToList().FindIndex(p => config.ModeratorRoleIds.Contains(p.Id)) != -1)
                    {
                        users.Add(ovr.TargetId);
                    }
                }
                for (int i = 0; i < roles.Count; i++)
                {
                    await channel.RemovePermissionOverwriteAsync(channel.Guild.GetRole(roles[i]));
                    await Task.Delay(100);
                }
                for (int i = 0; i < users.Count; i++)
                {
                    await channel.RemovePermissionOverwriteAsync(channel.Guild.GetUser(users[i]));
                    await Task.Delay(100);
                }
            }
        }

        private async Task UserVoiceUpdated(SocketUser arg1, SocketVoiceState arg2, SocketVoiceState arg3)
        {
            if (arg1 is SocketGuildUser user)
            {
                var chnl = user.VoiceChannel;
                if (chnl != null && chnl.Id == config.SetupChannelId)
                {
                    if (chnl.Category is SocketCategoryChannel cat)
                    {
                        if (cat.Channels.Count - 1 >= config.MaxRentedVCs)
                        {
                            await user.ModifyAsync(prop =>
                            {
                                prop.Channel = null;
                            });
                        }
                        else
                        {
                            RestVoiceChannel vc = await chnl.Guild.CreateVoiceChannelAsync(user.Username, prop =>
                            {
                                prop.CategoryId = config.CategoryId;
                            });
                            OverwritePermissions creatorperms = OverwritePermissions.DenyAll(vc);
                            creatorperms = creatorperms.Modify(viewChannel: PermValue.Allow, manageChannel: PermValue.Allow, manageRoles: PermValue.Allow, useVoiceActivation: PermValue.Allow, prioritySpeaker: PermValue.Allow, connect: PermValue.Allow, stream: PermValue.Allow, speak: PermValue.Allow);
                            await vc.AddPermissionOverwriteAsync(user, creatorperms);
                            await user.ModifyAsync(prop =>
                            {
                                prop.Channel = vc;
                            });
                        }
                    }
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
