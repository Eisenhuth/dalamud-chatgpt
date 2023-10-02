using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Utility;
using ImGuiNET;
using Newtonsoft.Json.Linq;

namespace xivgpt
{
    using Dalamud.Plugin;
    using Dalamud.Plugin.Services;

    public class ChatGPTPlugin : IDalamudPlugin
    {
        public string Name =>"ChatGPT for FFXIV";
        private const string commandName = "/gpt";
        private static bool drawConfiguration;
        
        private Configuration configuration;
        private IChatGui chatGui;
        [PluginService] private static DalamudPluginInterface PluginInterface { get; set; } = null!;
        [PluginService] private static ICommandManager CommandManager { get; set; } = null!;

        private string configKey;
        private int configMaxTokens;
        private bool configLineBreaks;
        private bool configAdditionalInfo;
        
        public ChatGPTPlugin([RequiredVersion("1.0")] DalamudPluginInterface dalamudPluginInterface, [RequiredVersion("1.0")] IChatGui chatGui, [RequiredVersion("1.0")] ICommandManager commandManager)
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
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", configKey);

            var requestBody = $"{{\"model\": \"{Configuration.Model}\", \"prompt\": \"{input}\", \"max_tokens\": {configMaxTokens}}}";
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
                    chatGui.Print($"ChatGPT>>\nprompt: {input}" +
                                  $"\nmodel: {Configuration.Model}" +
                                  $"\nmax_tokens: {configMaxTokens}" +
                                  $"\nresponse length: {text.Length}" +
                                  $"\nchunks: {chunks.Count()}");
                
                foreach (var chunk in chunks)
                    chatGui.Print($"ChatGPT: {chunk}");
            }
            else
            {
                var errorMessage = "ChatGPT>> Error: text is null";

                if (configAdditionalInfo)
                {
                    errorMessage += $"\nmodel: {Configuration.Model}" +
                                    $"\nmax_tokens: {configMaxTokens}" +
                                    $"\nresponse code: {(int) response.StatusCode} - {response.StatusCode}";
                }
                else
                    errorMessage += "\nYou can enable additional info in the configuration. If the issue persists, please report it on github.";

                chatGui.Print(errorMessage);
            }
        }

        #region configuration
        
        private void DrawConfiguration()
        {
            if (!drawConfiguration)
                return;
            
            ImGui.Begin($"{Name} Configuration", ref drawConfiguration);
            
            ImGui.Text("currently used model:");
            ImGui.SameLine();
            if (ImGui.SmallButton($"GPT-3.5/{Configuration.Model}"))
            {
                const string modelsDocs = "https://platform.openai.com/docs/models/gpt-3-5";
                Util.OpenLink(modelsDocs);
            }
            ImGui.Spacing();
            ImGui.InputText("API key", ref configKey, 60, ImGuiInputTextFlags.Password);
            ImGui.SameLine();
            if (ImGui.SmallButton("get API key"))
            {
                const string apiKeysUrl = "https://platform.openai.com/account/api-keys";
                Util.OpenLink(apiKeysUrl);
            }
            ImGui.Spacing();
            ImGui.SliderInt("max_tokens", ref configMaxTokens, 8, 4096);
            ImGui.SameLine();
            if (ImGui.SmallButton("learn more"))
            {
                const string conceptsDocs = "https://platform.openai.com/docs/introduction/key-concepts";
                Util.OpenLink(conceptsDocs);
            }
            ImGui.Separator();
            ImGui.Checkbox("remove line breaks from responses", ref configLineBreaks);
            ImGui.Checkbox("show additional info", ref configAdditionalInfo);
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

