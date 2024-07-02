using Dalamud.Configuration;
using Dalamud.Plugin;

namespace xivgpt
{
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; }
        public bool RemoveLineBreaks { get; set; }
        public bool ShowAdditionalInfo { get; set; }
        public bool ShowPrompt { get; set; }
        public string ApiKey { get; set; } = "";
        public int MaxTokens { get; set; }
        public const string Endpoint = "https://api.openai.com/v1/chat/completions";
        public const string Model = "gpt-4o";


        private IDalamudPluginInterface pluginInterface;

        public void Initialize(IDalamudPluginInterface pInterface)
        {
            pluginInterface = pInterface;
        }

        public void Save()
        {
            pluginInterface.SavePluginConfig(this);
        }
    }
}
