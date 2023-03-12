using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
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
        private bool configLineBreaks;
        
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

            var requestBody = $"{{\"model\": \"{Configuration.Model}\", \"prompt\": \"{input}\", \"max_tokens\": 256}}";
            var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            
            var response = await client.PostAsync(Configuration.Endpoint, content);
            var responseBody = await response.Content.ReadAsStringAsync();
            
            var responseJson = JObject.Parse(responseBody);
            var text = (string) responseJson.SelectToken("choices[0].text");
            
            if (text != null)
            {
                if(configLineBreaks)
                    text = text.Replace("\r", "").Replace("\n", "");

                chatGui.Print($"ChatGPT prompt: {input}{text}");
            }
        }

        #region configuration
        
        private void DrawConfiguration()
        {
            if (!drawConfiguration)
                return;
            
            ImGui.Begin($"{Name} Configuration", ref drawConfiguration);
            
            ImGui.Separator();
            
            ImGui.InputText("API Key", ref configKey, 60, ImGuiInputTextFlags.Password);

            if (ImGui.Button("Get API Key"))
            {
                const string apiKeysUrl = "https://beta.openai.com/account/api-keys";
                Util.OpenLink(apiKeysUrl);
            }

            ImGui.Checkbox("remove line breaks from responses", ref configLineBreaks);
            
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
            configLineBreaks = configuration.RemoveLineBreaks;
        }

        private void SaveConfiguration()
        {
            configuration.ApiKey = configKey;
            configuration.RemoveLineBreaks = configLineBreaks;
            
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

