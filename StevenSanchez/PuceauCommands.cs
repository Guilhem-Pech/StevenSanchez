using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Newtonsoft.Json;
using PhoneNumbers;

namespace StevenSanchez
{
    public class PuceauCommands : BaseCommandModule
    {
        [Command("ping")] // let's define this method as a command
        [Description("Example ping command")] // this will be displayed to tell users what this command does when they invoke help
        [Aliases("pong")] // alternative names for the command
        public async Task Ping(CommandContext ctx) // this command takes no arguments
        {
            // let's trigger a typing indicator to let
            // users know we're working
            await ctx.TriggerTypingAsync();

            // let's make the message a bit more colourful
            var emoji = DiscordEmoji.FromName(ctx.Client, ":ping_pong:");

            // respond with ping
            await ctx.RespondAsync($"{emoji} Pong! Ping: {ctx.Client.Ping}ms");
        }
        
        
    }

    public class Puceau
    {
        public ulong userID;
        public DateTime birthday;
        public string phoneNumber;

        
        public Puceau(ulong discordUserId, DateTime _birthDate, string phoneNumber)
        {
            userID = discordUserId;
            birthday = _birthDate;
            this.phoneNumber = phoneNumber;
        }
        
        public Puceau() {}
    }

    public static class Utils
    {
        public static readonly HttpClient client = new HttpClient();

        public static async Task<Dictionary<ulong, Puceau>> WriteInformation(DiscordClient _client, DiscordGuild _guild)
        {
            DiscordEmoji emoteBirthday = DiscordEmoji.FromName(_client, ":birthday:");
            DiscordEmoji emotePhone = DiscordEmoji.FromName(_client, ":mobile_phone:");
            var cultureInfo = new CultureInfo("fr-FR");

            DiscordChannel lifeUpdate = _guild.GetChannel(824332186454327366);
            var messages = await lifeUpdate.GetMessagesAsync();
            using (var fs = File.Open("birthday.json", FileMode.OpenOrCreate))
            {
                fs.SetLength(0);
                Dictionary<ulong, Puceau> puceaux = new Dictionary<ulong, Puceau>();

                foreach (DiscordMessage discordMessage in messages)
                {
                    Puceau newPuceau = new Puceau();
                    newPuceau.userID = discordMessage.Author.Id;

                    foreach (var line in discordMessage.Content.Split(new[] { "\n" },
                                 StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (line.StartsWith(emoteBirthday.ToString()))
                        {
                            string[] entry = line.Split(new[] { ":" }, StringSplitOptions.RemoveEmptyEntries);
                            try
                            {
                                newPuceau.birthday = DateTime.Parse(entry[1], cultureInfo);
                            }
                            catch (FormatException)
                            {}
                        }
                        else if (line.StartsWith(emotePhone.ToString()))
                        {
                            string[] entry = line.Split(new[] { ":" }, StringSplitOptions.RemoveEmptyEntries);
                            var phoneNumberUtil = PhoneNumbers.PhoneNumberUtil.GetInstance();
                            try
                            {
                                entry[1] = entry[1].Trim();
                                entry[1] = entry[1].Replace(".", " ");

                                var phone = phoneNumberUtil.Parse(entry[1], "FR");
                                newPuceau.phoneNumber = phoneNumberUtil.Format(phone, PhoneNumberFormat.NATIONAL);
                            }
                            catch (Exception e)
                            {
                                newPuceau.phoneNumber = entry[1];
                            }
                        }

                        if (puceaux.ContainsKey(discordMessage.Author.Id))
                        {
                            puceaux[discordMessage.Author.Id] = newPuceau;
                        }
                        else
                        {
                            puceaux.Add(discordMessage.Author.Id, newPuceau);
                        }
                    }
                }
                using (var sr = new StreamWriter(fs, new UTF8Encoding(false)))
                    await sr.WriteAsync(JsonConvert.SerializeObject(puceaux, Formatting.Indented));
                return puceaux;
            }
        }

        public static async Task<Dictionary<ulong, Puceau>> GetPuceauxInfos()
        {
            string json = "";
            using (var fs = File.OpenRead("birthday.json"))
            {
                using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                    json = await sr.ReadToEndAsync();
                if (json.Length != 0)
                {
                    try
                    {
                        return JsonConvert.DeserializeObject<Dictionary<ulong, Puceau>>(json);
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }
            }
            return new Dictionary<ulong, Puceau>();
        }
        
        public static async Task<Puceau> GetPuceauInfo(DiscordUser _user, DiscordClient _client, DiscordGuild _guild)
        {
            var puceaux = await GetPuceauxInfos();
            if (puceaux.TryGetValue(_user.Id, out Puceau puceau))
            {
                return puceau;
            }
            else
            {
                puceaux = await WriteInformation(_client, _guild);
                if (puceaux.TryGetValue(_user.Id, out puceau))
                {
                    return puceau;
                }
            }
            throw new NullReferenceException("Puceau does not exist");
        }
        
        
    }
    
    

    class Result
    {
        public string range = "";
        public string majorDimantion = "";
        public List<List<string>> values = new List<List<string>>();
    }

    public class SlashCommands : ApplicationCommandModule
        {
            [SlashCommand("test", "A slash command made to test the DSharpPlus Slash Commands extension!")]
            public async Task TestCommand(InteractionContext ctx)
            {
                await Utils.WriteInformation(ctx.Client, ctx.Guild);
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Success!"));
            }
        
            [SlashCommand("birthday", "Try to get user's birthday")]
            public async Task GetBirthday(InteractionContext ctx, [Option("user", "User")] DiscordUser user)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
                try
                {
                    var puceau = await Utils.GetPuceauInfo(user, ctx.Client, ctx.Guild);
                    int now = int.Parse(DateTime.Now.ToString("yyyyMMdd"));
                    int dob = int.Parse(puceau.birthday.ToString("yyyyMMdd"));
                    int age = (now - dob) / 10000;

                    await ctx.EditResponseAsync(
                        new DiscordWebhookBuilder().WithContent(
                            $"L'anniversaire de {user.Mention} est le {puceau.birthday:dd MMMM} (iel a {age} ans)"));
                    
                }
                catch (Exception e)
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Le boloss de {user.Mention} n'a pas écrit sa date de naissance (ou pas correctement exemple :birthday: : 13 Février 1998)"));
                }
            }
            
            [SlashCommand("phone", "Try to get user's phone number")]
            public async Task GetPhone(InteractionContext ctx, [Option("user", "User")] DiscordUser user)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
                try
                {
                    var puceau = await Utils.GetPuceauInfo(user, ctx.Client, ctx.Guild);
                   
                    await ctx.EditResponseAsync(
                        new DiscordWebhookBuilder().WithContent(
                            $"Le numéro de {user.Mention} est le {puceau.phoneNumber}"));
                    
                }
                catch (Exception e)
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Le boloss de {user.Mention} n'a pas écrit son numéro de téléphone"));
                }
            }
            
            
        }
}