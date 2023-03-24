﻿using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.IoC;
using Dalamud.Utility;
using ImGuiNET;
using Newtonsoft.Json.Linq;

namespace xivgpt
{
    using Dalamud.Plugin;

    public class ChatGPTPlugin : IDalamudPlugin
    {
        public string Name =>"ChatGPT for FFXIV";
        private const string commandName = "/gpt";
        private static bool drawConfiguration;
        
        private Configuration configuration;
        private ChatGui chatGui;
        [PluginService] private static DalamudPluginInterface PluginInterface { get; set; } = null!;
        [PluginService] private static CommandManager CommandManager { get; set; } = null!;

        private string configKey;
        private int configMaxTokens;
        private bool configLineBreaks;
        private bool configAdditionalInfo;
        
        public ChatGPTPlugin([RequiredVersion("1.0")] DalamudPluginInterface dalamudPluginInterface, [RequiredVersion("1.0")] ChatGui chatGui, [RequiredVersion("1.0")] CommandManager commandManager)
        {
            this.chatGui = chatGui;

            configuration = (Configuration) dalamudPluginInterface.GetPluginConfig() ?? new Configuration();
            configuration.Initialize(dalamudPluginInterface);

            LoadConfiguration();
            
            dalamudPluginInterface.UiBuilder.Draw += DrawConfiguration;
            dalamudPluginInterface.UiBuilder.OpenConfigUi += OpenConfig;
            
            commandManager.AddHandler(commandName, new CommandInfo(GPTCommand)
            {
                HelpMessage = "/gpt whatever you want to ask ChatGPT/OpenAI's completion API",
                ShowInHelp = true
            });
        }
        private void GPTCommand(string command, string args)
        {
            if (configKey == string.Empty)
            {
                chatGui.Print("ChatGPT>> enter an API key in the configuration");
                OpenConfig();
                return;
            }
            
            if (args == string.Empty)
            {
                chatGui.Print("ChatGPT>> enter a prompt after the /gpt command");
                return;
            }

            Task.Run(() => SendPrompt(args));
        }

        private async Task SendPrompt(string input)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", configuration.ApiKey);

            var requestBody = $"{{\"model\": \"{Configuration.Model}\", \"prompt\": \"{input}\", \"max_tokens\": {configuration.MaxTokens}}}";
            var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            
            var response = await client.PostAsync(Configuration.Endpoint, content);
            var responseBody = await response.Content.ReadAsStringAsync();
            
            var responseJson = JObject.Parse(responseBody);
            var text = (string) responseJson.SelectToken("choices[0].text");
            
            if (text != null)
            {
                if(configLineBreaks)
                    text = text.Replace("\r", "").Replace("\n", "");
                
                const int chunkSize = 1000;
                var regex = new Regex(@".{1," + chunkSize + @"}(\s+|$)"); //jesus take the wheel
                var chunks = regex.Matches(text).Select(match => match.Value);
                chunks = chunks.ToList();
                
                if(configAdditionalInfo)
                    chatGui.Print($"ChatGPT \nprompt: {input}\nmodel: {Configuration.Model}\nmax_tokens: {configMaxTokens}\nresponse length: {text.Length}\nchunks: {chunks.Count()}");
                
                foreach (var chunk in chunks)
                    chatGui.Print($"ChatGPT: {chunk}");
            }
        }

        #region configuration
        
        private void DrawConfiguration()
        {
            if (!drawConfiguration)
                return;
            
            ImGui.Begin($"{Name} Configuration", ref drawConfiguration);
            
            ImGui.Separator();
            ImGui.Checkbox("remove line breaks from responses", ref configLineBreaks);
            ImGui.Checkbox("show additional info", ref configAdditionalInfo);
            ImGui.InputInt("max_tokens", ref configMaxTokens);
            ImGui.InputText("API Key", ref configKey, 60, ImGuiInputTextFlags.Password);

            if (ImGui.Button("Get API Key"))
            {
                const string apiKeysUrl = "https://platform.openai.com/account/api-keys";
                Util.OpenLink(apiKeysUrl);
            }

            ImGui.Separator();        

            
            
            if (ImGui.Button("Save and Close"))
            {
                SaveConfiguration();

                drawConfiguration = false;
            }
            
            ImGui.End();
        }
        
        private static void OpenConfig()
        {
            drawConfiguration = true;
        }

        private void LoadConfiguration()
        {
            configKey = configuration.ApiKey;
            configMaxTokens = configuration.MaxTokens != 0 ? configuration.MaxTokens : 256;
            configLineBreaks = configuration.RemoveLineBreaks;
            configAdditionalInfo = configuration.ShowAdditionalInfo;
        }

        private void SaveConfiguration()
        {
            configuration.ApiKey = configKey;
            configuration.MaxTokens = configMaxTokens;
            configuration.RemoveLineBreaks = configLineBreaks;
            configuration.ShowAdditionalInfo = configAdditionalInfo;
            
            PluginInterface.SavePluginConfig(configuration);
        }
        #endregion
        public void Dispose()
        {
            PluginInterface.UiBuilder.Draw -= DrawConfiguration;
            PluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;

            CommandManager.RemoveHandler(commandName);
        }
        
        
    }
}

